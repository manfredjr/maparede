using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using MapNet.Nucleo;

namespace MapNet.Testes;

/// <summary>
/// Identificação leve: leitura de HTTP, certificado, banner e UPnP, limpeza do texto que vem da
/// rede, a etapa com seus limites e o que aparece na tabela, no detalhe e no relatório.
/// </summary>
public class IdentificacaoTestes
{
    [Theory]
    [InlineData("<html><head><title>Painel do roteador</title></head></html>", "Painel do roteador")]
    [InlineData("<TITLE>\n   Impressão   &amp; digitalização \r\n</TITLE>", "Impressão & digitalização")]
    [InlineData("<title lang=\"pt\">Câmera &#8211; entrada</title>", "Câmera \u2013 entrada")]
    [InlineData("<html><body>sem título</body></html>", null)]
    public void Titulo_da_pagina(string html, string? esperado)
    {
        Assert.Equal(esperado, RespostaHttp.Titulo(html));
    }

    [Fact]
    public void Resposta_http_traz_codigo_servidor_e_localizacao()
    {
        var dados = RespostaHttp.Interpretar(Encoding.ASCII.GetBytes(
            "HTTP/1.1 302 Found\r\nServer: lighttpd/1.4\r\nLocation: https://192.0.2.1/login\r\n\r\n"));

        Assert.Equal(302, dados.Codigo);
        Assert.Equal("lighttpd/1.4", dados.Servidor);
        Assert.Equal("https://192.0.2.1/login", dados.Localizacao);
    }

    [Fact]
    public void Charset_do_cabecalho_e_da_pagina_sao_respeitados()
    {
        var latin = Encoding.Latin1;
        var cabecalho = latin.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=ISO-8859-1\r\n\r\n<title>Configuração</title>");
        var meta = latin.GetBytes("HTTP/1.1 200 OK\r\n\r\n<meta charset=\"iso-8859-1\"><title>Câmera</title>");

        Assert.Equal("Configuração", RespostaHttp.Interpretar(cabecalho).Titulo);
        Assert.Equal("Câmera", RespostaHttp.Interpretar(meta).Titulo);
    }

    [Fact]
    public void Texto_da_rede_perde_controle_e_e_cortado()
    {
        Assert.Equal("a b c", TextoRede.Limpar("a\u0000\u001b[31m b\t\tc\r\n".Replace("[31m", string.Empty)));
        Assert.Equal(TextoRede.Maximo, TextoRede.Limpar(new string('x', 5000))!.Length);
        Assert.Null(TextoRede.Limpar(" \r\n\t "));
        Assert.Equal("ab", TextoRede.Limpar("a\u200Bb"));
    }

    [Fact]
    public void Resposta_maior_que_o_limite_e_cortada()
    {
        var grande = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n\r\n" + new string('x', 100_000) + "<title>fora</title>");

        Assert.Null(RespostaHttp.Interpretar(grande).Titulo);
    }

    [Fact]
    public void Ssdp_le_servidor_e_localizacao()
    {
        var r = Ssdp.Interpretar(IPAddress.Parse("192.0.2.5"), Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nSERVER: Linux/5 UPnP/1.0 Exemplo/1\r\nLOCATION: http://192.0.2.5:49152/desc.xml\r\nST: upnp:rootdevice\r\n\r\n"));

        Assert.NotNull(r);
        Assert.Equal("Linux/5 UPnP/1.0 Exemplo/1", r!.Servidor);
        Assert.Equal(new Uri("http://192.0.2.5:49152/desc.xml"), Ssdp.DescricaoDoProprio(r));
        Assert.Null(Ssdp.Interpretar(IPAddress.Parse("192.0.2.5"), Encoding.ASCII.GetBytes("NOTIFY * HTTP/1.1\r\n\r\n")));
    }

    [Theory]
    [InlineData("http://192.0.2.9:49152/desc.xml")]
    [InlineData("https://192.0.2.5/desc.xml")]
    [InlineData("http://exemplo.example/desc.xml")]
    [InlineData("nao e endereco")]
    public void Descricao_so_e_lida_do_proprio_equipamento_por_http(string local)
    {
        Assert.Null(Ssdp.DescricaoDoProprio(new RespostaSsdp(IPAddress.Parse("192.0.2.5"), null, local)));
    }

    [Fact]
    public void Descricao_upnp_da_o_modelo()
    {
        const string xml = """
            <?xml version="1.0"?>
            <root xmlns="urn:schemas-upnp-org:device-1-0"><device>
              <friendlyName>NAS da sala</friendlyName><manufacturer>Exemplo</manufacturer><modelName>N-4</modelName>
            </device></root>
            """;

        Assert.Equal("NAS da sala (Exemplo N-4)", Ssdp.Modelo(xml));
    }

    [Fact]
    public void Descricao_com_dtd_e_recusada()
    {
        const string xml = """
            <?xml version="1.0"?>
            <!DOCTYPE root [<!ENTITY x SYSTEM "file:///c:/windows/win.ini">]>
            <root><device><friendlyName>&x;</friendlyName></device></root>
            """;

        Assert.Null(Ssdp.Modelo(xml));
    }

    [Fact]
    public async Task Http_de_verdade_le_titulo_e_servidor()
    {
        await using var servidor = ServidorLocal.Iniciar(async (fluxo, pedido) =>
        {
            Assert.StartsWith("GET / HTTP/1.1", pedido);
            Assert.Contains("User-Agent: MapNet-MT/", pedido);
            await fluxo.WriteAsync(Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nServer: exemplo\r\nContent-Type: text/html; charset=utf-8\r\n\r\n<title>Painel</title>"));
        });

        var s = await new IdentificadorRede().IdentificarAsync(IPAddress.Loopback, servidor.Porta, ProtocoloServico.Http, 3000, CancellationToken.None);

        Assert.NotNull(s);
        Assert.Equal("Painel", s!.Titulo);
        Assert.Equal("exemplo", s.Servidor);
    }

    [Fact]
    public async Task Https_le_certificado_vencido_e_autoassinado()
    {
        using var certificado = Certificado("painel.example", DateTimeOffset.Now.AddYears(-2), DateTimeOffset.Now.AddYears(-1));
        await using var servidor = ServidorLocal.Iniciar(async (fluxo, _) =>
            await fluxo.WriteAsync(Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\n\r\n<title>Seguro</title>")), certificado);

        var s = await new IdentificadorRede().IdentificarAsync(IPAddress.Loopback, servidor.Porta, ProtocoloServico.Https, 5000, CancellationToken.None);

        Assert.NotNull(s);
        Assert.Equal("painel.example", s!.CertificadoNome);
        Assert.True(s.CertificadoValidade < DateTimeOffset.Now);
        Assert.Equal("Seguro", s.Titulo);
    }

    [Fact]
    public async Task Banner_so_le_e_nao_envia_nada()
    {
        var recebidos = -1;
        await using var servidor = ServidorLocal.Banner("SSH-2.0-OpenSSH_9.2\r\n", n => recebidos = n);

        var s = await new IdentificadorRede().IdentificarAsync(IPAddress.Loopback, servidor.Porta, ProtocoloServico.Banner, 3000, CancellationToken.None);
        await servidor.DisposeAsync();

        Assert.Equal("SSH-2.0-OpenSSH_9.2", s!.Banner);
        Assert.Equal(0, recebidos);
    }

    [Fact]
    public async Task Servidor_mudo_termina_sem_servico()
    {
        await using var servidor = ServidorLocal.Banner(null, _ => { });

        var s = await new IdentificadorRede().IdentificarAsync(IPAddress.Loopback, servidor.Porta, ProtocoloServico.Banner, 300, CancellationToken.None);

        Assert.Null(s);
    }

    [Theory]
    [InlineData(80, ProtocoloServico.Http)]
    [InlineData(8000, ProtocoloServico.Http)]
    [InlineData(8443, ProtocoloServico.Https)]
    [InlineData(22, ProtocoloServico.Banner)]
    [InlineData(23, null)]
    [InlineData(445, null)]
    public void Protocolo_de_cada_porta(int porta, ProtocoloServico? esperado)
    {
        Assert.Equal(esperado, EtapaIdentificacao.Protocolo(porta));
    }

    [Fact]
    public async Task Etapa_so_conversa_com_portas_abertas_de_hosts_verificados()
    {
        var verificado = Host("192.0.2.1", [22, 23, 80, 445]);
        var fora = Host("192.0.2.2", []);
        fora.PortasAbertas = [80];
        var falso = new IdentificadorFalso
        {
            Upnp =
            [
                new(IPAddress.Parse("192.0.2.1"), "Exemplo UPnP", "http://192.0.2.1:49152/d.xml"),
                new(IPAddress.Parse("192.0.2.1"), "Exemplo UPnP", "http://192.0.2.1:49152/d.xml"),
                new(IPAddress.Parse("192.0.2.2"), "Outro", null),
                new(IPAddress.Parse("198.51.100.7"), "De fora", null),
            ],
        };

        await EtapaIdentificacao.IdentificarAsync([verificado, fora], IPAddress.Parse("192.0.2.10"), new OpcoesVarredura(), falso, null, CancellationToken.None);

        Assert.Equal([(IPAddress.Parse("192.0.2.1"), 22), (IPAddress.Parse("192.0.2.1"), 80)], falso.Pedidos.OrderBy(p => p.Porta));
        Assert.Equal([0, 22, 80], verificado.Servicos.Select(s => s.Porta));
        Assert.Equal("NAS (Exemplo N-4)", verificado.ServicoResumo);
        Assert.True(verificado.ServicosIdentificados);
        Assert.Empty(fora.Servicos);
        Assert.False(fora.ServicosIdentificados);
    }

    [Fact]
    public async Task Etapa_respeita_o_limite_de_simultaneas()
    {
        var hosts = Enumerable.Range(1, 30).Select(n => Host($"192.0.2.{n}", [80, 443])).ToList();
        var falso = new IdentificadorFalso { EsperaMs = 10 };

        await EtapaIdentificacao.IdentificarAsync(hosts, IPAddress.Parse("192.0.2.200"), new OpcoesVarredura { IdentificacoesSimultaneas = 5 }, falso, null, CancellationToken.None);

        Assert.Equal(60, falso.Pedidos.Count);
        Assert.InRange(falso.MaximoJuntas, 1, 5);
    }

    [Fact]
    public async Task Cancelar_para_a_identificacao()
    {
        var hosts = Enumerable.Range(1, 30).Select(n => Host($"192.0.2.{n}", [80])).ToList();
        using var cancelamento = new CancellationTokenSource();
        var falso = new IdentificadorFalso { EsperaMs = 20, AoIdentificar = cancelamento.Cancel };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            EtapaIdentificacao.IdentificarAsync(hosts, IPAddress.Parse("192.0.2.200"), new OpcoesVarredura(), falso, null, cancelamento.Token));

        Assert.True(falso.Pedidos.Count < 30);
    }

    [Fact]
    public void Tabela_detalhe_e_relatorio_mostram_os_servicos_codificados()
    {
        var h = Host("192.0.2.5", [80]);
        h.Servicos = [new ServicoIdentificado { Porta = 80, Protocolo = "HTTP", Titulo = "<script>alert(1)</script>", Servidor = "exemplo" }];
        h.ServicosIdentificados = true;
        var linha = new LinhaHost(h);

        Assert.Equal("<script>alert(1)</script>", linha.Servico);
        Assert.Equal("80 HTTP: título \"<script>alert(1)</script>\", servidor exemplo", linha.ServicoDica);
        Assert.True(linha.Contem("exemplo"));
        Assert.Equal(linha.ServicoDica, DetalheHost.Itens(h).Single(i => i.Rotulo == "Serviços").Valor);

        var r = Resultado(h);
        r.IdentificacaoFeita = true;
        var html = RelatorioHtml.Gerar(r);
        Assert.DoesNotContain("<script>alert(1)", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.Contains("<th data-tipo=\"texto\">Serviço</th>", html);
        Assert.Contains("feita nas portas abertas", WebUtility.HtmlDecode(html));
    }

    [Fact]
    public void Titulo_de_pagina_de_erro_nao_vira_resumo()
    {
        var h = Host("192.0.2.5", [80]);
        h.Servicos =
        [
            new ServicoIdentificado { Porta = 80, Protocolo = "HTTP", CodigoHttp = 403, Titulo = "403 Forbidden", Servidor = "Microsoft-IIS/10.0" },
        ];

        Assert.Equal("Microsoft-IIS/10.0", h.ServicoResumo);
        Assert.Contains("403 Forbidden", h.Servicos[0].Texto);
    }

    [Fact]
    public void Linha_de_comando_identifica_por_padrao_com_portas()
    {
        Assert.True(ArgumentosCli.Interpretar(["--varrer", "--portas"]).Opcoes.IdentificarServicos);

        var sem = ArgumentosCli.Interpretar(["--varrer", "--portas", "--sem-identificar"]);
        Assert.True(sem.Valido);
        Assert.False(sem.Opcoes.IdentificarServicos);

        var errado = ArgumentosCli.Interpretar(["--varrer", "--sem-identificar"]);
        Assert.Contains(errado.Erros, e => e.Contains("só vale junto com --portas"));
    }

    [Fact]
    public void Pergunta_diz_o_que_a_identificacao_envia()
    {
        var subRede = SubRede.Calcular(IPAddress.Parse("192.0.2.10"), 24);

        Assert.Contains("página inicial dos serviços web", PainelVarredura.PerguntaPortas(subRede, 24, false, identificar: true));
        Assert.DoesNotContain("página inicial", PainelVarredura.PerguntaPortas(subRede, 24, false, identificar: false));
    }

    [Fact]
    public async Task Demonstracao_traz_servicos_de_exemplo()
    {
        var dependencias = Demonstracao.Dependencias();
        dependencias.Opcoes.OlharPortas = true;

        var r = await dependencias.Varrer(dependencias.ListarInterfaces()[0], new Progress<ProgressoVarredura>(), CancellationToken.None);

        Assert.True(r.IdentificacaoFeita);
        Assert.Contains(r.Hosts, h => h.ServicoResumo.Contains("Impressora"));
        Assert.All(r.Hosts.Where(h => h.MacAleatorio), h => Assert.Empty(h.Servicos));
    }

    private static HostEncontrado Host(string ip, int[] abertas) => new()
    {
        Ip = IPAddress.Parse(ip),
        Mac = [0x00, 0x80, 0x77, 0x12, 0x34, 0x05],
        RespondeuPing = true,
        TempoPingMs = 1,
        PortasAbertas = abertas,
        PortasTestadas = ListaPortas.Padrao,
        PortasVerificadas = abertas.Length > 0,
    };

    private static ResultadoVarredura Resultado(HostEncontrado h)
    {
        var i = new InterfaceRede
        {
            Id = "teste",
            Nome = "Ethernet",
            Descricao = "Placa de teste",
            Tipo = TipoInterface.Cabo,
            Ip = IPAddress.Parse("192.0.2.10"),
            Prefixo = 24,
            Gateway = IPAddress.Parse("192.0.2.1"),
            Dns = [IPAddress.Parse("192.0.2.1")],
        };
        var inicio = new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);
        var r = new ResultadoVarredura { Interface = i, SubRedeVarrida = i.SubRede, Inicio = inicio, Fim = inicio.AddSeconds(30) };
        r.Hosts.Add(h);
        return r;
    }

    private static X509Certificate2 Certificado(string nome, DateTimeOffset inicio, DateTimeOffset fim)
    {
        using var chave = RSA.Create(2048);
        var pedido = new CertificateRequest($"CN={nome}", chave, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var criado = pedido.CreateSelfSigned(inicio, fim);

        // No Windows, o SslStream só usa a chave privada de um certificado importado de PFX.
        return new X509Certificate2(criado.Export(X509ContentType.Pfx));
    }

    /// <summary>Servidor TCP no próprio computador, com a resposta que o teste escolhe.</summary>
    private sealed class ServidorLocal : IAsyncDisposable
    {
        private readonly TcpListener _ouvinte = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _fim = new();
        private Task _laco = Task.CompletedTask;

        public int Porta => ((IPEndPoint)_ouvinte.LocalEndpoint).Port;

        public static ServidorLocal Iniciar(Func<Stream, string, Task> responder, X509Certificate2? certificado = null)
        {
            var s = new ServidorLocal();
            s._ouvinte.Start();
            s._laco = Task.Run(async () =>
            {
                using var cliente = await s._ouvinte.AcceptTcpClientAsync(s._fim.Token);
                Stream fluxo = cliente.GetStream();
                if (certificado != null)
                {
                    var tls = new SslStream(fluxo);
                    await tls.AuthenticateAsServerAsync(certificado);
                    fluxo = tls;
                }

                var buffer = new byte[4096];
                var n = await fluxo.ReadAsync(buffer, s._fim.Token);
                await responder(fluxo, Encoding.ASCII.GetString(buffer, 0, n));
                await fluxo.FlushAsync();
                fluxo.Dispose();
            });
            return s;
        }

        /// <summary>Manda o banner (ou nada) e conta os bytes que o cliente enviar até fechar.</summary>
        public static ServidorLocal Banner(string? banner, Action<int> recebidos)
        {
            var s = new ServidorLocal();
            s._ouvinte.Start();
            s._laco = Task.Run(async () =>
            {
                using var cliente = await s._ouvinte.AcceptTcpClientAsync(s._fim.Token);
                var fluxo = cliente.GetStream();
                if (banner != null)
                {
                    await fluxo.WriteAsync(Encoding.ASCII.GetBytes(banner));
                }

                var total = 0;
                var buffer = new byte[256];
                try
                {
                    int n;
                    while ((n = await fluxo.ReadAsync(buffer, s._fim.Token)) > 0)
                    {
                        total += n;
                    }
                }
                catch (Exception e) when (e is IOException or OperationCanceledException)
                {
                    // Cliente fechou ou o teste terminou.
                }

                recebidos(total);
            });
            return s;
        }

        private bool _encerrado;

        public async ValueTask DisposeAsync()
        {
            if (_encerrado)
            {
                return;
            }

            _encerrado = true;
            _fim.CancelAfter(TimeSpan.FromSeconds(2));
            try
            {
                await _laco;
            }
            catch (Exception e) when (e is OperationCanceledException or IOException or SocketException)
            {
                // Servidor encerrado no fim do teste.
            }

            _ouvinte.Stop();
            _fim.Dispose();
        }
    }

    /// <summary>Identificador simulado: registra os pedidos e conta quantos rodam juntos.</summary>
    private sealed class IdentificadorFalso : IIdentificador
    {
        private readonly object _trava = new();
        private int _agora;

        public List<(IPAddress Ip, int Porta)> Pedidos { get; } = [];

        public int MaximoJuntas { get; private set; }

        public int EsperaMs { get; init; } = 1;

        public Action? AoIdentificar { get; init; }

        public IReadOnlyList<RespostaSsdp> Upnp { get; init; } = [];

        public async Task<ServicoIdentificado?> IdentificarAsync(IPAddress ip, int porta, ProtocoloServico protocolo, int tempoMs, CancellationToken cancelamento)
        {
            lock (_trava)
            {
                Pedidos.Add((ip, porta));
                _agora++;
                MaximoJuntas = Math.Max(MaximoJuntas, _agora);
            }

            AoIdentificar?.Invoke();
            try
            {
                await Task.Delay(EsperaMs, CancellationToken.None);
                return new ServicoIdentificado { Porta = porta, Protocolo = protocolo.ToString().ToUpperInvariant(), Banner = protocolo == ProtocoloServico.Banner ? "SSH-2.0-exemplo" : null };
            }
            finally
            {
                lock (_trava)
                {
                    _agora--;
                }
            }
        }

        public Task<IReadOnlyList<RespostaSsdp>> BuscarUpnpAsync(IPAddress origem, int tempoMs, CancellationToken cancelamento) => Task.FromResult(Upnp);

        public Task<string?> LerDescricaoAsync(Uri endereco, int tempoMs, CancellationToken cancelamento) =>
            Task.FromResult<string?>("<root><device><friendlyName>NAS</friendlyName><manufacturer>Exemplo</manufacturer><modelName>N-4</modelName></device></root>");
    }
}

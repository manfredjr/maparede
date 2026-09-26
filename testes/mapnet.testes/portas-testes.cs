using System.Net;
using MapNet.Nucleo;

namespace MapNet.Testes;

/// <summary>
/// Etapa de portas: leitura da lista, quem entra na verificação, limites de conexão,
/// cancelamento e o que aparece na tabela, no detalhe, no relatório e na linha de comando.
/// </summary>
public class PortasTestes
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("padrao")]
    [InlineData("Padrão")]
    public void Lista_vazia_ou_padrao_usa_as_portas_comuns(string? texto)
    {
        var portas = ListaPortas.Interpretar(texto, out var erro);

        Assert.Null(erro);
        Assert.Equal(ListaPortas.Padrao, portas);
        Assert.Equal(24, portas!.Count);
    }

    [Fact]
    public void Lista_digitada_sai_em_ordem_e_sem_repeticao()
    {
        var portas = ListaPortas.Interpretar("443, 80;22 80", out var erro);

        Assert.Null(erro);
        Assert.Equal([22, 80, 443], portas);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("80,http")]
    [InlineData("-1")]
    public void Porta_invalida_e_recusada(string texto)
    {
        Assert.Null(ListaPortas.Interpretar(texto, out var erro));
        Assert.Contains("Porta inválida", erro);
    }

    [Fact]
    public void Mais_de_cem_portas_e_recusado()
    {
        var texto = string.Join(",", Enumerable.Range(1, 101));

        Assert.Null(ListaPortas.Interpretar(texto, out var erro));
        Assert.Contains("O limite é 100", erro);
    }

    [Fact]
    public void Texto_das_portas_leva_o_servico_quando_conhecido()
    {
        Assert.Equal("80 (HTTP), 9100 (Impressão direta), 8123", ListaPortas.Texto([80, 9100, 8123]));
    }

    [Fact]
    public void Este_computador_e_mac_aleatorio_ficam_fora_por_padrao()
    {
        var hosts = new[] { Host("192.0.2.1"), Host("192.0.2.10", proprio: true), Host("192.0.2.31", macAleatorio: true) };

        var escolhidos = EtapaPortas.Escolher(hosts, incluirMacAleatorio: false);

        Assert.Equal(["192.0.2.1"], escolhidos.Select(h => h.Ip.ToString()));
        Assert.Equal(EtapaPortas.MotivoEsteComputador, hosts[1].MotivoSemPortas);
        Assert.Equal(EtapaPortas.MotivoMacAleatorio, hosts[2].MotivoSemPortas);
    }

    [Fact]
    public void Mac_aleatorio_entra_quando_pedido()
    {
        var hosts = new[] { Host("192.0.2.1"), Host("192.0.2.31", macAleatorio: true) };

        var escolhidos = EtapaPortas.Escolher(hosts, incluirMacAleatorio: true);

        Assert.Equal(2, escolhidos.Count);
        Assert.Null(hosts[1].MotivoSemPortas);
    }

    [Fact]
    public async Task So_os_hosts_da_descoberta_sao_sondados_e_os_limites_valem()
    {
        var hosts = Enumerable.Range(1, 20).Select(n => Host($"192.0.2.{n}")).ToList();
        var opcoes = new OpcoesVarredura { Portas = ListaPortas.Padrao, ConexoesPortas = 16, ConexoesPorHost = 3 };
        var sonda = new SondaContadora(abertas: [80, 443]);

        await EtapaPortas.VerificarAsync(hosts, opcoes, sonda, null, CancellationToken.None);

        Assert.Equal(20 * 24, sonda.Total);
        Assert.All(sonda.Alvos, ip => Assert.Contains(hosts, h => h.Ip.Equals(ip)));
        Assert.InRange(sonda.MaximoGlobal, 1, 16);
        Assert.InRange(sonda.MaximoPorHost, 1, 3);
        Assert.All(hosts, h =>
        {
            Assert.True(h.PortasVerificadas);
            Assert.Equal([80, 443], h.PortasAbertas);
        });
    }

    [Fact]
    public async Task Cancelar_para_a_etapa_de_portas()
    {
        var hosts = Enumerable.Range(1, 10).Select(n => Host($"192.0.2.{n}")).ToList();
        using var cancelamento = new CancellationTokenSource();
        var sonda = new SondaContadora(abertas: [], aoSondar: () => cancelamento.Cancel(), esperaMs: 20);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            EtapaPortas.VerificarAsync(hosts, new OpcoesVarredura(), sonda, null, cancelamento.Token));

        Assert.True(sonda.Total < 10 * 24);
    }

    [Fact]
    public async Task Andamento_da_etapa_conta_os_hosts()
    {
        var hosts = new[] { Host("192.0.2.1"), Host("192.0.2.2") };
        var relatos = new List<ProgressoVarredura>();

        await EtapaPortas.VerificarAsync(hosts, new OpcoesVarredura { Portas = [80] }, new SondaContadora([80]), new Relator(relatos), CancellationToken.None);

        Assert.All(relatos, r => Assert.Equal(EtapaPortas.Nome, r.Etapa));
        Assert.Equal(2, relatos.Max(r => r.Concluidos));
    }

    [Fact]
    public void Linha_da_tabela_mostra_numeros_e_dica_com_servico()
    {
        var h = Host("192.0.2.5");
        h.PortasAbertas = [80, 9100];
        h.PortasVerificadas = true;
        var linha = new LinhaHost(h);

        Assert.Equal("80, 9100", linha.Portas);
        Assert.Equal("80 (HTTP), 9100 (Impressão direta)", linha.PortasDica);
        Assert.True(linha.Contem("9100"));
        Assert.False(linha.Contem("910"));
    }

    [Fact]
    public void Dica_da_linha_explica_por_que_nao_verificou()
    {
        var h = Host("192.0.2.31", macAleatorio: true);
        EtapaPortas.Escolher([h], incluirMacAleatorio: false);

        Assert.StartsWith("Fora da verificação por padrão", new LinhaHost(h).PortasDica);
    }

    [Fact]
    public void Detalhe_traz_as_portas_ou_o_motivo()
    {
        var aberto = Host("192.0.2.5");
        aberto.PortasAbertas = [22];
        aberto.PortasVerificadas = true;
        var fechado = Host("192.0.2.6");
        fechado.PortasVerificadas = true;
        var semEtapa = Host("192.0.2.7");

        Assert.Equal("22 (SSH)", Valor(aberto));
        Assert.Equal("nenhuma das portas verificadas está aberta", Valor(fechado));
        Assert.Equal("não verificadas nesta varredura", Valor(semEtapa));

        static string Valor(HostEncontrado h) => DetalheHost.Itens(h).Single(i => i.Rotulo == "Portas abertas").Valor;
    }

    [Theory]
    [InlineData(new[] { 80, 8080 }, false, null)]
    [InlineData(new[] { 8080 }, false, 8080)]
    [InlineData(new[] { 8000 }, false, 8000)]
    [InlineData(new[] { 22 }, false, null)]
    [InlineData(new[] { 8443 }, true, 8443)]
    [InlineData(new[] { 443, 8443 }, true, null)]
    public void Atalho_web_usa_a_porta_alternativa_quando_so_ela_esta_aberta(int[] abertas, bool https, int? esperada)
    {
        var h = Host("192.0.2.5");
        h.PortasAbertas = abertas;
        h.PortasVerificadas = true;

        Assert.Equal(esperada, DetalheHost.PortaWeb(h, https));
    }

    [Theory]
    [InlineData(AcaoHost.PastaCompartilhada, new[] { 80 }, false)]
    [InlineData(AcaoHost.PastaCompartilhada, new[] { 139 }, true)]
    [InlineData(AcaoHost.AreaDeTrabalhoRemota, new int[0], false)]
    [InlineData(AcaoHost.AreaDeTrabalhoRemota, new[] { 3389 }, true)]
    [InlineData(AcaoHost.AbrirHttp, new[] { 8000 }, true)]
    [InlineData(AcaoHost.AbrirHttps, new[] { 80 }, false)]
    public void Acao_desliga_quando_as_portas_do_servico_estao_fechadas(AcaoHost acao, int[] abertas, bool esperado)
    {
        var h = Host("192.0.2.5");
        h.PortasTestadas = ListaPortas.Padrao;
        h.PortasAbertas = abertas;
        h.PortasVerificadas = true;

        Assert.Equal(esperado, DetalheHost.Disponivel(h, acao));
    }

    [Fact]
    public void Acao_fica_ligada_sem_verificacao_ou_quando_a_porta_nao_estava_na_lista()
    {
        var semEtapa = Host("192.0.2.5");
        var outraLista = Host("192.0.2.6");
        outraLista.PortasTestadas = [80];
        outraLista.PortasVerificadas = true;

        Assert.True(DetalheHost.Disponivel(semEtapa, AcaoHost.PastaCompartilhada));
        Assert.True(DetalheHost.Disponivel(outraLista, AcaoHost.AreaDeTrabalhoRemota));
        Assert.False(DetalheHost.Disponivel(outraLista, AcaoHost.AbrirHttp));
    }

    [Fact]
    public void Atalho_web_sem_verificacao_fica_na_porta_comum()
    {
        Assert.Null(DetalheHost.PortaWeb(Host("192.0.2.5"), https: false));
        Assert.Equal("http://192.0.2.5:8080/", DetalheHost.Destino(AcaoHost.AbrirHttp, IPAddress.Parse("192.0.2.5"), 8080).Arquivo);
        Assert.Equal("https://192.0.2.5/", DetalheHost.Destino(AcaoHost.AbrirHttps, IPAddress.Parse("192.0.2.5")).Arquivo);
    }

    [Fact]
    public void Relatorio_tem_coluna_de_portas_e_diz_quais_foram_verificadas()
    {
        var r = Resultado();
        r.PortasVerificadas = [80, 443];
        r.Hosts[0].PortasAbertas = [80];
        r.Hosts[0].PortasVerificadas = true;

        var html = WebUtility.HtmlDecode(RelatorioHtml.Gerar(r));

        Assert.Contains("<th data-tipo=\"texto\">Portas</th>", html);
        Assert.Contains("title=\"80 (HTTP)\">80</td>", html);
        Assert.Contains("2 porta(s) TCP, só abrindo e fechando a conexão: 80 (HTTP), 443 (HTTPS)", html);
        Assert.Contains("com verificação de portas por conexão TCP", html);
        Assert.Contains("colspan=\"9\"", html);
    }

    [Fact]
    public void Relatorio_sem_a_etapa_diz_que_nao_verificou()
    {
        var html = WebUtility.HtmlDecode(RelatorioHtml.Gerar(Resultado()));

        Assert.Contains("não verificadas nesta varredura", html);
        Assert.DoesNotContain("com verificação de portas", html);
    }

    [Fact]
    public void Linha_de_comando_portas_sem_lista_usa_o_padrao()
    {
        var a = ArgumentosCli.Interpretar(["--varrer", "--portas", "--abrir"]);

        Assert.True(a.Valido);
        Assert.True(a.Opcoes.OlharPortas);
        Assert.Equal(ListaPortas.Padrao, a.Opcoes.Portas);
        Assert.True(a.Abrir);
    }

    [Fact]
    public void Linha_de_comando_portas_com_lista()
    {
        var a = ArgumentosCli.Interpretar(["--varrer", "--portas", "22,80", "--incluir-mac-aleatorio"]);

        Assert.True(a.Valido);
        Assert.Equal([22, 80], a.Opcoes.Portas);
        Assert.True(a.Opcoes.PortasEmMacAleatorio);
    }

    [Fact]
    public void Linha_de_comando_sem_portas_nao_verifica()
    {
        Assert.False(ArgumentosCli.Interpretar(["--varrer"]).Opcoes.OlharPortas);
    }

    [Theory]
    [InlineData(new[] { "--varrer", "--portas", "99999" }, "Porta inválida")]
    [InlineData(new[] { "--portas" }, "pedem o comando --varrer")]
    [InlineData(new[] { "--varrer", "--incluir-mac-aleatorio" }, "só vale junto com --portas")]
    public void Linha_de_comando_recusa_portas_erradas(string[] args, string trecho)
    {
        var a = ArgumentosCli.Interpretar(args);

        Assert.False(a.Valido);
        Assert.Contains(a.Erros, e => e.Contains(trecho));
    }

    [Fact]
    public void Pergunta_diz_o_que_vai_ser_enviado_e_lembra_da_autorizacao()
    {
        var pergunta = PainelVarredura.PerguntaPortas(SubRede.Calcular(IPAddress.Parse("192.0.2.10"), 24), 24, incluiMacAleatorio: false);

        Assert.Contains("192.0.2.0/24", pergunta);
        Assert.Contains("24 porta(s)", pergunta);
        Assert.Contains("sem enviar dados", pergunta);
        Assert.Contains("ficam de fora", pergunta);
        Assert.Contains("autorização", pergunta);
    }

    private static HostEncontrado Host(string ip, bool proprio = false, bool macAleatorio = false) => new()
    {
        Ip = IPAddress.Parse(ip),
        Mac = macAleatorio ? [0x02, 0x1A, 0x2B, 0x3C, 0x4D, 0x31] : [0x00, 0x80, 0x77, 0x12, 0x34, 0x05],
        RespondeuPing = true,
        TempoPingMs = 1,
        EhEsteComputador = proprio,
    };

    private static ResultadoVarredura Resultado()
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
        r.Hosts.Add(Host("192.0.2.1"));
        return r;
    }

    /// <summary>Sonda simulada: conta as conexões ao mesmo tempo, no total e por host.</summary>
    private sealed class SondaContadora(int[] abertas, Action? aoSondar = null, int esperaMs = 2) : ISondaPorta
    {
        private readonly object _trava = new();
        private readonly Dictionary<IPAddress, int> _porHost = [];
        private int _agora;

        public int Total { get; private set; }

        public int MaximoGlobal { get; private set; }

        public int MaximoPorHost { get; private set; }

        public HashSet<IPAddress> Alvos { get; } = [];

        public async Task<bool> AbertaAsync(IPAddress ip, int porta, int tempoMs, CancellationToken cancelamento)
        {
            lock (_trava)
            {
                Total++;
                Alvos.Add(ip);
                _agora++;
                MaximoGlobal = Math.Max(MaximoGlobal, _agora);
                _porHost[ip] = _porHost.GetValueOrDefault(ip) + 1;
                MaximoPorHost = Math.Max(MaximoPorHost, _porHost[ip]);
            }

            aoSondar?.Invoke();
            try
            {
                await Task.Delay(esperaMs, CancellationToken.None);
                return abertas.Contains(porta);
            }
            finally
            {
                lock (_trava)
                {
                    _agora--;
                    _porHost[ip]--;
                }
            }
        }
    }

    private sealed class Relator(List<ProgressoVarredura> relatos) : IProgress<ProgressoVarredura>
    {
        public void Report(ProgressoVarredura value)
        {
            lock (relatos)
            {
                relatos.Add(value);
            }
        }
    }
}

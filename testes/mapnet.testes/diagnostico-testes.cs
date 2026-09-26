using System.Buffers.Binary;
using System.Net;
using MapNet.Nucleo;

namespace MapNet.Testes;

/// <summary>
/// Ferramentas do console da fatia 4: alvo, ping, tracert, DNS e tabelas. Nenhum
/// teste manda pacote nem roda programa: tudo passa por versões simuladas.
/// </summary>
public class DiagnosticoTestes
{
    private static readonly IPAddress _destino = IPAddress.Parse("192.0.2.10");

    [Theory]
    [InlineData("192.0.2.1", true)]
    [InlineData(" 192.0.2.1 ", true)]
    [InlineData("2001:db8::1", true)]
    [InlineData("impressora.escritorio.example", true)]
    [InlineData("SERVIDOR", true)]
    [InlineData("host-01.example.", true)]
    [InlineData("", false)]
    [InlineData("1", false)]
    [InlineData("192.0.2.1; calc", false)]
    [InlineData("host & del", false)]
    [InlineData("-host", false)]
    [InlineData("host..example", false)]
    [InlineData("\"host\"", false)]
    [InlineData("host|more", false)]
    public void Alvo_aceita_so_ip_ou_nome_de_host(string texto, bool esperado)
    {
        Assert.Equal(esperado, Alvo.Valido(texto));
    }

    [Fact]
    public void Alvo_longo_demais_e_recusado()
    {
        Assert.False(Alvo.Valido(new string('a', 60) + "." + new string('b', 60) + "." + new string('c', 60) + "." + new string('d', 80)));
    }

    [Fact]
    public async Task Alvo_invalido_nem_chega_ao_dns()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Alvo.ResolverAsync("host; rm", CancellationToken.None));
    }

    [Fact]
    public void Estatistica_do_ping()
    {
        var e = EstatisticaPing.Calcular([3, null, 5, 10]);

        Assert.Equal(4, e.Enviados);
        Assert.Equal(3, e.Recebidos);
        Assert.Equal(25, e.PerdaPorcento);
        Assert.Equal(3, e.Minimo);
        Assert.Equal(6, e.Media);
        Assert.Equal(10, e.Maximo);
        Assert.Equal("4 enviados, 3 recebidos, 1 perdidos (25% de perda). Tempo: mínimo 3 ms, média 6 ms, máximo 10 ms", e.Texto());
    }

    [Fact]
    public void Estatistica_sem_nenhuma_resposta()
    {
        var e = EstatisticaPing.Calcular([null, null]);

        Assert.Equal(100, e.PerdaPorcento);
        Assert.Equal("2 enviados, 0 recebidos, 2 perdidos (100% de perda)", e.Texto());
    }

    [Fact]
    public async Task Ping_manda_quatro_pacotes_e_resume()
    {
        var pingador = new PingadorFixo(ttl => new RespostaPing(StatusPing.Respondeu, _destino, 4, 64));
        var (saida, linhas) = Saida();

        await new FerramentaPing(pingador, SemEspera).ExecutarAsync(new ParametrosFerramenta("192.0.2.10", "", false), saida, CancellationToken.None);

        Assert.Equal(4, pingador.Enviados);
        Assert.Equal(4, linhas.Count(l => l == "  Resposta de 192.0.2.10: 4 ms, TTL 64"));
        Assert.Contains(linhas, l => l.StartsWith("[L] Resumo: 4 enviados, 4 recebidos, 0 perdidos", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ping_continuo_para_no_cancelamento_e_resume()
    {
        using var parar = new CancellationTokenSource();
        var pingador = new PingadorFixo(_ => new RespostaPing(StatusPing.TempoEsgotado, null, 0, null));
        pingador.AoEnviar = n =>
        {
            if (n == 7)
            {
                parar.Cancel();
            }
        };
        var (saida, linhas) = Saida();

        await new FerramentaPing(pingador, SemEspera).ExecutarAsync(new ParametrosFerramenta("192.0.2.10", "", true), saida, parar.Token);

        Assert.Equal(7, pingador.Enviados);
        Assert.Contains("[L] Ping interrompido.", linhas);
        Assert.Contains(linhas, l => l.Contains("7 enviados, 0 recebidos"));
    }

    [Fact]
    public async Task Tracert_mostra_cada_salto_e_para_no_destino()
    {
        var caminho = new Dictionary<int, RespostaPing>
        {
            [1] = new(StatusPing.TtlExpirou, IPAddress.Parse("192.0.2.1"), 2, null),
            [2] = new(StatusPing.TempoEsgotado, null, 0, null),
            [3] = new(StatusPing.TtlExpirou, IPAddress.Parse("198.51.100.1"), 12, null),
            [4] = new(StatusPing.Respondeu, _destino, 15, 60),
        };
        var pingador = new PingadorFixo(ttl => caminho[ttl]);
        var (saida, linhas) = Saida();
        var nomes = (IPAddress ip, CancellationToken _) => Task.FromResult<string?>(ip.ToString() == "192.0.2.1" ? "roteador.example" : null);

        await new FerramentaTracert(pingador, nomes).ExecutarAsync(new ParametrosFerramenta("192.0.2.10", "", false), saida, CancellationToken.None);

        Assert.Equal(4, pingador.Enviados);
        Assert.Contains("  1     2 ms    192.0.2.1  roteador.example", linhas);
        Assert.Contains("  2      *    sem resposta", linhas);
        Assert.Contains("  3    12 ms    198.51.100.1", linhas);
        Assert.Contains("[L] Destino alcançado em 4 saltos.", linhas);
    }

    [Fact]
    public async Task Tracert_para_quando_o_destino_e_inalcancavel()
    {
        var pingador = new PingadorFixo(ttl => ttl == 1
            ? new RespostaPing(StatusPing.TtlExpirou, IPAddress.Parse("192.0.2.1"), 2, null)
            : new RespostaPing(StatusPing.Inalcancavel, IPAddress.Parse("192.0.2.1"), 3, null));
        var (saida, linhas) = Saida();

        await new FerramentaTracert(pingador, (_, _) => Task.FromResult<string?>(null))
            .ExecutarAsync(new ParametrosFerramenta("192.0.2.10", "", false), saida, CancellationToken.None);

        Assert.Equal(2, pingador.Enviados);
        Assert.Contains("[L] O caminho parou: destino inalcançável.", linhas);
    }

    [Fact]
    public void Consulta_dns_montada_pelo_nucleo()
    {
        var pacote = ConsultaDns.MontarConsulta(0x1234, "exemplo.example", ConsultaDns.TipoA);

        Assert.Equal(0x1234, BinaryPrimitives.ReadUInt16BigEndian(pacote));
        Assert.Equal(0x0100, BinaryPrimitives.ReadUInt16BigEndian(pacote.AsSpan(2)));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16BigEndian(pacote.AsSpan(4)));
        Assert.Equal([7, (byte)'e', (byte)'x'], pacote[12..15]);
        Assert.Equal([0, 0, 1, 0, 1], pacote[^5..]);
    }

    [Fact]
    public void Resposta_dns_com_cname_e_a()
    {
        var resposta = Resposta(0x4321, 0, "www.exemplo.example", ConsultaDns.TipoA,
            (ConsultaDns.TipoCname, 300, Nome("exemplo.example")),
            (ConsultaDns.TipoA, 60, [192, 0, 2, 80]));

        var r = ConsultaDns.Interpretar(resposta, 0x4321)!;

        Assert.Equal(0, r.Codigo);
        Assert.Equal(["CNAME", "A"], r.Registros.Select(x => x.Tipo));
        Assert.Equal("exemplo.example", r.Registros[0].Valor);
        Assert.Equal("192.0.2.80", r.Registros[1].Valor);
        Assert.Equal(60u, r.Registros[1].Ttl);
    }

    [Fact]
    public void Resposta_dns_com_aaaa_mx_e_ptr()
    {
        byte[] mx = [0, 10, .. Nome("correio.exemplo.example")];
        var resposta = Resposta(7, 0, "exemplo.example", ConsultaDns.TipoMx,
            (ConsultaDns.TipoAaaa, 60, IPAddress.Parse("2001:db8::80").GetAddressBytes()),
            (ConsultaDns.TipoMx, 60, mx),
            (ConsultaDns.TipoPtr, 60, Nome("host.exemplo.example")));

        var r = ConsultaDns.Interpretar(resposta, 7)!;

        Assert.Equal("2001:db8::80", r.Registros[0].Valor);
        Assert.Equal("correio.exemplo.example (preferência 10)", r.Registros[1].Valor);
        Assert.Equal("host.exemplo.example", r.Registros[2].Valor);
    }

    [Fact]
    public void Resposta_dns_de_nome_inexistente()
    {
        var r = ConsultaDns.Interpretar(Resposta(9, 3, "nada.example", ConsultaDns.TipoA), 9)!;

        Assert.Equal(3, r.Codigo);
        Assert.Equal("nome inexistente", r.TextoCodigo);
        Assert.Empty(r.Registros);
    }

    [Fact]
    public void Resposta_dns_malformada_ou_de_outra_pergunta_e_descartada()
    {
        var boa = Resposta(5, 0, "exemplo.example", ConsultaDns.TipoA, (ConsultaDns.TipoA, 60, [192, 0, 2, 80]));

        Assert.Null(ConsultaDns.Interpretar(boa, 6));
        Assert.Null(ConsultaDns.Interpretar(boa[..^3], 5));
        Assert.Null(ConsultaDns.Interpretar(boa[..8], 5));
        Assert.Null(ConsultaDns.Interpretar([], 5));

        var pergunta = ConsultaDns.MontarConsulta(5, "exemplo.example", ConsultaDns.TipoA);
        Assert.Null(ConsultaDns.Interpretar(pergunta, 5));

        // Ponteiro de compressão que aponta para ele mesmo: laço sem fim num leitor ingênuo.
        byte[] laco = [0, 5, 0x81, 0x80, 0, 0, 0, 1, 0, 0, 0, 0, 0xC0, 12, 0, 1, 0, 1, 0, 0, 0, 60, 0, 4, 1, 2, 3, 4];
        Assert.Null(ConsultaDns.Interpretar(laco, 5));
    }

    [Fact]
    public async Task Ferramenta_dns_pergunta_a_e_aaaa_para_nome()
    {
        var tipos = new List<ushort>();
        var (saida, linhas) = Saida();
        var dns = new FerramentaDns((servidor, pergunta, _, _) =>
        {
            Assert.Equal("192.0.2.53:53", servidor.ToString());
            var id = BinaryPrimitives.ReadUInt16BigEndian(pergunta);
            var tipo = (ushort)((pergunta[^4] << 8) | pergunta[^3]);
            tipos.Add(tipo);
            return Task.FromResult<byte[]?>(tipo == ConsultaDns.TipoA
                ? Resposta(id, 0, "exemplo.example", tipo, (ConsultaDns.TipoA, 60, [192, 0, 2, 80]))
                : Resposta(id, 0, "exemplo.example", tipo));
        });

        await dns.ExecutarAsync(new ParametrosFerramenta("exemplo.example", "192.0.2.53", false), saida, CancellationToken.None);

        Assert.Equal([ConsultaDns.TipoA, ConsultaDns.TipoAaaa], tipos);
        Assert.Contains(linhas, l => l.StartsWith("  A     exemplo.example  192.0.2.80  (TTL 60 s", StringComparison.Ordinal));
        Assert.Contains(linhas, l => l.StartsWith("  AAAA: sem registro", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ferramenta_dns_com_ip_pergunta_o_ptr_e_avisa_sem_resposta()
    {
        string? nome = null;
        var (saida, linhas) = Saida();
        var dns = new FerramentaDns((_, pergunta, _, _) =>
        {
            var pos = 12;
            nome = Mdns.LerNome(pergunta, ref pos);
            return Task.FromResult<byte[]?>(null);
        });

        await dns.ExecutarAsync(new ParametrosFerramenta("192.0.2.10", "192.0.2.53", false), saida, CancellationToken.None);

        Assert.Equal("10.2.0.192.in-addr.arpa", nome);
        Assert.Contains("  PTR: o servidor não respondeu em 3 segundos.", linhas);
    }

    [Fact]
    public async Task Ferramenta_dns_sem_servidor_valido_nao_pergunta()
    {
        var perguntou = false;
        var dns = new FerramentaDns((_, _, _, _) =>
        {
            perguntou = true;
            return Task.FromResult<byte[]?>(null);
        });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            dns.ExecutarAsync(new ParametrosFerramenta("exemplo.example", "servidor; calc", false), Saida().Saida, CancellationToken.None));
        Assert.False(perguntou);
    }

    [Fact]
    public void Nome_reverso_do_ipv6()
    {
        Assert.Equal("1.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.8.b.d.0.1.0.0.2.ip6.arpa",
            FerramentaDns.NomeReverso(IPAddress.Parse("2001:db8::1")));
    }

    [Theory]
    [InlineData(0x5000u, 80)]
    [InlineData(0xBB01u, 443)]
    [InlineData(0x3500u, 53)]
    public void Porta_vem_na_ordem_da_rede(uint valor, int porta)
    {
        Assert.Equal(porta, TextoTabelas.PortaDaApi(valor));
    }

    [Theory]
    [InlineData(2, "escutando")]
    [InlineData(5, "estabelecida")]
    [InlineData(11, "TIME-WAIT")]
    [InlineData(99, "desconhecido")]
    public void Estado_da_conexao_tcp(int estado, string texto)
    {
        Assert.Equal(texto, TextoTabelas.EstadoTcp(estado));
    }

    [Fact]
    public void Arp_esconde_multicast_broadcast_e_invalidas()
    {
        byte[] mac = [0x00, 0x00, 0x0C, 0x12, 0x34, 0x01];
        LinhaArp[] linhas =
        [
            new(IPAddress.Parse("192.0.2.1"), mac, 3, "Wi-Fi"),
            new(IPAddress.Parse("192.0.2.9"), [0, 0, 0, 0, 0, 0], 2, "Wi-Fi"),
            new(IPAddress.Parse("224.0.0.251"), [0x01, 0x00, 0x5E, 0x00, 0x00, 0xFB], 4, "Wi-Fi"),
            new(IPAddress.Parse("192.0.2.255"), [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF], 4, "Wi-Fi"),
            new(IPAddress.Parse("192.0.2.5"), [0x00, 0x80, 0x77, 0x12, 0x34, 0x05], 4, "Wi-Fi"),
        ];

        var texto = TextoTabelas.Arp(linhas).ToList();

        Assert.Equal(3, texto.Count);
        Assert.Contains(texto, l => l.Contains("192.0.2.1") && l.Contains("00:00:0C:12:34:01") && l.Contains("dinâmico"));
        Assert.Contains(texto, l => l.Contains("192.0.2.5") && l.Contains("estático"));
    }

    [Fact]
    public void Rotas_mostram_prefixo_e_rede_local()
    {
        LinhaRota[] rotas =
        [
            new(IPAddress.Parse("192.0.2.0"), IPAddress.Parse("255.255.255.0"), IPAddress.Any, "Wi-Fi", 291),
            new(IPAddress.Any, IPAddress.Any, IPAddress.Parse("192.0.2.1"), "Wi-Fi", 35),
        ];

        var texto = TextoTabelas.Rotas(rotas).ToList();

        Assert.StartsWith("  0.0.0.0/0", texto[1]);
        Assert.Contains("192.0.2.1", texto[1]);
        Assert.Contains("192.0.2.0/24", texto[2]);
        Assert.Contains("na rede local", texto[2]);
    }

    [Fact]
    public void Conexoes_mostram_programa_e_estado()
    {
        LinhaConexao[] conexoes =
        [
            new("TCP", IPAddress.Parse("192.0.2.23"), 50433, IPAddress.Parse("203.0.113.10"), 443, 5, 7310, "navegador"),
            new("UDP", IPAddress.Any, 5353, null, 0, 0, 900, null),
        ];

        var texto = TextoTabelas.Conexoes(conexoes).ToList();

        Assert.Contains(texto, l => l.Contains("192.0.2.23:50433") && l.Contains("203.0.113.10:443") && l.Contains("estabelecida") && l.Contains("navegador (7310)"));
        Assert.Contains(texto, l => l.Contains("0.0.0.0:5353") && l.Contains("processo 900"));
    }

    [Fact]
    public async Task Ferramenta_de_tabela_resume_e_lista()
    {
        var (saida, linhas) = Saida();

        await FerramentaTabela.Rotas(new TabelasFixas()).ExecutarAsync(new ParametrosFerramenta("", "", false), saida, CancellationToken.None);

        Assert.Equal("[L] Rotas: 1 rotas.", linhas[0]);
        Assert.Equal(3, linhas.Count);
    }



    [Fact]
    public async Task Aba_troca_executar_por_parar_e_escreve_o_erro()
    {
        var liberar = new TaskCompletionSource();
        var aba = new AbaConsole("Teste", new RegistroConsole(), new FerramentaFixa(async (_, _, c) =>
        {
            await liberar.Task;
            throw new InvalidOperationException("sem rede");
        }));

        var rodando = aba.ExecutarAsync();
        Assert.True(aba.Rodando);
        Assert.Equal("Parar", aba.TextoBotao);

        liberar.SetResult();
        await rodando;

        Assert.False(aba.Rodando);
        Assert.Equal("Executar", aba.TextoBotao);
        Assert.EndsWith("Teste: sem rede", aba.Registro.Linhas[^1]);
    }

    [Fact]
    public async Task Parar_cancela_a_ferramenta()
    {
        var aba = new AbaConsole("Teste", new RegistroConsole(), new FerramentaFixa((_, _, c) => Task.Delay(Timeout.Infinite, c)));

        var rodando = aba.ExecutarAsync();
        aba.ComandoExecutar.Execute(null);
        await rodando;

        Assert.False(aba.Rodando);
        Assert.EndsWith("Teste interrompido.", aba.Registro.Linhas[^1]);
    }

    [Fact]
    public void Padroes_nao_apagam_o_que_o_tecnico_digitou()
    {
        var aba = new AbaConsole("Ping", new RegistroConsole(), new FerramentaFixa((_, _, _) => Task.CompletedTask));

        aba.DefinirPadroes("192.0.2.1", "192.0.2.53");
        Assert.Equal("192.0.2.1", aba.Alvo);

        aba.DefinirPadroes("198.51.100.1", "198.51.100.53");
        Assert.Equal("198.51.100.1", aba.Alvo);

        aba.Alvo = "impressora.example";
        aba.DefinirPadroes("192.0.2.1", "192.0.2.53");
        Assert.Equal("impressora.example", aba.Alvo);
        Assert.Equal("192.0.2.53", aba.Servidor);
    }

    [Fact]
    public void Painel_tem_a_aba_varredura_e_uma_por_ferramenta_com_o_gateway()
    {
        var painel = new PainelVarredura(new DependenciasPainel
        {
            ListarInterfaces = () => [MaquinaTestes.Interface(TipoInterface.Cabo)],
            Varrer = (_, _, _) => Task.FromResult<ResultadoVarredura>(null!),
            SalvarEm = (_, c) => Task.FromResult(c),
            Ferramentas = () => Demonstracao.Ferramentas(),
        });
        painel.CarregarInterfaces();

        Assert.Equal(["Varredura", "Ping", "Tracert", "DNS", "ARP", "Conexões", "Rotas"], painel.Abas.Select(a => a.Titulo));
        Assert.Same(painel.Console, painel.Abas[0].Registro);
        Assert.Same(painel.Abas[0], painel.AbaSelecionada);
        Assert.Equal("192.0.2.1", painel.Abas[1].Alvo);
        Assert.Equal("192.0.2.1", painel.Abas[3].Servidor);
    }

    [Fact]
    public async Task Ferramentas_da_demonstracao_rodam_sem_rede()
    {
        foreach (var f in Demonstracao.Ferramentas())
        {
            var (saida, linhas) = Saida();
            await f.ExecutarAsync(new ParametrosFerramenta("192.0.2.10", "192.0.2.1", false), saida, CancellationToken.None);

            Assert.NotEmpty(linhas);
            Assert.DoesNotContain(linhas, l => l.Contains("não respondeu em"));
            Assert.Empty(linhas.SelectMany(CaracteresProibidosTestes.Proibidos));
        }
    }

    private static Task SemEspera(TimeSpan tempo, CancellationToken cancelamento)
    {
        cancelamento.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private static (SaidaFerramenta Saida, List<string> Linhas) Saida()
    {
        var linhas = new List<string>();
        return (new SaidaFerramenta(l => linhas.Add("[L] " + l), linhas.Add), linhas);
    }

    private static byte[] Nome(string nome)
    {
        var bytes = new List<byte>();
        foreach (var rotulo in nome.Split('.'))
        {
            bytes.Add((byte)rotulo.Length);
            bytes.AddRange(System.Text.Encoding.ASCII.GetBytes(rotulo));
        }

        bytes.Add(0);
        return bytes.ToArray();
    }

    /// <summary>Pacote de resposta DNS com a pergunta repetida e os registros dados.</summary>
    private static byte[] Resposta(ushort id, int codigo, string nome, ushort tipo, params (ushort Tipo, uint Ttl, byte[] Dados)[] registros)
    {
        var pacote = new List<byte>();
        var cabecalho = new byte[12];
        BinaryPrimitives.WriteUInt16BigEndian(cabecalho, id);
        BinaryPrimitives.WriteUInt16BigEndian(cabecalho.AsSpan(2), (ushort)(0x8180 | codigo));
        BinaryPrimitives.WriteUInt16BigEndian(cabecalho.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt16BigEndian(cabecalho.AsSpan(6), (ushort)registros.Length);
        pacote.AddRange(cabecalho);
        pacote.AddRange(Nome(nome));
        pacote.AddRange([(byte)(tipo >> 8), (byte)tipo, 0, 1]);
        foreach (var r in registros)
        {
            pacote.AddRange([0xC0, 12, (byte)(r.Tipo >> 8), (byte)r.Tipo, 0, 1]);
            var ttl = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(ttl, r.Ttl);
            pacote.AddRange(ttl);
            pacote.AddRange([(byte)(r.Dados.Length >> 8), (byte)r.Dados.Length]);
            pacote.AddRange(r.Dados);
        }

        return pacote.ToArray();
    }

    private sealed class PingadorFixo(Func<int, RespostaPing> resposta) : IPingador
    {
        public int Enviados { get; private set; }

        public Action<int>? AoEnviar { get; set; }

        public Task<RespostaPing> EnviarAsync(IPAddress destino, int ttl, int tempoMs, CancellationToken cancelamento)
        {
            cancelamento.ThrowIfCancellationRequested();
            Enviados++;
            AoEnviar?.Invoke(Enviados);
            return Task.FromResult(resposta(ttl));
        }
    }

    private sealed class TabelasFixas : ITabelasRede
    {
        public IReadOnlyList<LinhaArp> Arp() => [];

        public IReadOnlyList<LinhaRota> Rotas() => [new(IPAddress.Any, IPAddress.Any, IPAddress.Parse("192.0.2.1"), "Wi-Fi", 35)];

        public IReadOnlyList<LinhaConexao> Conexoes() => [];
    }


    private sealed class FerramentaFixa(Func<ParametrosFerramenta, SaidaFerramenta, CancellationToken, Task> executar) : IFerramenta
    {
        public string Titulo => "Teste";

        public string Descricao => "Ferramenta de teste.";

        public bool PedeAlvo => true;

        public bool PedeServidor => true;

        public bool PodeContinuo => false;

        public Task ExecutarAsync(ParametrosFerramenta parametros, SaidaFerramenta saida, CancellationToken cancelamento) =>
            executar(parametros, saida, cancelamento);
    }
}

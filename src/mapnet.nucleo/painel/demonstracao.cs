using System.Net;

namespace MapNet.Nucleo;

/// <summary>
/// Modo de demonstração (mapnet.exe --demonstracao): a tela inteira com dados de exemplo, sem
/// varrer nem consultar nada. Serve para as imagens da documentação e dos Pull Requests, que não
/// podem mostrar rede de ninguém. Os endereços são os reservados para documentação (192.0.2.x,
/// 198.51.100.x, 203.0.113.x e 2001:db8::) e os nomes são inventados.
/// </summary>
public static class Demonstracao
{
    public const string Argumento = "--demonstracao";

    public static DependenciasPainel Dependencias()
    {
        var wifi = new InterfaceRede
        {
            Id = "{D3B0C4F1-0000-4000-8000-000000000001}",
            Nome = "Wi-Fi",
            Descricao = "Placa Wi-Fi de exemplo",
            Tipo = TipoInterface.WiFi,
            Mac = [0x02, 0x00, 0x00, 0x00, 0x00, 0x23],
            Ip = IPAddress.Parse("192.0.2.23"),
            Prefixo = 24,
            Gateway = IPAddress.Parse("192.0.2.1"),
            Dns = [IPAddress.Parse("192.0.2.1")],
            VelocidadeBps = 866_000_000,
        };
        var cabo = new InterfaceRede
        {
            Id = "{D3B0C4F1-0000-4000-8000-000000000002}",
            Nome = "Ethernet",
            Descricao = "Placa de rede de exemplo",
            Tipo = TipoInterface.Cabo,
            Mac = [0x02, 0x00, 0x00, 0x00, 0x00, 0x40],
            Ip = IPAddress.Parse("198.51.100.40"),
            Prefixo = 24,
            Gateway = IPAddress.Parse("198.51.100.1"),
            Dns = [IPAddress.Parse("198.51.100.1")],
            VelocidadeBps = 1_000_000_000,
        };

        var opcoes = new OpcoesVarredura();
        return new DependenciasPainel
        {
            Opcoes = opcoes,
            ListarInterfaces = () => [wifi, cabo],
            Varrer = (i, p, c) => VarrerAsync(i, opcoes, p, c),
            SalvarEm = (r, caminho) => RelatorioHtml.SalvarAsync(r, caminho),
            // Documentos Públicos: o caminho não traz o nome do usuário, que apareceria nas imagens.
            PastaRelatorios = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "MapNet - MT", "demonstracao"),
            LerMaquina = i => Task.FromResult(LeitorMaquina.Ler(i, Fontes(), new OpcoesVarredura().PrefixoMinimo)),
            IpPublico = new IpPublicoDemonstracao(),
            Ferramentas = Ferramentas,
            Manutencao = new ManutencaoDemonstracao(),
        };
    }

    /// <summary>Não roda nada no Windows: devolve a saída que os comandos dariam.</summary>
    private sealed class ManutencaoDemonstracao : IExecutorManutencao
    {
        public async Task<ResultadoManutencao> ExecutarAsync(AcaoManutencao acao, CancellationToken cancelamento)
        {
            await Task.Delay(600, cancelamento).ConfigureAwait(true);
            var saida = acao switch
            {
                AcaoManutencao.LimparCacheDns => "> ipconfig /flushdns\nConfiguração de IP do Windows\nCache do DNS Resolver liberado com êxito.",
                AcaoManutencao.RenovarIp => "> ipconfig /release\nConfiguração de IP do Windows\n> ipconfig /renew\nConfiguração de IP do Windows\n   Endereço IPv4: 192.0.2.23",
                AcaoManutencao.LimparArp => "> netsh interface ip delete arpcache\nOk.",
                _ => "> netsh winsock reset\nO Catálogo do Winsock foi redefinido com êxito.\n> netsh int ip reset\nRedefinindo, OK!",
            };
            return new ResultadoManutencao(false, 0, saida);
        }
    }

    /// <summary>As ferramentas do console com respostas de exemplo, sem tocar na rede nem no Windows.</summary>
    public static IReadOnlyList<IFerramenta> Ferramentas()
    {
        var pingador = new PingadorDemonstracao();
        var espera = (TimeSpan t, CancellationToken c) => Task.Delay(TimeSpan.FromMilliseconds(t.TotalMilliseconds / 4), c);
        var tabelas = new TabelasDemonstracao();
        return
        [
            new FerramentaPing(pingador, espera),
            new FerramentaTracert(pingador, (ip, _) => Task.FromResult(NomeDoSalto(ip))),
            new FerramentaDns(RespostaDnsDemonstracao),
            FerramentaTabela.Arp(tabelas),
            FerramentaTabela.Conexoes(tabelas),
            FerramentaTabela.Rotas(tabelas),
        ];
    }

    private static string? NomeDoSalto(IPAddress ip) => ip.ToString() switch
    {
        "192.0.2.1" => "roteador.escritorio.example",
        "198.51.100.1" => "borda.provedor.example",
        _ => null,
    };

    /// <summary>Caminho de exemplo: roteador, provedor, um salto mudo e o destino.</summary>
    private sealed class PingadorDemonstracao : IPingador
    {
        private static readonly string?[] _caminho = ["192.0.2.1", "198.51.100.1", null, "203.0.113.10"];

        public async Task<RespostaPing> EnviarAsync(IPAddress destino, int ttl, int tempoMs, CancellationToken cancelamento)
        {
            await Task.Delay(120, cancelamento).ConfigureAwait(true);
            if (ttl >= 128 || ttl >= _caminho.Length)
            {
                return new RespostaPing(StatusPing.Respondeu, destino, 18 + Random.Shared.Next(0, 6), 57);
            }

            return _caminho[ttl - 1] is { } salto
                ? new RespostaPing(StatusPing.TtlExpirou, IPAddress.Parse(salto), 2 + (ttl * 5), null)
                : new RespostaPing(StatusPing.TempoEsgotado, null, 0, null);
        }
    }

    /// <summary>Responde qualquer pergunta com um registro de exemplo, montando o pacote de volta.</summary>
    private static Task<byte[]?> RespostaDnsDemonstracao(IPEndPoint servidor, byte[] pergunta, int tempoMs, CancellationToken cancelamento)
    {
        var tipo = (ushort)((pergunta[^4] << 8) | pergunta[^3]);
        byte[] dados = tipo switch
        {
            ConsultaDns.TipoA => [192, 0, 2, 80],
            ConsultaDns.TipoAaaa => IPAddress.Parse("2001:db8::80").GetAddressBytes(),
            _ => NomeEmBytes("servidor-de-arquivos.escritorio.example"),
        };
        var resposta = new List<byte>(pergunta);
        resposta[2] = 0x81;
        resposta[3] = 0x80;
        resposta[7] = 1;
        resposta.AddRange([0xC0, 0x0C, (byte)(tipo >> 8), (byte)tipo, 0, 1, 0, 0, 0x0E, 0x10, 0, (byte)dados.Length]);
        resposta.AddRange(dados);
        return Task.FromResult<byte[]?>(resposta.ToArray());
    }

    private static byte[] NomeEmBytes(string nome)
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

    private sealed class TabelasDemonstracao : ITabelasRede
    {
        public IReadOnlyList<LinhaArp> Arp() =>
        [
            new(IPAddress.Parse("192.0.2.1"), [0x00, 0x00, 0x0C, 0x12, 0x34, 0x01], 3, "Wi-Fi"),
            new(IPAddress.Parse("192.0.2.5"), [0x00, 0x80, 0x77, 0x12, 0x34, 0x05], 3, "Wi-Fi"),
            new(IPAddress.Parse("192.0.2.57"), [0x00, 0x0C, 0x29, 0x12, 0x34, 0x57], 3, "Wi-Fi"),
            new(IPAddress.Parse("192.0.2.255"), [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF], 4, "Wi-Fi"),
        ];

        public IReadOnlyList<LinhaRota> Rotas() =>
        [
            new(IPAddress.Any, IPAddress.Any, IPAddress.Parse("192.0.2.1"), "Wi-Fi", 35),
            new(IPAddress.Parse("192.0.2.0"), IPAddress.Parse("255.255.255.0"), IPAddress.Any, "Wi-Fi", 291),
            new(IPAddress.Parse("198.51.100.0"), IPAddress.Parse("255.255.255.0"), IPAddress.Any, "Ethernet", 281),
        ];

        public IReadOnlyList<LinhaConexao> Conexoes() =>
        [
            new("TCP", IPAddress.Any, 135, null, 0, 2, 1044, "svchost"),
            new("TCP", IPAddress.Any, 445, null, 0, 2, 4, "System"),
            new("TCP", IPAddress.Parse("192.0.2.23"), 50412, IPAddress.Parse("192.0.2.57"), 445, 5, 4, "System"),
            new("TCP", IPAddress.Parse("192.0.2.23"), 50433, IPAddress.Parse("203.0.113.10"), 443, 5, 7310, "navegador"),
            new("UDP", IPAddress.Any, 5353, null, 0, 0, 2210, "svchost"),
        ];
    }



    /// <summary>Máquina de exemplo, com Wi-Fi e DHCP.</summary>
    public static FontesMaquina Fontes()
    {
        var agora = DateTimeOffset.Now;
        return new FontesMaquina
        {
            Computador = () => new DadosComputador("NOTEBOOK-TECNICO", "ESCRITORIO", false, "tecnico"),
            Placa = i => new DadosPlaca
            {
                Mtu = 1500,
                Ipv6 = [IPAddress.Parse("2001:db8:0:1::23"), IPAddress.Parse("fe80::23")],
                SufixoDns = "escritorio.example",
                DhcpLigado = true,
                ServidorDhcp = i.Gateway,
                ConcessaoObtida = agora.AddHours(-3),
                ConcessaoValidade = agora.AddHours(21),
            },
            Wifi = new WifiDemonstracao(),
        };
    }

    /// <summary>Hosts de exemplo, com os textos mais longos de cada coluna da tabela.</summary>
    public static IReadOnlyList<HostEncontrado> Hosts(InterfaceRede i)
    {
        var rede = i.SubRede.Rede.GetAddressBytes();
        IPAddress Ip(byte final) => new([rede[0], rede[1], rede[2], final]);
        var oui = TabelaOui.Embutida;
        HostEncontrado Host(byte final, string mac, long? ping, Action<HostEncontrado>? ajuste = null)
        {
            var bytes = Convert.FromHexString(mac.Replace(":", string.Empty));
            var h = new HostEncontrado
            {
                Ip = Ip(final),
                Mac = bytes,
                Fabricante = oui.DescreverFabricante(bytes),
                RespondeuPing = ping.HasValue,
                RespondeuArp = true,
                TempoPingMs = ping,
                Ttl = ping.HasValue ? 64 : null,
            };
            ajuste?.Invoke(h);
            return h;
        }

        return
        [
            Host(1, "00:00:0C:12:34:01", 1, h => { h.EhGateway = true; h.NomeDns = "roteador.escritorio.example"; }),
            Host(5, "00:80:77:12:34:05", 3, h => h.NomeNetBios = "IMPRESSORA-RECEPCAO"),
            Host(12, "B8:27:EB:12:34:12", 4, h => h.NomeMdns = "painel-de-senhas.local"),
            Host(23, "02:00:00:00:00:23", 0, h => { h.EhEsteComputador = true; h.NomeDns = "notebook-tecnico.escritorio.example"; }),
            Host(31, "02:1A:2B:3C:4D:31", 38, h => h.NomeMdns = "celular-exemplo.local"),
            Host(40, "00:17:88:12:34:40", 12),
            Host(57, "00:0C:29:12:34:57", 1, h => { h.NomeDns = "servidor-de-arquivos.escritorio.example"; h.NomeNetBios = "SERVIDOR"; }),
            Host(88, "00:26:AB:12:34:88", null),
            Host(101, "F0:18:98:12:34:01", 7, h => h.NomeMdns = "sala-de-reuniao.local"),
            Host(142, "3C:5A:B4:12:34:42", 21),
        ];
    }

    /// <summary>Portas abertas de exemplo, pelo último número do IP dos <see cref="Hosts"/>.</summary>
    private static readonly Dictionary<byte, int[]> _portasExemplo = new()
    {
        [1] = [53, 80, 443],
        [5] = [80, 443, 515, 631, 9100],
        [12] = [22, 80],
        [31] = [8080],
        [40] = [80, 443],
        [57] = [135, 139, 445, 3389],
        [101] = [5000],
        [142] = [554, 8000],
    };

    /// <summary>
    /// Varredura de mentira: anda pelos 254 endereços em uns 4 segundos e relata os hosts de
    /// exemplo. Com as portas ligadas, preenche as portas de exemplo, com a mesma regra de quem
    /// fica de fora que a varredura de verdade usa.
    /// </summary>
    private static async Task<ResultadoVarredura> VarrerAsync(InterfaceRede i, OpcoesVarredura opcoes, IProgress<ProgressoVarredura> progresso, CancellationToken cancelamento)
    {
        var inicio = DateTimeOffset.Now;
        var subRede = Varredor.SubRedeAVarrer(i, new OpcoesVarredura().PrefixoMinimo, out _);
        var resultado = new ResultadoVarredura { Interface = i, SubRedeVarrida = subRede, Inicio = inicio, NomeComputador = "NOTEBOOK-TECNICO" };
        var hosts = Hosts(i).ToDictionary(h => h.Ip.GetAddressBytes()[3]);
        var total = (int)subRede.QuantidadeHosts;
        for (var n = 1; n <= total; n++)
        {
            if (cancelamento.IsCancellationRequested)
            {
                resultado.Cancelada = true;
                break;
            }

            hosts.TryGetValue((byte)n, out var host);
            if (host != null)
            {
                resultado.Hosts.Add(host);
            }

            progresso.Report(new ProgressoVarredura(Varredor.EtapaDescoberta, n, total, host));
            await Task.Delay(15, CancellationToken.None).ConfigureAwait(true);
        }

        if (!resultado.Cancelada && opcoes.OlharPortas)
        {
            resultado.PortasVerificadas = opcoes.Portas;
            var alvos = EtapaPortas.Escolher(resultado.Hosts, opcoes.PortasEmMacAleatorio);
            for (var n = 0; n < alvos.Count; n++)
            {
                var h = alvos[n];
                h.PortasAbertas = _portasExemplo.TryGetValue(h.Ip.GetAddressBytes()[3], out var p) ? p.Where(opcoes.Portas.Contains).ToList() : [];
                h.PortasVerificadas = true;
                progresso.Report(new ProgressoVarredura(EtapaPortas.Nome, n + 1, alvos.Count, h));
                await Task.Delay(120, CancellationToken.None).ConfigureAwait(true);
            }
        }

        resultado.Fim = DateTimeOffset.Now;
        return resultado;
    }

    private sealed class WifiDemonstracao : IFonteWifi
    {
        public DadosWifi? Ler(string idInterface) =>
            new() { Ssid = "Rede Exemplo", Canal = 36, FrequenciaKhz = 5_180_000, Sinal = 78 };
    }

    /// <summary>Não sai para a internet: responde um endereço reservado para documentação.</summary>
    private sealed class IpPublicoDemonstracao : IConsultaIpPublico
    {
        public string Endereco => "demonstração, sem consulta de verdade";

        public async Task<IPAddress> ConsultarAsync(CancellationToken cancelamento)
        {
            await Task.Delay(800, cancelamento).ConfigureAwait(true);
            return IPAddress.Parse("203.0.113.45");
        }
    }
}

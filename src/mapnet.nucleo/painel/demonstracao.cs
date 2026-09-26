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

        return new DependenciasPainel
        {
            ListarInterfaces = () => [wifi, cabo],
            Varrer = VarrerAsync,
            SalvarEm = (r, caminho) => RelatorioHtml.SalvarAsync(r, caminho),
            // Documentos Públicos: o caminho não traz o nome do usuário, que apareceria nas imagens.
            PastaRelatorios = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "MapNet - MT", "demonstracao"),
            LerMaquina = i => Task.FromResult(LeitorMaquina.Ler(i, Fontes(), new OpcoesVarredura().PrefixoMinimo)),
            IpPublico = new IpPublicoDemonstracao(),
        };
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

    /// <summary>Varredura de mentira: anda pelos 254 endereços em uns 4 segundos e relata os hosts de exemplo.</summary>
    private static async Task<ResultadoVarredura> VarrerAsync(InterfaceRede i, IProgress<ProgressoVarredura> progresso, CancellationToken cancelamento)
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

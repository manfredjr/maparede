using System.Net;
using MapNet.Nucleo;

namespace MapNet.Testes;

/// <summary>
/// Coluna "Minha máquina" da fatia 3: montagem com dados simulados, DHCP, Wi-Fi e IP público.
/// Nenhum teste chama o Windows nem a rede.
/// </summary>
public class MaquinaTestes
{
    private static readonly DateTimeOffset _agora = new(2026, 9, 26, 10, 0, 0, TimeSpan.FromHours(-3));

    [Fact]
    public void Monta_os_grupos_com_dados_simulados()
    {
        var info = LeitorMaquina.Ler(Interface(TipoInterface.Cabo), Fontes(), 22);

        var grupos = info.Grupos(_agora);
        var itens = Itens(grupos);

        Assert.Equal(["Computador", "Placa", "Endereços", "DHCP"], grupos.Select(g => g.Titulo));
        Assert.Equal("NOTEBOOK-MT", itens["Computador/Nome"]);
        Assert.Equal("ESCRITORIO", itens["Computador/Grupo de trabalho"]);
        Assert.Equal("tecnico", itens["Computador/Usuário"]);
        Assert.Equal("1 Gbps", itens["Placa/Velocidade"]);
        Assert.Equal("1500", itens["Placa/MTU"]);
        Assert.Equal("192.0.2.10/24", itens["Endereços/IPv4"]);
        Assert.Equal("2001:db8::10", itens["Endereços/IPv6"]);
        Assert.Equal("fe80::10", itens["Endereços/IPv6 de link local"]);
        Assert.Equal("escritorio.example", itens["Endereços/Sufixo DNS"]);
        Assert.Equal("192.0.2.0/24 (254 endereços)", itens["Endereços/Sub-rede a varrer"]);
        Assert.Equal("pelo DHCP", itens["DHCP/Endereço"]);
        Assert.Equal("192.0.2.1", itens["DHCP/Servidor DHCP"]);
    }

    [Fact]
    public void Dominio_troca_o_rotulo()
    {
        var fontes = Fontes(computador: new DadosComputador("PC-01", "empresa.example", true, "tecnico"));

        var itens = Itens(LeitorMaquina.Ler(Interface(TipoInterface.Cabo), fontes, 22).Grupos(_agora));

        Assert.Equal("empresa.example", itens["Computador/Domínio"]);
    }

    [Fact]
    public void Informacao_que_nao_vem_mostra_nao_informado()
    {
        var fontes = new FontesMaquina { Computador = () => null, Placa = _ => null };

        var itens = Itens(LeitorMaquina.Ler(Interface(TipoInterface.Cabo, velocidade: 0), fontes, 22).Grupos(_agora));

        Assert.Equal("não informado", itens["Computador/Nome"]);
        Assert.Equal("não informado", itens["Placa/MTU"]);
        Assert.Equal("não informado", itens["Placa/Velocidade"]);
        Assert.Equal("nenhum", itens["Endereços/IPv6"]);
        Assert.Equal("não informado", itens["DHCP/Endereço"]);
        Assert.Equal("não informado", itens["DHCP/Concessão"]);
    }

    [Fact]
    public void Leitura_que_falha_nao_derruba_as_outras()
    {
        var fontes = new FontesMaquina
        {
            Computador = () => throw new InvalidOperationException("sem acesso"),
            Placa = _ => throw new InvalidOperationException("placa sumiu"),
            Wifi = new WifiSimulado(() => throw new InvalidOperationException("wlan desligado")),
        };

        var info = LeitorMaquina.Ler(Interface(TipoInterface.WiFi), fontes, 22);
        var itens = Itens(info.Grupos(_agora));

        Assert.Equal("não informado", itens["Computador/Nome"]);
        Assert.Equal("192.0.2.10/24", itens["Endereços/IPv4"]);
        Assert.Equal("não informado", itens["Wi-Fi/Rede"]);
    }

    [Fact]
    public void Wifi_so_aparece_para_placa_wifi()
    {
        var chamadas = 0;
        var fontes = Fontes(wifi: new WifiSimulado(() =>
        {
            chamadas++;
            return new DadosWifi { Ssid = "Rede Exemplo", Canal = 36, FrequenciaKhz = 5_180_000, Sinal = 72 };
        }));

        var cabo = LeitorMaquina.Ler(Interface(TipoInterface.Cabo), fontes, 22).Grupos(_agora);
        Assert.DoesNotContain(cabo, g => g.Titulo == "Wi-Fi");
        Assert.Equal(0, chamadas);

        var itens = Itens(LeitorMaquina.Ler(Interface(TipoInterface.WiFi), fontes, 22).Grupos(_agora));
        Assert.Equal("Rede Exemplo", itens["Wi-Fi/Rede"]);
        Assert.Equal("5 GHz", itens["Wi-Fi/Banda"]);
        Assert.Equal("36", itens["Wi-Fi/Canal"]);
        Assert.Equal("72% (bom)", itens["Wi-Fi/Sinal"]);
    }

    [Fact]
    public void Localizacao_desligada_vira_mensagem_e_nao_erro()
    {
        var fontes = Fontes(wifi: new WifiSimulado(() => new DadosWifi { LocalizacaoNegada = true, Canal = 6 }));

        var itens = Itens(LeitorMaquina.Ler(Interface(TipoInterface.WiFi), fontes, 22).Grupos(_agora));

        Assert.Equal("ligue a Localização do Windows para ver o nome da rede", itens["Wi-Fi/Rede"]);
        Assert.Equal("2,4 GHz", itens["Wi-Fi/Banda"]);
    }

    [Fact]
    public void Ip_publico_so_entra_quando_foi_consultado()
    {
        var info = LeitorMaquina.Ler(Interface(TipoInterface.Cabo), Fontes(), 22);

        Assert.DoesNotContain(info.Grupos(_agora), g => g.Titulo == "IP público");
        Assert.Equal("203.0.113.7", Itens(info.Grupos(_agora, "203.0.113.7"))["IP público/IP público"]);
    }

    [Theory]
    [InlineData(false, null, null, "IP fixo")]
    [InlineData(null, null, null, "não informado")]
    [InlineData(true, null, null, "não informado")]
    [InlineData(true, "2026-09-26T08:00:00-03:00", "2026-09-27T08:00:00-03:00", "de 26/09 08:00 até 27/09 08:00")]
    [InlineData(true, null, "2026-09-26T14:02:00-03:00", "até 26/09 14:02")]
    [InlineData(true, "2026-09-25T08:00:00-03:00", "2026-09-26T08:00:00-03:00", "vencida em 26/09 08:00")]
    public void Texto_da_validade_do_dhcp(bool? ligado, string? obtida, string? validade, string esperado)
    {
        var texto = Dhcp.TextoValidade(ligado, Data(obtida), Data(validade), _agora);

        Assert.Equal(esperado, texto);
    }

    [Fact]
    public void Validade_do_dhcp_sai_no_fuso_de_agora()
    {
        // 17:00 em UTC são 14:00 no fuso -3.
        var validade = new DateTimeOffset(2026, 9, 26, 17, 0, 0, TimeSpan.Zero);

        Assert.Equal("até 26/09 14:00", Dhcp.TextoValidade(true, null, validade, _agora));
    }

    [Theory]
    [InlineData(0L, false)]
    [InlineData(-5L, false)]
    [InlineData(1790395200L, true)]
    public void Time_t_do_windows(long segundos, bool temData)
    {
        Assert.Equal(temData, Dhcp.DeTimeT(segundos).HasValue);
    }

    [Theory]
    [InlineData(1, null, "2,4 GHz")]
    [InlineData(14, null, "2,4 GHz")]
    [InlineData(36, null, "5 GHz")]
    [InlineData(177, null, "5 GHz")]
    [InlineData(20, null, null)]
    [InlineData(null, null, null)]
    [InlineData(37, 6_135_000L, "6 GHz")]
    [InlineData(5, 5_975_000L, "6 GHz")]
    [InlineData(40, 5_200_000L, "5 GHz")]
    [InlineData(6, 2_437_000L, "2,4 GHz")]
    public void Banda_pelo_canal_e_pela_frequencia(int? canal, long? khz, string? esperado)
    {
        Assert.Equal(esperado, Wifi.Banda(canal, khz));
    }

    [Theory]
    [InlineData(100, "100% (ótimo)")]
    [InlineData(120, "100% (ótimo)")]
    [InlineData(60, "60% (bom)")]
    [InlineData(45, "45% (regular)")]
    [InlineData(10, "10% (fraco)")]
    [InlineData(null, "não informado")]
    [InlineData(-1, "não informado")]
    public void Texto_do_sinal(int? qualidade, string esperado)
    {
        Assert.Equal(esperado, Wifi.TextoSinal(qualidade));
    }

    [Theory]
    [InlineData("fl=123\nh=1.1.1.1\nip=203.0.113.7\nts=1790395200.1\n", "203.0.113.7")]
    [InlineData("ip=2001:db8::7\n", "2001:db8::7")]
    [InlineData("ip=203.0.113.7\r\nts=1", "203.0.113.7")]
    [InlineData("h=1.1.1.1\nts=1\n", null)]
    [InlineData("ip=\n", null)]
    [InlineData("ip=1\n", null)]
    [InlineData("ip=203.0.113.7<script>\n", null)]
    [InlineData("<html>erro 502</html>", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Resposta_do_cloudflare(string? resposta, string? esperado)
    {
        Assert.Equal(esperado, IpPublicoCloudflare.Interpretar(resposta)?.ToString());
    }

    [Fact]
    public void Consulta_do_ip_publico_usa_o_endereco_aprovado_e_tempo_limite_de_5_segundos()
    {
        Assert.Equal("https://1.1.1.1/cdn-cgi/trace", IpPublicoCloudflare.EnderecoTrace);
        Assert.Equal(TimeSpan.FromSeconds(5), IpPublicoCloudflare.TempoLimite);
    }

    [Theory]
    [InlineData(1_000_000_000L, "1 Gbps")]
    [InlineData(2_500_000_000L, "2,5 Gbps")]
    [InlineData(866_700_000L, "866 Mbps")]
    [InlineData(0L, "não informado")]
    public void Texto_da_velocidade(long bps, string esperado)
    {
        Assert.Equal(esperado, InformacoesMaquina.TextoVelocidade(bps));
    }

    [Fact]
    public void Textos_da_maquina_nao_tem_caractere_proibido()
    {
        var fontes = Fontes(wifi: new WifiSimulado(() => new DadosWifi { LocalizacaoNegada = true }));
        var grupos = LeitorMaquina.Ler(Interface(TipoInterface.WiFi), fontes, 22).Grupos(_agora, "não foi possível consultar");

        var textos = grupos.SelectMany(g => g.Itens.Select(i => g.Titulo + i.Rotulo + i.Valor))
            .Append(Dhcp.TextoValidade(false, null, null, _agora))
            .Append(Wifi.TextoSinal(50));

        Assert.Empty(textos.SelectMany(CaracteresProibidosTestes.Proibidos));
    }

    internal static Dictionary<string, string> Itens(IEnumerable<GrupoMinhaMaquina> grupos) =>
        grupos.SelectMany(g => g.Itens.Select(i => (Chave: $"{g.Titulo}/{i.Rotulo}", i.Valor)))
            .ToDictionary(p => p.Chave, p => p.Valor);

    internal static FontesMaquina Fontes(DadosComputador? computador = null, IFonteWifi? wifi = null) => new()
    {
        Computador = () => computador ?? new DadosComputador("NOTEBOOK-MT", "ESCRITORIO", false, "tecnico"),
        Placa = _ => new DadosPlaca
        {
            Mtu = 1500,
            Ipv6 = [IPAddress.Parse("2001:db8::10"), IPAddress.Parse("fe80::10%7")],
            SufixoDns = "escritorio.example",
            DhcpLigado = true,
            ServidorDhcp = IPAddress.Parse("192.0.2.1"),
            ConcessaoObtida = _agora.AddHours(-2),
            ConcessaoValidade = _agora.AddHours(6),
        },
        Wifi = wifi,
    };

    internal static InterfaceRede Interface(TipoInterface tipo, long velocidade = 1_000_000_000) => new()
    {
        Id = "{00000000-0000-0000-0000-000000000001}",
        Nome = tipo == TipoInterface.WiFi ? "Wi-Fi" : "Ethernet",
        Descricao = "Placa de teste",
        Tipo = tipo,
        Mac = [0x02, 0x00, 0x00, 0x00, 0x00, 0x10],
        Ip = IPAddress.Parse("192.0.2.10"),
        Prefixo = 24,
        Gateway = IPAddress.Parse("192.0.2.1"),
        Dns = [IPAddress.Parse("192.0.2.1")],
        VelocidadeBps = velocidade,
    };

    private static DateTimeOffset? Data(string? texto) => texto is null ? null : DateTimeOffset.Parse(texto, System.Globalization.CultureInfo.InvariantCulture);

    private sealed class WifiSimulado(Func<DadosWifi?> leitura) : IFonteWifi
    {
        public DadosWifi? Ler(string idInterface) => leitura();
    }
}

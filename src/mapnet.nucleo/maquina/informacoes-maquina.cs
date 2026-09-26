using System.Globalization;
using System.Net;

namespace MapNet.Nucleo;

/// <summary>Identificação do computador: nome, grupo de trabalho ou domínio e usuário.</summary>
public sealed record DadosComputador(string? Nome, string? Grupo, bool EhDominio, string? Usuario);

/// <summary>O que a placa escolhida mostra além do que a <see cref="InterfaceRede"/> já traz.</summary>
public sealed record DadosPlaca
{
    public int? Mtu { get; init; }

    public IReadOnlyList<IPAddress> Ipv6 { get; init; } = [];

    public string? SufixoDns { get; init; }

    /// <summary>Null quando o Windows não informou.</summary>
    public bool? DhcpLigado { get; init; }

    public IPAddress? ServidorDhcp { get; init; }

    public DateTimeOffset? ConcessaoObtida { get; init; }

    public DateTimeOffset? ConcessaoValidade { get; init; }
}

/// <summary>Conexão Wi-Fi da placa escolhida.</summary>
public sealed record DadosWifi
{
    public string? Ssid { get; init; }

    public int? Canal { get; init; }

    /// <summary>Frequência central em kHz, quando o Windows informa. Separa 2,4, 5 e 6 GHz.</summary>
    public long? FrequenciaKhz { get; init; }

    /// <summary>Qualidade do sinal de 0 a 100, como o Windows mede.</summary>
    public int? Sinal { get; init; }

    /// <summary>O Windows negou o nome da rede porque a Localização está desligada.</summary>
    public bool LocalizacaoNegada { get; init; }
}

/// <summary>Tudo o que a coluna "Minha máquina" mostra, sem nenhum tipo do Windows.</summary>
public sealed class InformacoesMaquina
{
    public const string NaoInformado = "não informado";

    public required InterfaceRede Interface { get; init; }

    public DadosComputador? Computador { get; init; }

    public DadosPlaca? Placa { get; init; }

    /// <summary>Só para placa Wi-Fi. Null quando não é Wi-Fi ou a leitura falhou.</summary>
    public DadosWifi? Wifi { get; init; }

    /// <summary>Sub-rede que a varredura vai usar, já com o limite de tamanho aplicado.</summary>
    public SubRede? SubRedeAVarrer { get; init; }

    /// <summary>Linhas agrupadas para a tela e para o relatório. O IP público só entra quando foi consultado.</summary>
    public IReadOnlyList<GrupoMinhaMaquina> Grupos(DateTimeOffset agora, string? ipPublico = null)
    {
        var i = Interface;
        var p = Placa;
        var grupos = new List<GrupoMinhaMaquina>();

        var c = Computador;
        grupos.Add(new GrupoMinhaMaquina("Computador",
        [
            new ItemMinhaMaquina("Nome", Texto(c?.Nome)),
            new ItemMinhaMaquina(c?.EhDominio == true ? "Domínio" : "Grupo de trabalho", Texto(c?.Grupo)),
            new ItemMinhaMaquina("Usuário", Texto(c?.Usuario)),
        ]));

        grupos.Add(new GrupoMinhaMaquina("Placa",
        [
            new ItemMinhaMaquina("Nome", i.Descricao.Length > 0 ? i.Descricao : i.Nome),
            new ItemMinhaMaquina("Tipo", i.TipoTexto),
            new ItemMinhaMaquina("MAC", i.Mac.Length > 0 ? EnderecoMac.Formatar(i.Mac) : NaoInformado),
            new ItemMinhaMaquina("Velocidade", TextoVelocidade(i.VelocidadeBps)),
            new ItemMinhaMaquina("MTU", p?.Mtu is > 0 ? p.Mtu.Value.ToString(CultureInfo.InvariantCulture) : NaoInformado),
        ]));

        var ipv6 = p?.Ipv6 ?? [];
        var globais = ipv6.Where(a => !a.IsIPv6LinkLocal).ToList();
        var locais = ipv6.Where(a => a.IsIPv6LinkLocal).ToList();
        var enderecos = new List<ItemMinhaMaquina>
        {
            new("IPv4", $"{i.Ip}/{i.Prefixo}"),
            new("Máscara", i.Mascara.ToString()),
            new("IPv6", globais.Count > 0 ? string.Join(", ", globais.Select(SemEscopo)) : "nenhum"),
            new("IPv6 de link local", locais.Count > 0 ? string.Join(", ", locais.Select(SemEscopo)) : "nenhum"),
            new("Gateway", i.Gateway?.ToString() ?? "nenhum"),
            new("DNS", i.Dns.Count > 0 ? string.Join(", ", i.Dns) : "nenhum"),
            new("Sufixo DNS", string.IsNullOrWhiteSpace(p?.SufixoDns) ? "nenhum" : p.SufixoDns),
        };
        if (SubRedeAVarrer is { } s)
        {
            enderecos.Add(new ItemMinhaMaquina("Sub-rede a varrer", $"{s} ({s.QuantidadeHosts} endereços)"));
        }

        grupos.Add(new GrupoMinhaMaquina("Endereços", enderecos));

        grupos.Add(new GrupoMinhaMaquina("DHCP",
        [
            new ItemMinhaMaquina("Endereço", p?.DhcpLigado switch
            {
                true => "pelo DHCP",
                false => "IP fixo",
                null => NaoInformado,
            }),
            new ItemMinhaMaquina("Servidor DHCP", p?.DhcpLigado == false ? "não se aplica" : p?.ServidorDhcp?.ToString() ?? NaoInformado),
            new ItemMinhaMaquina("Concessão", Dhcp.TextoValidade(p?.DhcpLigado, p?.ConcessaoObtida, p?.ConcessaoValidade, agora)),
        ]));

        if (i.Tipo == TipoInterface.WiFi)
        {
            grupos.Add(new GrupoMinhaMaquina("Wi-Fi", ItensWifi(Wifi)));
        }

        if (ipPublico != null)
        {
            grupos.Add(new GrupoMinhaMaquina("IP público", [new ItemMinhaMaquina("IP público", ipPublico)]));
        }

        return grupos;
    }

    private static List<ItemMinhaMaquina> ItensWifi(DadosWifi? w)
    {
        if (w is null)
        {
            return [new ItemMinhaMaquina("Rede", NaoInformado)];
        }

        var rede = w.LocalizacaoNegada
            ? "ligue a Localização do Windows para ver o nome da rede"
            : Texto(w.Ssid);
        return
        [
            new ItemMinhaMaquina("Rede", rede),
            new ItemMinhaMaquina("Banda", MapNet.Nucleo.Wifi.Banda(w.Canal, w.FrequenciaKhz) ?? NaoInformado),
            new ItemMinhaMaquina("Canal", w.Canal is > 0 ? w.Canal.Value.ToString(CultureInfo.InvariantCulture) : NaoInformado),
            new ItemMinhaMaquina("Sinal", MapNet.Nucleo.Wifi.TextoSinal(w.Sinal)),
        ];
    }

    private static string Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? NaoInformado : valor.Trim();

    /// <summary>IPv6 sem o "%12" do escopo, que é número interno do Windows.</summary>
    private static string SemEscopo(IPAddress endereco)
    {
        var texto = endereco.ToString();
        var pos = texto.IndexOf('%');
        return pos < 0 ? texto : texto[..pos];
    }

    public static string TextoVelocidade(long bps) => bps switch
    {
        <= 0 => NaoInformado,
        >= 1_000_000_000 when bps % 1_000_000_000 == 0 => $"{bps / 1_000_000_000} Gbps",
        >= 1_000_000_000 => $"{(bps / 1e9).ToString("0.#", CultureInfo.GetCultureInfo("pt-BR"))} Gbps",
        >= 1_000_000 => $"{bps / 1_000_000} Mbps",
        _ => $"{bps / 1000} kbps",
    };
}

/// <summary>Um bloco da coluna "Minha máquina", com título pequeno.</summary>
public sealed record GrupoMinhaMaquina(string Titulo, IReadOnlyList<ItemMinhaMaquina> Itens);

/// <summary>Uma linha da coluna "Minha máquina": rótulo e valor.</summary>
public sealed record ItemMinhaMaquina(string Rotulo, string Valor);

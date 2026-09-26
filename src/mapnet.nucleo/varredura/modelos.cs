using System.Net;
using System.Reflection;

namespace MapNet.Nucleo;

/// <summary>Ajustes da varredura. Os valores padrão servem para rede de escritório.</summary>
public sealed class OpcoesVarredura
{
    /// <summary>Quanto esperar pela resposta de cada ping.</summary>
    public int TempoPingMs { get; set; } = 1000;

    /// <summary>Quantos endereços são sondados ao mesmo tempo.</summary>
    public int Paralelismo { get; set; } = 64;

    /// <summary>Quanto esperar por cada consulta de nome (DNS, NetBIOS, mDNS).</summary>
    public int TempoNomeMs { get; set; } = 1500;

    /// <summary>
    /// Maior sub-rede varrida inteira. Numa rede maior que isso (/16, por exemplo), varre só o
    /// bloco deste tamanho em volta do IP do computador. O /22 dá 1022 endereços.
    /// </summary>
    public int PrefixoMinimo { get; set; } = 22;

    /// <summary>Usa o ARP além do ping. Desligar só para teste.</summary>
    public bool UsarArp { get; set; } = true;

    /// <summary>Verifica as portas TCP dos hosts encontrados. Desligado por padrão: quem usa liga.</summary>
    public bool OlharPortas { get; set; }

    public IReadOnlyList<int> Portas { get; set; } = ListaPortas.Padrao;

    /// <summary>Inclui na verificação de portas os aparelhos com MAC aleatório, quase sempre pessoais.</summary>
    public bool PortasEmMacAleatorio { get; set; }

    /// <summary>Conexões TCP abertas ao mesmo tempo, somando todos os hosts.</summary>
    public int ConexoesPortas { get; set; } = 128;

    /// <summary>Conexões TCP ao mesmo tempo num só host.</summary>
    public int ConexoesPorHost { get; set; } = 8;

    public int TempoPortaMs { get; set; } = 800;
}

/// <summary>Andamento da varredura, para barra de progresso e mensagem na tela.</summary>
public sealed record ProgressoVarredura(string Etapa, int Concluidos, int Total, HostEncontrado? Host = null);

/// <summary>Tudo que se sabe de um host que respondeu.</summary>
public sealed class HostEncontrado
{
    public required IPAddress Ip { get; init; }

    public byte[]? Mac { get; set; }

    public string MacTexto => Mac is { Length: > 0 } ? EnderecoMac.Formatar(Mac) : string.Empty;

    public bool MacAleatorio => Mac is { Length: > 0 } && EnderecoMac.AdministradoLocalmente(Mac);

    public string Fabricante { get; set; } = string.Empty;

    public bool RespondeuPing { get; set; }

    public bool RespondeuArp { get; set; }

    public long? TempoPingMs { get; set; }

    /// <summary>TTL da resposta ao ping. Dá pista do sistema: 64 Linux, 128 Windows, 255 equipamento de rede.</summary>
    public int? Ttl { get; set; }

    public string? NomeDns { get; set; }

    public string? NomeNetBios { get; set; }

    public string? GrupoNetBios { get; set; }

    public string? NomeMdns { get; set; }

    public bool EhGateway { get; set; }

    public bool EhEsteComputador { get; set; }

    /// <summary>Portas TCP que aceitaram conexão, em ordem.</summary>
    public IReadOnlyList<int> PortasAbertas { get; set; } = [];

    public bool PortasVerificadas { get; set; }

    /// <summary>Por que as portas deste host não foram verificadas. Null quando foram, ou quando a etapa não rodou.</summary>
    public string? MotivoSemPortas { get; set; }

    /// <summary>Portas para a tabela: "22, 80, 443".</summary>
    public string PortasTexto => string.Join(", ", PortasAbertas);

    /// <summary>Melhor nome disponível, na ordem DNS reverso, NetBIOS, mDNS.</summary>
    public string Nome => NomeDns ?? NomeNetBios ?? NomeMdns ?? string.Empty;

    public string OrigemNome =>
        NomeDns != null ? "DNS reverso"
        : NomeNetBios != null ? "NetBIOS"
        : NomeMdns != null ? "mDNS"
        : string.Empty;

    public uint IpNumero => SubRede.ParaNumero(Ip);

    /// <summary>Marcas curtas para a coluna de observação.</summary>
    public IReadOnlyList<string> Marcas
    {
        get
        {
            var marcas = new List<string>();
            if (EhGateway)
            {
                marcas.Add("Gateway");
            }

            if (EhEsteComputador)
            {
                marcas.Add("Este computador");
            }

            if (MacAleatorio)
            {
                marcas.Add("MAC aleatório");
            }

            if (!RespondeuPing && RespondeuArp)
            {
                marcas.Add("Não responde a ping");
            }

            return marcas;
        }
    }
}

/// <summary>Resultado de uma varredura, completa ou cancelada.</summary>
public sealed class ResultadoVarredura
{
    public required InterfaceRede Interface { get; init; }

    public required SubRede SubRedeVarrida { get; init; }

    public DateTimeOffset Inicio { get; init; }

    public DateTimeOffset Fim { get; set; }

    public TimeSpan Duracao => Fim - Inicio;

    public List<HostEncontrado> Hosts { get; } = [];

    public List<string> Avisos { get; } = [];

    public bool Cancelada { get; set; }

    public string NomeComputador { get; init; } = Environment.MachineName;

    /// <summary>Dados da coluna "Minha máquina" para a seção do relatório. Null quando não foram lidos.</summary>
    public InformacoesMaquina? Maquina { get; set; }

    /// <summary>IP público, só quando o técnico consultou antes de gravar o relatório.</summary>
    public string? IpPublico { get; set; }

    public static string VersaoPrograma =>
        typeof(ResultadoVarredura).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "0.0.0";
}

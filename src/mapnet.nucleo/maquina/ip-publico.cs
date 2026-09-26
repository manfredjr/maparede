using System.Net;
using System.Net.Sockets;

namespace MapNet.Nucleo;

/// <summary>Consulta o IP público. Só roda quando o técnico clica no botão.</summary>
public interface IConsultaIpPublico
{
    /// <summary>Endereço de onde a consulta é feita, para o console dizer aonde o programa foi.</summary>
    string Endereco { get; }

    /// <summary>Devolve o IP público ou lança exceção com o motivo.</summary>
    Task<IPAddress> ConsultarAsync(CancellationToken cancelamento);
}

/// <summary>
/// IP público pelo trace do Cloudflare, que responde texto simples ("ip=203.0.113.7") e não pede
/// cadastro. Só roda com o clique do técnico.
/// </summary>
public sealed class IpPublicoCloudflare : IConsultaIpPublico
{
    public const string EnderecoTrace = "https://1.1.1.1/cdn-cgi/trace";

    public static readonly TimeSpan TempoLimite = TimeSpan.FromSeconds(5);

    public string Endereco => EnderecoTrace;

    public async Task<IPAddress> ConsultarAsync(CancellationToken cancelamento)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelamento);
        limite.CancelAfter(TempoLimite);
        using var cliente = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        cliente.DefaultRequestHeaders.UserAgent.ParseAdd($"MapNet-MT/{ResultadoVarredura.VersaoPrograma}");

        string resposta;
        try
        {
            resposta = await cliente.GetStringAsync(EnderecoTrace, limite.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancelamento.IsCancellationRequested)
        {
            throw new TimeoutException($"o serviço não respondeu em {TempoLimite.TotalSeconds:0} segundos");
        }

        return Interpretar(resposta)
            ?? throw new InvalidDataException("a resposta do serviço não trouxe o IP");
    }

    /// <summary>Acha a linha "ip=" da resposta. Qualquer outra coisa devolve null.</summary>
    public static IPAddress? Interpretar(string? resposta)
    {
        if (string.IsNullOrEmpty(resposta))
        {
            return null;
        }

        foreach (var linha in resposta.Split('\n'))
        {
            var texto = linha.Trim();
            if (!texto.StartsWith("ip=", StringComparison.Ordinal))
            {
                continue;
            }

            // O IPAddress aceita "1" como 0.0.0.1. Aqui só vale o IPv4 com quatro partes ou o IPv6.
            var valor = texto[3..];
            var formato = valor.Contains(':')
                ? valor.All(ch => Uri.IsHexDigit(ch) || ch is ':' or '.')
                : valor.Count(ch => ch == '.') == 3 && valor.All(ch => char.IsAsciiDigit(ch) || ch == '.');
            return formato
                && IPAddress.TryParse(valor, out var ip)
                && ip.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6
                ? ip
                : null;
        }

        return null;
    }
}

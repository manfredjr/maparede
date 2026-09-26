using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace MapNet.Nucleo;

/// <summary>O que o técnico preencheu na aba antes de rodar a ferramenta.</summary>
public sealed record ParametrosFerramenta(string Alvo, string Servidor, bool Continuo);

/// <summary>
/// Saída de uma ferramenta no console. <see cref="Linha"/> leva a hora na frente, para os
/// acontecimentos. <see cref="Texto"/> vai como está, para as linhas de tabela.
/// </summary>
public sealed record SaidaFerramenta(Action<string> Linha, Action<string> Texto);

/// <summary>Uma ferramenta de diagnóstico do console: ping, tracert, DNS, tabelas e ipconfig.</summary>
public interface IFerramenta
{
    string Titulo { get; }

    /// <summary>Dica do botão e da aba.</summary>
    string Descricao { get; }

    /// <summary>Pede um host (IP ou nome) no campo da aba.</summary>
    bool PedeAlvo { get; }

    /// <summary>Pede o servidor DNS no campo da aba.</summary>
    bool PedeServidor { get; }

    /// <summary>Tem a opção de rodar sem parar, até o técnico clicar em Parar.</summary>
    bool PodeContinuo { get; }

    Task ExecutarAsync(ParametrosFerramenta parametros, SaidaFerramenta saida, CancellationToken cancelamento);
}

/// <summary>Conferência e resolução do host digitado na aba.</summary>
public static partial class Alvo
{
    /// <summary>
    /// Aceita só IP ou nome de host (letras, números, hífen e ponto, até 253 caracteres). O
    /// texto nunca vai para linha de comando: serve só para achar o endereço.
    /// </summary>
    public static bool Valido(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        var t = texto.Trim();
        if (IPAddress.TryParse(t, out var ip))
        {
            return ip.AddressFamily == AddressFamily.InterNetwork && t.Count(c => c == '.') == 3
                || ip.AddressFamily == AddressFamily.InterNetworkV6;
        }

        return t.Length <= 253 && NomeDeHost().IsMatch(t);
    }

    /// <summary>IP digitado, ou o primeiro IPv4 do nome (o IPv6 só quando não há IPv4).</summary>
    public static async Task<IPAddress> ResolverAsync(string texto, CancellationToken cancelamento)
    {
        if (!Valido(texto))
        {
            throw new ArgumentException($"\"{texto}\" não é um IP nem um nome de host");
        }

        var t = texto.Trim();
        if (IPAddress.TryParse(t, out var ip))
        {
            return ip;
        }

        IPAddress[] enderecos;
        try
        {
            enderecos = await Dns.GetHostAddressesAsync(t, cancelamento).ConfigureAwait(false);
        }
        catch (SocketException)
        {
            throw new InvalidOperationException($"o nome {t} não foi encontrado no DNS");
        }

        return enderecos.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
            ?? enderecos.FirstOrDefault()
            ?? throw new InvalidOperationException($"o nome {t} não tem endereço no DNS");
    }

    [GeneratedRegex(@"^(?=.{1,253}$)([A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?)(\.[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?)*\.?$")]
    private static partial Regex NomeDeHost();
}

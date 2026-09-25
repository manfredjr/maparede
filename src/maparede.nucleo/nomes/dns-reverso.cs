using System.Net;
using System.Net.Sockets;

namespace MapaRede.Nucleo;

/// <summary>Nome do host pelo DNS reverso (registro PTR), usando o resolvedor do sistema.</summary>
public static class DnsReverso
{
    public static async Task<string?> ConsultarAsync(IPAddress ip, int tempoMs, CancellationToken cancelamento)
    {
        try
        {
            var entrada = await Dns.GetHostEntryAsync(ip.ToString(), cancelamento)
                .WaitAsync(TimeSpan.FromMilliseconds(tempoMs), cancelamento)
                .ConfigureAwait(false);
            var nome = entrada.HostName?.TrimEnd('.');

            // Sem PTR, alguns sistemas devolvem o próprio IP como nome.
            return string.IsNullOrWhiteSpace(nome) || IPAddress.TryParse(nome, out _) ? null : nome;
        }
        catch (Exception e) when (e is SocketException or TimeoutException or ArgumentException
            || (e is OperationCanceledException && !cancelamento.IsCancellationRequested))
        {
            return null;
        }
    }
}

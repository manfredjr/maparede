using System.Net;
using System.Net.Sockets;

namespace MapaRede.Nucleo;

/// <summary>Pergunta e resposta por UDP, com tempo limite, para NetBIOS e mDNS.</summary>
internal static class Udp
{
    /// <summary>
    /// Envia o pacote de uma porta qualquer e espera a primeira resposta vinda do IP de destino.
    /// Devolve null quando o tempo acaba ou o host recusa (porta fechada).
    /// </summary>
    public static async Task<byte[]?> PerguntarAsync(IPEndPoint destino, byte[] pacote, int tempoMs, CancellationToken cancelamento)
    {
        using var cliente = new UdpClient(AddressFamily.InterNetwork);
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelamento);
        limite.CancelAfter(tempoMs);
        try
        {
            await cliente.SendAsync(pacote, destino, limite.Token).ConfigureAwait(false);
            while (true)
            {
                var resposta = await cliente.ReceiveAsync(limite.Token).ConfigureAwait(false);
                if (resposta.RemoteEndPoint.Address.Equals(destino.Address))
                {
                    return resposta.Buffer;
                }
            }
        }
        catch (OperationCanceledException) when (!cancelamento.IsCancellationRequested)
        {
            return null;
        }
        catch (SocketException)
        {
            return null;
        }
    }
}

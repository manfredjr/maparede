using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace MapNet.Nucleo;

/// <summary>Conversa com os serviços. Os testes trocam por uma versão simulada.</summary>
public interface IIdentificador
{
    /// <summary>Identifica o serviço de uma porta aberta. Null quando ele não respondeu no tempo.</summary>
    Task<ServicoIdentificado?> IdentificarAsync(IPAddress ip, int porta, ProtocoloServico protocolo, int tempoMs, CancellationToken cancelamento);

    /// <summary>Manda um M-SEARCH pela interface de origem e junta as respostas que chegarem no tempo.</summary>
    Task<IReadOnlyList<RespostaSsdp>> BuscarUpnpAsync(IPAddress origem, int tempoMs, CancellationToken cancelamento);

    /// <summary>Lê a descrição UPnP do próprio equipamento, por http.</summary>
    Task<string?> LerDescricaoAsync(Uri endereco, int tempoMs, CancellationToken cancelamento);
}

/// <summary>
/// Identificação pela rede, sem administrador. HTTP e HTTPS mandam só um GET na raiz; SSH, FTP e
/// SMTP não recebem nada, o programa só lê o que o servidor diz ao conectar.
/// </summary>
public sealed class IdentificadorRede : IIdentificador
{
    public async Task<ServicoIdentificado?> IdentificarAsync(IPAddress ip, int porta, ProtocoloServico protocolo, int tempoMs, CancellationToken cancelamento)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelamento);
        limite.CancelAfter(tempoMs);
        try
        {
            using var cliente = new TcpClient(ip.AddressFamily) { NoDelay = true };
            await cliente.ConnectAsync(ip, porta, limite.Token).ConfigureAwait(false);
            var rede = cliente.GetStream();
            return protocolo switch
            {
                ProtocoloServico.Http => await HttpAsync(rede, ip, porta, "HTTP", null, limite.Token, cancelamento).ConfigureAwait(false),
                ProtocoloServico.Https => await HttpsAsync(rede, ip, porta, limite.Token, cancelamento).ConfigureAwait(false),
                _ => await BannerAsync(rede, porta, limite.Token).ConfigureAwait(false),
            };
        }
        catch (OperationCanceledException) when (!cancelamento.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception e) when (e is SocketException or IOException or AuthenticationException)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<RespostaSsdp>> BuscarUpnpAsync(IPAddress origem, int tempoMs, CancellationToken cancelamento)
    {
        var respostas = new List<RespostaSsdp>();
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelamento);
        limite.CancelAfter(tempoMs);
        try
        {
            using var cliente = new UdpClient(new IPEndPoint(origem, 0));
            await cliente.SendAsync(Ssdp.Pedido(), Ssdp.Grupo, limite.Token).ConfigureAwait(false);
            while (true)
            {
                var r = await cliente.ReceiveAsync(limite.Token).ConfigureAwait(false);
                if (Ssdp.Interpretar(r.RemoteEndPoint.Address, r.Buffer) is { } resposta)
                {
                    respostas.Add(resposta);
                }
            }
        }
        catch (OperationCanceledException) when (!cancelamento.IsCancellationRequested)
        {
            return respostas;
        }
        catch (SocketException)
        {
            return respostas;
        }
    }

    public async Task<string?> LerDescricaoAsync(Uri endereco, int tempoMs, CancellationToken cancelamento)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelamento);
        limite.CancelAfter(tempoMs);
        try
        {
            using var cliente = new TcpClient(AddressFamily.InterNetwork) { NoDelay = true };
            await cliente.ConnectAsync(endereco.Host, endereco.Port, limite.Token).ConfigureAwait(false);
            var rede = cliente.GetStream();
            await rede.WriteAsync(RespostaHttp.Pedido(endereco.Authority, endereco.PathAndQuery), limite.Token).ConfigureAwait(false);
            var dados = RespostaHttp.Interpretar(await LerAsync(rede, limite.Token, cancelamento, ateTitulo: false).ConfigureAwait(false));
            return dados.Codigo == 200 ? dados.Corpo : null;
        }
        catch (OperationCanceledException) when (!cancelamento.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception e) when (e is SocketException or IOException)
        {
            return null;
        }
    }

    private static async Task<ServicoIdentificado> HttpAsync(Stream rede, IPAddress ip, int porta, string protocolo, ServicoIdentificado? base_, CancellationToken cancelamento, CancellationToken usuario)
    {
        await rede.WriteAsync(RespostaHttp.Pedido(ip.ToString(), "/"), cancelamento).ConfigureAwait(false);
        var dados = RespostaHttp.Interpretar(await LerAsync(rede, cancelamento, usuario, ateTitulo: true).ConfigureAwait(false));
        var s = base_ ?? new ServicoIdentificado { Porta = porta, Protocolo = protocolo };
        s.CodigoHttp = dados.Codigo;
        s.Titulo = dados.Titulo;
        s.Servidor = dados.Servidor;
        s.Redireciona = dados.Codigo is >= 300 and < 400 ? dados.Localizacao : null;
        return s;
    }

    /// <summary>
    /// Começa o TLS aceitando qualquer certificado: equipamento de rede usa quase sempre um
    /// autoassinado ou vencido, e aqui o objetivo é ler o certificado, não confiar nele.
    /// </summary>
    private static async Task<ServicoIdentificado> HttpsAsync(Stream rede, IPAddress ip, int porta, CancellationToken cancelamento, CancellationToken usuario)
    {
        X509Certificate2? certificado = null;
        await using var tls = new SslStream(rede, leaveInnerStreamOpen: false, (_, cert, _, _) =>
        {
            if (cert != null)
            {
                certificado = new X509Certificate2(cert);
            }

            return true;
        });
        await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = ip.ToString() }, cancelamento).ConfigureAwait(false);
        var s = new ServicoIdentificado { Porta = porta, Protocolo = "HTTPS" };
        if (certificado != null)
        {
            s.CertificadoNome = TextoRede.Limpar(certificado.GetNameInfo(X509NameType.SimpleName, forIssuer: false));
            s.CertificadoEmissor = TextoRede.Limpar(certificado.GetNameInfo(X509NameType.SimpleName, forIssuer: true));
            s.CertificadoValidade = new DateTimeOffset(certificado.NotAfter);
            certificado.Dispose();
        }

        try
        {
            return await HttpAsync(tls, ip, porta, "HTTPS", s, cancelamento, usuario).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException || (e is OperationCanceledException && !usuario.IsCancellationRequested))
        {
            // O certificado já foi lido; a página pode ter fechado a conexão ou não respondido no tempo.
            return s;
        }
    }

    /// <summary>Só lê a primeira linha que o servidor manda ao conectar. Não envia nada.</summary>
    private static async Task<ServicoIdentificado> BannerAsync(Stream rede, int porta, CancellationToken cancelamento)
    {
        var buffer = new byte[512];
        var lidos = 0;
        while (lidos < buffer.Length)
        {
            var n = await rede.ReadAsync(buffer.AsMemory(lidos), cancelamento).ConfigureAwait(false);
            if (n == 0)
            {
                break;
            }

            lidos += n;
            if (Array.IndexOf(buffer, (byte)'\n', 0, lidos) >= 0)
            {
                break;
            }
        }

        var linha = Encoding.UTF8.GetString(buffer, 0, lidos).Split('\n')[0];
        return new ServicoIdentificado { Porta = porta, Protocolo = EtapaIdentificacao.NomeBanner(porta), Banner = TextoRede.Limpar(linha) };
    }

    /// <summary>
    /// Lê até o servidor fechar, até o limite de 64 KB ou, na página, até o fim do título. Se o
    /// tempo acabar no meio, fica o que já chegou. Só o cancelamento de quem usa interrompe.
    /// </summary>
    private static async Task<byte[]> LerAsync(Stream rede, CancellationToken limite, CancellationToken usuario, bool ateTitulo)
    {
        var buffer = new byte[RespostaHttp.MaximoBytes];
        var lidos = 0;
        try
        {
            while (lidos < buffer.Length)
            {
                var n = await rede.ReadAsync(buffer.AsMemory(lidos), limite).ConfigureAwait(false);
                if (n == 0)
                {
                    break;
                }

                lidos += n;
                if (ateTitulo && TemFimDoTitulo(buffer.AsSpan(0, lidos)))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (lidos > 0 && !usuario.IsCancellationRequested)
        {
            // O tempo acabou com a resposta pela metade: fica o que chegou.
        }
        catch (IOException) when (lidos > 0)
        {
            // Servidor fechou de um jeito brusco depois de mandar a resposta: fica o que chegou.
        }

        return buffer[..lidos];
    }

    private static bool TemFimDoTitulo(ReadOnlySpan<byte> dados) =>
        dados.IndexOf("</title"u8) >= 0 || dados.IndexOf("</TITLE"u8) >= 0 || dados.IndexOf("</Title"u8) >= 0;
}
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace MapNet.Nucleo;

/// <summary>Portas TCP comuns, com o serviço de cada uma, e a leitura da lista digitada.</summary>
public static class ListaPortas
{
    public const int MaximoDePortas = 100;

    private static readonly SortedDictionary<int, string> _servicos = new()
    {
        [21] = "FTP",
        [22] = "SSH",
        [23] = "Telnet",
        [25] = "SMTP",
        [53] = "DNS",
        [80] = "HTTP",
        [135] = "RPC do Windows",
        [139] = "NetBIOS",
        [443] = "HTTPS",
        [445] = "Compartilhamento do Windows",
        [515] = "Impressão LPD",
        [554] = "Câmera RTSP",
        [631] = "Impressão IPP",
        [1433] = "SQL Server",
        [3306] = "MySQL",
        [3389] = "Área de trabalho remota",
        [5000] = "NAS e UPnP",
        [5900] = "VNC",
        [8000] = "HTTP alternativo",
        [8080] = "HTTP alternativo",
        [8291] = "MikroTik Winbox",
        [8443] = "HTTPS alternativo",
        [9100] = "Impressão direta",
        [37777] = "NVR Dahua",
    };

    public static IReadOnlyList<int> Padrao { get; } = _servicos.Keys.ToList();

    public static string Servico(int porta) => _servicos.TryGetValue(porta, out var s) ? s : string.Empty;

    /// <summary>"80 (HTTP), 443 (HTTPS), 8123".</summary>
    public static string Texto(IEnumerable<int> portas) =>
        string.Join(", ", portas.Select(p => Servico(p) is { Length: > 0 } s ? $"{p} ({s})" : p.ToString(CultureInfo.InvariantCulture)));

    /// <summary>
    /// Lê "padrao" ou uma lista como "22,80,443". Porta fora de 1 a 65535, texto que não é
    /// número e lista com mais de <see cref="MaximoDePortas"/> portas são recusados.
    /// </summary>
    public static IReadOnlyList<int>? Interpretar(string? texto, out string? erro)
    {
        erro = null;
        var t = texto?.Trim() ?? string.Empty;
        if (t.Length == 0 || t.Equals("padrao", StringComparison.OrdinalIgnoreCase) || t.Equals("padrão", StringComparison.OrdinalIgnoreCase))
        {
            return Padrao;
        }

        var portas = new SortedSet<int>();
        foreach (var parte in t.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!int.TryParse(parte, NumberStyles.None, CultureInfo.InvariantCulture, out var p) || p < 1 || p > 65535)
            {
                erro = $"Porta inválida: {parte}. Use números de 1 a 65535, separados por vírgula.";
                return null;
            }

            portas.Add(p);
        }

        if (portas.Count > MaximoDePortas)
        {
            erro = $"São {portas.Count} portas. O limite é {MaximoDePortas}.";
            return null;
        }

        return portas.ToList();
    }
}

/// <summary>Diz se a porta aceita conexão. Os testes trocam por uma sonda simulada.</summary>
public interface ISondaPorta
{
    Task<bool> AbertaAsync(IPAddress ip, int porta, int tempoMs, CancellationToken cancelamento);
}

/// <summary>
/// Abre a conexão TCP e fecha em seguida, sem enviar nenhum dado. Porta que recusa ou não
/// responde no tempo limite conta como fechada. Não pede administrador.
/// </summary>
public sealed class SondaPortaTcp : ISondaPorta
{
    public async Task<bool> AbertaAsync(IPAddress ip, int porta, int tempoMs, CancellationToken cancelamento)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelamento);
        limite.CancelAfter(tempoMs);
        using var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(new IPEndPoint(ip, porta), limite.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (!cancelamento.IsCancellationRequested)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}

/// <summary>
/// Etapa de portas da varredura. Só verifica os hosts que a descoberta achou, nunca sai
/// deles. Por padrão, deixa de fora este computador e os aparelhos com MAC aleatório, que são
/// quase sempre pessoais: o MAC aleatório serve de sinal, não de prova.
/// </summary>
public static class EtapaPortas
{
    public const string Nome = "Verificação de portas";

    public const string MotivoMacAleatorio = "fora da verificação por padrão: MAC aleatório, aparelho provavelmente pessoal";

    public const string MotivoEsteComputador = "não se aplica (este computador)";

    /// <summary>Hosts que entram na verificação. Os outros recebem o motivo.</summary>
    public static IReadOnlyList<HostEncontrado> Escolher(IReadOnlyList<HostEncontrado> hosts, bool incluirMacAleatorio)
    {
        var escolhidos = new List<HostEncontrado>();
        foreach (var h in hosts)
        {
            if (h.EhEsteComputador)
            {
                h.MotivoSemPortas = MotivoEsteComputador;
            }
            else if (h.MacAleatorio && !incluirMacAleatorio)
            {
                h.MotivoSemPortas = MotivoMacAleatorio;
            }
            else
            {
                escolhidos.Add(h);
            }
        }

        return escolhidos;
    }

    public static async Task VerificarAsync(
        IReadOnlyList<HostEncontrado> hosts,
        OpcoesVarredura opcoes,
        ISondaPorta sonda,
        IProgress<ProgressoVarredura>? progresso,
        CancellationToken cancelamento)
    {
        var alvos = Escolher(hosts, opcoes.PortasEmMacAleatorio);
        var total = alvos.Count;
        var concluidos = 0;
        using var global = new SemaphoreSlim(Math.Max(1, opcoes.ConexoesPortas));
        progresso?.Report(new ProgressoVarredura(Nome, 0, total));

        var tarefas = alvos.Select(async host =>
        {
            using var porHost = new SemaphoreSlim(Math.Max(1, opcoes.ConexoesPorHost));
            var abertas = new ConcurrentBag<int>();
            var portas = opcoes.Portas.Select(async porta =>
            {
                await porHost.WaitAsync(cancelamento).ConfigureAwait(false);
                try
                {
                    await global.WaitAsync(cancelamento).ConfigureAwait(false);
                    try
                    {
                        if (await sonda.AbertaAsync(host.Ip, porta, opcoes.TempoPortaMs, cancelamento).ConfigureAwait(false))
                        {
                            abertas.Add(porta);
                        }
                    }
                    finally
                    {
                        global.Release();
                    }
                }
                finally
                {
                    porHost.Release();
                }
            });
            await Task.WhenAll(portas).ConfigureAwait(false);

            host.PortasAbertas = abertas.Order().ToList();
            host.PortasVerificadas = true;
            var feitos = Interlocked.Increment(ref concluidos);
            progresso?.Report(new ProgressoVarredura(Nome, feitos, total, host));
        });

        await Task.WhenAll(tarefas).ConfigureAwait(false);
    }
}

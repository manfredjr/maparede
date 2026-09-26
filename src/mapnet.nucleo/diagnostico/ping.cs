using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;

namespace MapNet.Nucleo;

public enum StatusPing
{
    Respondeu,
    TtlExpirou,
    TempoEsgotado,
    Inalcancavel,
    Falhou,
}

/// <summary>Resposta de um eco ICMP. <see cref="De"/> é quem respondeu, que no tracert é o salto.</summary>
public sealed record RespostaPing(StatusPing Status, IPAddress? De, long Ms, int? Ttl);

/// <summary>Manda um eco ICMP. Os testes trocam por um simulado.</summary>
public interface IPingador
{
    Task<RespostaPing> EnviarAsync(IPAddress destino, int ttl, int tempoMs, CancellationToken cancelamento);
}

/// <summary>O ping do .NET, que no Windows usa o IcmpSendEcho e não pede administrador.</summary>
public sealed class PingadorSistema : IPingador
{
    private static readonly byte[] _dados = new byte[32];

    public async Task<RespostaPing> EnviarAsync(IPAddress destino, int ttl, int tempoMs, CancellationToken cancelamento)
    {
        using var ping = new Ping();
        try
        {
            var r = await ping.SendPingAsync(destino, TimeSpan.FromMilliseconds(tempoMs), _dados, new PingOptions(ttl, true), cancelamento)
                .ConfigureAwait(false);
            var status = r.Status switch
            {
                IPStatus.Success => StatusPing.Respondeu,
                IPStatus.TtlExpired or IPStatus.TimeExceeded => StatusPing.TtlExpirou,
                IPStatus.TimedOut => StatusPing.TempoEsgotado,
                IPStatus.DestinationHostUnreachable or IPStatus.DestinationNetworkUnreachable
                    or IPStatus.DestinationUnreachable => StatusPing.Inalcancavel,
                _ => StatusPing.Falhou,
            };
            var de = r.Address is { } a && !a.Equals(IPAddress.Any) ? a : null;
            return new RespostaPing(status, de, r.RoundtripTime, r.Options?.Ttl);
        }
        catch (PingException)
        {
            return new RespostaPing(StatusPing.Falhou, null, 0, null);
        }
    }
}

/// <summary>Perda, mínimo, média e máximo de uma série de pings.</summary>
public sealed record EstatisticaPing(int Enviados, int Recebidos, long? Minimo, double? Media, long? Maximo)
{
    public int Perdidos => Enviados - Recebidos;

    public int PerdaPorcento => Enviados == 0 ? 0 : (int)Math.Round(100.0 * Perdidos / Enviados);

    /// <summary>Tempos em ms; null é pacote sem resposta.</summary>
    public static EstatisticaPing Calcular(IReadOnlyList<long?> tempos)
    {
        var ok = tempos.Where(t => t.HasValue).Select(t => t!.Value).ToList();
        return ok.Count == 0
            ? new EstatisticaPing(tempos.Count, 0, null, null, null)
            : new EstatisticaPing(tempos.Count, ok.Count, ok.Min(), ok.Average(), ok.Max());
    }

    public string Texto()
    {
        var pacotes = $"{Enviados} enviados, {Recebidos} recebidos, {Perdidos} perdidos ({PerdaPorcento}% de perda)";
        if (Minimo is null)
        {
            return pacotes;
        }

        var media = Media!.Value.ToString("0", CultureInfo.InvariantCulture);
        return $"{pacotes}. Tempo: mínimo {Minimo} ms, média {media} ms, máximo {Maximo} ms";
    }
}

/// <summary>Ping normal (4 pacotes) ou contínuo, até o técnico parar.</summary>
public sealed class FerramentaPing(IPingador pingador, Func<TimeSpan, CancellationToken, Task>? esperar = null) : IFerramenta
{
    public const int Pacotes = 4;
    public const int TempoMs = 1000;

    private readonly Func<TimeSpan, CancellationToken, Task> _esperar = esperar ?? Task.Delay;

    public string Titulo => "Ping";

    public string Descricao => "Manda eco ICMP ao host e mostra tempo, TTL, perda e média.";

    public bool PedeAlvo => true;

    public bool PedeServidor => false;

    public bool PodeContinuo => true;

    public async Task ExecutarAsync(ParametrosFerramenta p, SaidaFerramenta saida, CancellationToken cancelamento)
    {
        var destino = await Alvo.ResolverAsync(p.Alvo, cancelamento).ConfigureAwait(true);
        saida.Linha(p.Continuo
            ? $"Ping contínuo para {Nome(p.Alvo, destino)}. Clique em Parar para encerrar."
            : $"Ping para {Nome(p.Alvo, destino)}, {Pacotes} pacotes:");

        var tempos = new List<long?>();
        try
        {
            for (var n = 0; p.Continuo || n < Pacotes; n++)
            {
                if (n > 0)
                {
                    await _esperar(TimeSpan.FromSeconds(1), cancelamento).ConfigureAwait(true);
                }

                var r = await pingador.EnviarAsync(destino, 128, TempoMs, cancelamento).ConfigureAwait(true);
                tempos.Add(r.Status == StatusPing.Respondeu ? r.Ms : null);
                saida.Texto("  " + TextoResposta(r, destino));
            }
        }
        catch (OperationCanceledException)
        {
            saida.Linha("Ping interrompido.");
        }

        saida.Linha("Resumo: " + EstatisticaPing.Calcular(tempos).Texto() + ".");
    }

    public static string TextoResposta(RespostaPing r, IPAddress destino) => r.Status switch
    {
        StatusPing.Respondeu => $"Resposta de {r.De ?? destino}: {r.Ms} ms" + (r.Ttl is { } ttl ? $", TTL {ttl}" : ""),
        StatusPing.TempoEsgotado => "Sem resposta: tempo esgotado.",
        StatusPing.Inalcancavel => "Destino inalcançável" + (r.De is { } de ? $", informado por {de}." : "."),
        StatusPing.TtlExpirou => $"TTL expirou em {r.De}.",
        _ => "Falha no envio do ping.",
    };

    internal static string Nome(string alvo, IPAddress ip) =>
        alvo.Trim() == ip.ToString() ? ip.ToString() : $"{alvo.Trim()} ({ip})";
}

/// <summary>
/// Tracert com ping de TTL crescente: cada roteador do caminho devolve "TTL expirou" e se
/// revela. Um pacote por salto, até 30 saltos, com o nome pelo DNS reverso.
/// </summary>
public sealed class FerramentaTracert(IPingador pingador, Func<IPAddress, CancellationToken, Task<string?>>? nomeReverso = null) : IFerramenta
{
    public const int SaltosMaximos = 30;
    public const int TempoMs = 1500;

    private readonly Func<IPAddress, CancellationToken, Task<string?>> _nome = nomeReverso ?? NomePeloDns;

    public string Titulo => "Tracert";

    public string Descricao => "Mostra cada roteador do caminho até o host, com tempo e nome.";

    public bool PedeAlvo => true;

    public bool PedeServidor => false;

    public bool PodeContinuo => false;

    public async Task ExecutarAsync(ParametrosFerramenta p, SaidaFerramenta saida, CancellationToken cancelamento)
    {
        var destino = await Alvo.ResolverAsync(p.Alvo, cancelamento).ConfigureAwait(true);
        saida.Linha($"Rota até {FerramentaPing.Nome(p.Alvo, destino)}, no máximo {SaltosMaximos} saltos:");
        try
        {
            for (var ttl = 1; ttl <= SaltosMaximos; ttl++)
            {
                var r = await pingador.EnviarAsync(destino, ttl, TempoMs, cancelamento).ConfigureAwait(true);
                var nome = r.De is { } de ? await _nome(de, cancelamento).ConfigureAwait(true) : null;
                saida.Texto(TextoSalto(ttl, r, nome));
                if (r.Status == StatusPing.Respondeu)
                {
                    saida.Linha(ttl == 1 ? "Destino alcançado em 1 salto." : $"Destino alcançado em {ttl} saltos.");
                    return;
                }

                if (r.Status == StatusPing.Inalcancavel)
                {
                    saida.Linha("O caminho parou: destino inalcançável.");
                    return;
                }
            }

            saida.Linha($"O destino não respondeu em {SaltosMaximos} saltos.");
        }
        catch (OperationCanceledException)
        {
            saida.Linha("Tracert interrompido.");
        }
    }

    public static string TextoSalto(int ttl, RespostaPing r, string? nome)
    {
        var numero = ttl.ToString(CultureInfo.InvariantCulture).PadLeft(3);
        if (r.De is null || r.Status is StatusPing.TempoEsgotado or StatusPing.Falhou)
        {
            return $"{numero}      *    sem resposta";
        }

        var tempo = $"{r.Ms} ms".PadLeft(8);
        return $"{numero} {tempo}    {r.De}" + (string.IsNullOrEmpty(nome) ? "" : $"  {nome}");
    }

    private static async Task<string?> NomePeloDns(IPAddress ip, CancellationToken cancelamento)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelamento);
        limite.CancelAfter(1500);
        try
        {
            var h = await Dns.GetHostEntryAsync(ip.ToString(), limite.Token).ConfigureAwait(false);
            return h.HostName == ip.ToString() ? null : h.HostName;
        }
        catch (Exception e) when (e is System.Net.Sockets.SocketException or OperationCanceledException && !cancelamento.IsCancellationRequested)
        {
            return null;
        }
    }
}

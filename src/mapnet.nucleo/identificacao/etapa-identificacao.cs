using System.Net;

namespace MapNet.Nucleo;

/// <summary>
/// Etapa de identificação, depois das portas. Só conversa com as portas que a etapa de portas
/// achou abertas, e só nos hosts que ela verificou: nunca sonda porta nem host novo. O UPnP é uma
/// busca só na rede local, e das respostas ficam só as dos hosts verificados.
/// </summary>
public static class EtapaIdentificacao
{
    public const string Nome = "Identificação de serviços";

    /// <summary>Protocolo usado em cada porta. Porta fora desta lista não é identificada (Telnet fica de fora de propósito).</summary>
    public static ProtocoloServico? Protocolo(int porta) => porta switch
    {
        80 or 8000 or 8080 => ProtocoloServico.Http,
        443 or 8443 => ProtocoloServico.Https,
        21 or 22 or 25 => ProtocoloServico.Banner,
        _ => null,
    };

    public static string NomeBanner(int porta) => porta switch
    {
        21 => "FTP",
        22 => "SSH",
        25 => "SMTP",
        _ => "TCP",
    };

    public static async Task IdentificarAsync(
        IReadOnlyList<HostEncontrado> hosts,
        IPAddress origem,
        OpcoesVarredura opcoes,
        IIdentificador identificador,
        IProgress<ProgressoVarredura>? progresso,
        CancellationToken cancelamento)
    {
        var verificados = hosts.Where(h => h.PortasVerificadas).ToList();
        var tarefas = verificados
            .SelectMany(h => h.PortasAbertas.Where(p => Protocolo(p) != null).Select(p => (Host: h, Porta: p)))
            .ToList();
        var total = tarefas.Count + 1;
        var concluidos = 0;
        progresso?.Report(new ProgressoVarredura(Nome, 0, total));

        using var vagas = new SemaphoreSlim(Math.Max(1, opcoes.IdentificacoesSimultaneas));
        var servicos = tarefas.Select(async t =>
        {
            await vagas.WaitAsync(cancelamento).ConfigureAwait(false);
            try
            {
                var s = await identificador.IdentificarAsync(t.Host.Ip, t.Porta, Protocolo(t.Porta)!.Value, opcoes.TempoIdentificacaoMs, cancelamento)
                    .ConfigureAwait(false);
                if (s != null)
                {
                    lock (t.Host)
                    {
                        t.Host.Servicos = [.. t.Host.Servicos, s];
                    }
                }
            }
            finally
            {
                vagas.Release();
            }

            progresso?.Report(new ProgressoVarredura(Nome, Interlocked.Increment(ref concluidos), total, t.Host));
        });

        var upnp = UpnpAsync(verificados, origem, opcoes, identificador, cancelamento);
        await Task.WhenAll(servicos.Append(upnp)).ConfigureAwait(false);
        progresso?.Report(new ProgressoVarredura(Nome, Interlocked.Increment(ref concluidos), total));

        foreach (var h in verificados)
        {
            h.Servicos = [.. h.Servicos.OrderBy(s => s.Porta)];
            h.ServicosIdentificados = true;
        }
    }

    private static async Task UpnpAsync(
        IReadOnlyList<HostEncontrado> verificados,
        IPAddress origem,
        OpcoesVarredura opcoes,
        IIdentificador identificador,
        CancellationToken cancelamento)
    {
        var porIp = verificados.ToDictionary(h => h.IpNumero);
        var respostas = await identificador.BuscarUpnpAsync(origem, opcoes.TempoIdentificacaoMs, cancelamento).ConfigureAwait(false);

        // Um equipamento responde várias vezes, uma por serviço: fica a primeira de cada IP.
        var unicas = respostas
            .Where(r => r.Origem.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && porIp.ContainsKey(SubRede.ParaNumero(r.Origem)))
            .GroupBy(r => SubRede.ParaNumero(r.Origem))
            .Select(g => g.First());

        await Task.WhenAll(unicas.Select(async r =>
        {
            var host = porIp[SubRede.ParaNumero(r.Origem)];
            string? modelo = null;
            if (Ssdp.DescricaoDoProprio(r) is { } endereco)
            {
                modelo = Ssdp.Modelo(await identificador.LerDescricaoAsync(endereco, opcoes.TempoIdentificacaoMs, cancelamento).ConfigureAwait(false));
            }

            var s = new ServicoIdentificado { Porta = 0, Protocolo = "UPnP", Modelo = modelo, Servidor = r.Servidor };
            lock (host)
            {
                host.Servicos = [.. host.Servicos, s];
            }
        })).ConfigureAwait(false);
    }
}

using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;

namespace MapaRede.Nucleo;

/// <summary>
/// Faz a varredura em duas etapas. Na descoberta, cada endereço da sub-rede recebe um ping e
/// um pedido ARP; responde a qualquer um dos dois, o host existe. Na identificação, cada host
/// encontrado passa por DNS reverso, NetBIOS e mDNS, e o MAC vira fabricante pela tabela OUI.
/// </summary>
public sealed class Varredor
{
    public const string EtapaDescoberta = "Descoberta de hosts";
    public const string EtapaNomes = "Identificação de nomes";

    private readonly OpcoesVarredura _opcoes;
    private readonly TabelaOui _oui;
    private readonly ISondaArp? _arp;

    public Varredor(OpcoesVarredura? opcoes = null, TabelaOui? oui = null, ISondaArp? arp = null)
    {
        _opcoes = opcoes ?? new OpcoesVarredura();
        _oui = oui ?? TabelaOui.Embutida;
        _arp = _opcoes.UsarArp ? arp ?? SondaArp.Padrao() : null;
    }

    /// <summary>
    /// Sub-rede que de fato será varrida. Quando a da interface passa do limite, fica só o
    /// bloco em volta do IP do computador, e o aviso explica isso.
    /// </summary>
    public static SubRede SubRedeAVarrer(InterfaceRede interfaceRede, int prefixoMinimo, out string? aviso)
    {
        aviso = null;
        var subRede = interfaceRede.SubRede;
        if (subRede.Prefixo >= prefixoMinimo)
        {
            return subRede;
        }

        var reduzida = SubRede.Calcular(interfaceRede.Ip, prefixoMinimo);
        aviso = $"A sub-rede {subRede} tem {subRede.QuantidadeHosts} endereços. Foi varrido só o bloco {reduzida}, "
            + "em volta do IP deste computador. A varredura de faixa manual fica para uma próxima versão.";
        return reduzida;
    }

    public async Task<ResultadoVarredura> VarrerAsync(
        InterfaceRede interfaceRede,
        IProgress<ProgressoVarredura>? progresso = null,
        CancellationToken cancelamento = default)
    {
        var subRede = SubRedeAVarrer(interfaceRede, _opcoes.PrefixoMinimo, out var aviso);
        var resultado = new ResultadoVarredura
        {
            Interface = interfaceRede,
            SubRedeVarrida = subRede,
            Inicio = DateTimeOffset.Now,
        };
        if (aviso != null)
        {
            resultado.Avisos.Add(aviso);
        }

        if (_arp is null)
        {
            resultado.Avisos.Add("O ARP não está disponível neste sistema. Hosts que bloqueiam ping podem ter ficado de fora.");
        }

        GarantirThreads();
        var encontrados = new ConcurrentDictionary<uint, HostEncontrado>();
        try
        {
            await DescobrirAsync(interfaceRede, subRede, encontrados, progresso, cancelamento).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            resultado.Cancelada = true;
        }

        CompletarDados(interfaceRede, encontrados);
        resultado.Hosts.AddRange(encontrados.Values.OrderBy(h => h.IpNumero));

        if (!resultado.Cancelada)
        {
            try
            {
                await IdentificarNomesAsync(resultado.Hosts, progresso, cancelamento).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                resultado.Cancelada = true;
            }
        }

        if (resultado.Cancelada)
        {
            resultado.Avisos.Add("A varredura foi interrompida antes do fim. O relatório mostra só o que foi levantado até ali.");
        }

        resultado.Fim = DateTimeOffset.Now;
        return resultado;
    }

    private async Task DescobrirAsync(
        InterfaceRede interfaceRede,
        SubRede subRede,
        ConcurrentDictionary<uint, HostEncontrado> encontrados,
        IProgress<ProgressoVarredura>? progresso,
        CancellationToken cancelamento)
    {
        var enderecos = subRede.Hosts().ToList();
        var total = enderecos.Count;
        var concluidos = 0;
        using var vagas = new SemaphoreSlim(_opcoes.Paralelismo);
        progresso?.Report(new ProgressoVarredura(EtapaDescoberta, 0, total));

        var tarefas = enderecos.Select(async ip =>
        {
            await vagas.WaitAsync(cancelamento).ConfigureAwait(false);
            HostEncontrado? host = null;
            try
            {
                host = await SondarAsync(ip, interfaceRede.Ip, cancelamento).ConfigureAwait(false);
                if (host != null)
                {
                    encontrados[host.IpNumero] = host;
                }
            }
            finally
            {
                vagas.Release();
            }

            var feitos = Interlocked.Increment(ref concluidos);
            progresso?.Report(new ProgressoVarredura(EtapaDescoberta, feitos, total, host));
        });

        await Task.WhenAll(tarefas).ConfigureAwait(false);
    }

    /// <summary>Ping e ARP num endereço. Devolve null quando nenhum dos dois respondeu.</summary>
    private async Task<HostEncontrado?> SondarAsync(IPAddress ip, IPAddress origem, CancellationToken cancelamento)
    {
        var host = new HostEncontrado { Ip = ip };
        using (var ping = new Ping())
        {
            try
            {
                var resposta = await ping.SendPingAsync(ip, TimeSpan.FromMilliseconds(_opcoes.TempoPingMs), null, null, cancelamento)
                    .ConfigureAwait(false);
                if (resposta.Status == IPStatus.Success)
                {
                    host.RespondeuPing = true;
                    host.TempoPingMs = resposta.RoundtripTime;
                    host.Ttl = resposta.Options?.Ttl;
                }
            }
            catch (PingException)
            {
                // Endereço inalcançável: segue para o ARP.
            }
        }

        cancelamento.ThrowIfCancellationRequested();

        // Depois do ping, o MAC de quem respondeu já está no cache e o ARP volta na hora.
        if (_arp != null)
        {
            var mac = await Task.Run(() => _arp.Resolver(ip, origem), cancelamento).ConfigureAwait(false);
            if (mac != null)
            {
                host.Mac = mac;
                host.RespondeuArp = true;
            }
        }

        return host.RespondeuPing || host.RespondeuArp ? host : null;
    }

    private void CompletarDados(InterfaceRede interfaceRede, ConcurrentDictionary<uint, HostEncontrado> encontrados)
    {
        // O próprio computador entra sempre, com o MAC da interface.
        var proprio = encontrados.GetOrAdd(SubRede.ParaNumero(interfaceRede.Ip), _ => new HostEncontrado { Ip = interfaceRede.Ip });
        proprio.EhEsteComputador = true;
        if (EnderecoMac.Valido(interfaceRede.Mac))
        {
            proprio.Mac = interfaceRede.Mac;
        }

        foreach (var host in encontrados.Values)
        {
            host.EhGateway = interfaceRede.Gateway != null && host.Ip.Equals(interfaceRede.Gateway);
            host.Fabricante = _oui.DescreverFabricante(host.Mac);
        }
    }

    private async Task IdentificarNomesAsync(
        IReadOnlyList<HostEncontrado> hosts,
        IProgress<ProgressoVarredura>? progresso,
        CancellationToken cancelamento)
    {
        var total = hosts.Count;
        var concluidos = 0;
        using var vagas = new SemaphoreSlim(Math.Max(1, _opcoes.Paralelismo / 2));
        progresso?.Report(new ProgressoVarredura(EtapaNomes, 0, total));

        var tarefas = hosts.Select(async host =>
        {
            await vagas.WaitAsync(cancelamento).ConfigureAwait(false);
            try
            {
                var tempo = _opcoes.TempoNomeMs;
                var dns = DnsReverso.ConsultarAsync(host.Ip, tempo, cancelamento);
                var netBios = NetBios.ConsultarAsync(host.Ip, tempo, cancelamento);
                var mdns = Mdns.ConsultarAsync(host.Ip, tempo, cancelamento);
                await Task.WhenAll(dns, netBios, mdns).ConfigureAwait(false);

                host.NomeDns = await dns.ConfigureAwait(false);
                host.NomeMdns = await mdns.ConfigureAwait(false);
                var status = await netBios.ConfigureAwait(false);
                if (status != null)
                {
                    host.NomeNetBios = status.NomeComputador;
                    host.GrupoNetBios = status.Grupo;
                    if (host.Mac is null && status.Mac != null)
                    {
                        host.Mac = status.Mac;
                        host.Fabricante = _oui.DescreverFabricante(host.Mac);
                    }
                }
            }
            finally
            {
                vagas.Release();
            }

            var feitos = Interlocked.Increment(ref concluidos);
            progresso?.Report(new ProgressoVarredura(EtapaNomes, feitos, total, host));
        });

        await Task.WhenAll(tarefas).ConfigureAwait(false);
    }

    /// <summary>
    /// O SendARP bloqueia a thread até o fim do pedido. Sem isso, o pool de threads demora a
    /// crescer e a descoberta fica lenta.
    /// </summary>
    private void GarantirThreads()
    {
        ThreadPool.GetMinThreads(out var trabalho, out var entradaSaida);
        var desejado = _opcoes.Paralelismo + 16;
        if (trabalho < desejado)
        {
            ThreadPool.SetMinThreads(desejado, entradaSaida);
        }
    }
}

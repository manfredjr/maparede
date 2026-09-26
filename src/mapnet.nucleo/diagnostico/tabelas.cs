using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MapNet.Nucleo;

/// <summary>Linha da tabela ARP do Windows.</summary>
public sealed record LinhaArp(IPAddress Ip, byte[] Mac, int Tipo, string Interface);

/// <summary>Linha da tabela de rotas IPv4.</summary>
public sealed record LinhaRota(IPAddress Destino, IPAddress Mascara, IPAddress Gateway, string Interface, int Metrica);

/// <summary>Conexão TCP ou porta UDP aberta, com o programa dono quando o Windows deixa ver.</summary>
public sealed record LinhaConexao(string Protocolo, IPAddress Local, int PortaLocal, IPAddress? Remoto, int PortaRemota, int Estado, int Pid, string? Programa);

/// <summary>Tabelas de rede do Windows. Os testes trocam por dados montados.</summary>
public interface ITabelasRede
{
    IReadOnlyList<LinhaArp> Arp();

    IReadOnlyList<LinhaRota> Rotas();

    IReadOnlyList<LinhaConexao> Conexoes();
}

/// <summary>Textos das tabelas, iguais em qualquer Windows.</summary>
public static class TextoTabelas
{
    public static string TipoArp(int tipo) => tipo switch
    {
        3 => "dinâmico",
        4 => "estático",
        2 => "inválido",
        _ => "outro",
    };

    public static string EstadoTcp(int estado) => estado switch
    {
        1 => "fechada",
        2 => "escutando",
        3 => "SYN enviado",
        4 => "SYN recebido",
        5 => "estabelecida",
        6 => "FIN-WAIT-1",
        7 => "FIN-WAIT-2",
        8 => "CLOSE-WAIT",
        9 => "fechando",
        10 => "LAST-ACK",
        11 => "TIME-WAIT",
        12 => "apagando",
        _ => "desconhecido",
    };

    /// <summary>A porta vem nos dois bytes baixos, na ordem da rede.</summary>
    public static int PortaDaApi(uint valor) => (int)(((valor & 0xFF) << 8) | ((valor >> 8) & 0xFF));

    /// <summary>
    /// Entrada que interessa ao técnico. Ficam de fora o multicast (224 a 239), o broadcast, as
    /// inválidas (pedido ARP sem resposta, que a própria varredura deixa) e as sem MAC.
    /// </summary>
    public static bool ArpUtil(LinhaArp l)
    {
        var primeiro = l.Ip.GetAddressBytes()[0];
        return l.Tipo != 2
            && primeiro is < 224 or > 239
            && !l.Ip.Equals(IPAddress.Broadcast)
            && l.Mac.Length == 6
            && l.Mac.Any(b => b != 0)
            && !l.Mac.All(b => b == 0xFF);
    }

    public static IEnumerable<string> Arp(IReadOnlyList<LinhaArp> linhas)
    {
        yield return $"  {"IP",-16} {"MAC",-18} {"Tipo",-10} Interface";
        foreach (var l in linhas.Where(ArpUtil).OrderBy(l => l.Interface).ThenBy(l => SubRede.ParaNumero(l.Ip)))
        {
            var mac = l.Mac.Length == 6 ? EnderecoMac.Formatar(l.Mac) : "-";
            yield return $"  {l.Ip,-16} {mac,-18} {TipoArp(l.Tipo),-10} {l.Interface}";
        }
    }

    public static IEnumerable<string> Rotas(IReadOnlyList<LinhaRota> linhas)
    {
        yield return $"  {"Destino",-19} {"Gateway",-16} {"Métrica",7}  Interface";
        foreach (var l in linhas.OrderBy(l => SubRede.ParaNumero(l.Destino)).ThenBy(l => SubRede.PrefixoDaMascara(l.Mascara)))
        {
            var destino = $"{l.Destino}/{SubRede.PrefixoDaMascara(l.Mascara)}";
            var gateway = l.Gateway.Equals(IPAddress.Any) ? "na rede local" : l.Gateway.ToString();
            yield return $"  {destino,-19} {gateway,-16} {l.Metrica.ToString(CultureInfo.InvariantCulture),7}  {l.Interface}";
        }
    }

    public static IEnumerable<string> Conexoes(IReadOnlyList<LinhaConexao> linhas)
    {
        yield return $"  {"Prot.",-5} {"Local",-22} {"Remoto",-22} {"Estado",-13} Programa";
        foreach (var l in linhas.OrderBy(l => l.Protocolo).ThenBy(l => l.Estado == 2 ? 0 : 1).ThenBy(l => l.PortaLocal))
        {
            var local = $"{l.Local}:{l.PortaLocal}";
            var remoto = l.Remoto is null ? "-" : $"{l.Remoto}:{l.PortaRemota}";
            var estado = l.Protocolo == "UDP" ? "-" : EstadoTcp(l.Estado);
            var programa = l.Programa is { Length: > 0 } nome ? $"{nome} ({l.Pid})" : l.Pid > 0 ? $"processo {l.Pid}" : "-";
            yield return $"  {l.Protocolo,-5} {local,-22} {remoto,-22} {estado,-13} {programa}";
        }
    }
}

/// <summary>Uma ferramenta que só lê uma tabela e mostra.</summary>
public sealed class FerramentaTabela(string titulo, string descricao, Func<IEnumerable<string>> linhas, Func<string> resumo, string sufixo) : IFerramenta
{
    public string Titulo => titulo;

    public string Descricao => descricao;

    public bool PedeAlvo => false;

    public bool PedeServidor => false;

    public bool PodeContinuo => false;

    public async Task ExecutarAsync(ParametrosFerramenta p, SaidaFerramenta saida, CancellationToken cancelamento)
    {
        var texto = await Task.Run(() => linhas().ToList(), cancelamento).ConfigureAwait(true);
        saida.Linha($"{titulo}: {resumo()}{sufixo}.");
        foreach (var l in texto)
        {
            saida.Texto(l);
        }
    }

    public static FerramentaTabela Arp(ITabelasRede tabelas)
    {
        IReadOnlyList<LinhaArp> lidas = [];
        return new("ARP", "Lista os IPs e MACs que o Windows conhece na rede local.",
            () => TextoTabelas.Arp(lidas = tabelas.Arp()),
            () => $"{lidas.Count(TextoTabelas.ArpUtil)} entradas (multicast, broadcast e inválidas ficam de fora: {lidas.Count(l => !TextoTabelas.ArpUtil(l))})",
            string.Empty);
    }

    public static FerramentaTabela Rotas(ITabelasRede tabelas)
    {
        IReadOnlyList<LinhaRota> lidas = [];
        return new("Rotas", "Mostra a tabela de rotas IPv4: por onde sai cada destino.",
            () => TextoTabelas.Rotas(lidas = tabelas.Rotas()), () => $"{lidas.Count}", " rotas");
    }

    public static FerramentaTabela Conexoes(ITabelasRede tabelas)
    {
        IReadOnlyList<LinhaConexao> lidas = [];
        return new("Conexões", "Lista as conexões TCP e as portas UDP abertas neste computador, com o programa dono.",
            () => TextoTabelas.Conexoes(lidas = tabelas.Conexoes()), () => $"{lidas.Count}", " conexões e portas");
    }
}

/// <summary>
/// Tabelas pelo iphlpapi.dll: GetIpNetTable, GetIpForwardTable, GetExtendedTcpTable e
/// GetExtendedUdpTable. Tudo sem administrador. O programa dono de um processo do sistema
/// pode não aparecer para usuário comum.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class TabelasWindows : ITabelasRede
{
    private const int BufferPequeno = 122;
    private const int SemErro = 0;
    private const int Ipv4 = 2;
    private const int TcpComPidTodas = 5;
    private const int UdpComPid = 1;

    public IReadOnlyList<LinhaArp> Arp()
    {
        var nomes = NomesDasInterfaces();
        return Ler((IntPtr m, ref int t) => GetIpNetTable(m, ref t, false), 24, (m, i) =>
        {
            var tamanhoMac = Marshal.ReadInt32(m, i + 4);
            var mac = new byte[Math.Clamp(tamanhoMac, 0, 8)];
            Marshal.Copy(m + i + 8, mac, 0, mac.Length);
            var ip = new IPAddress((uint)Marshal.ReadInt32(m, i + 16));
            return new LinhaArp(ip, mac, Marshal.ReadInt32(m, i + 20), Nome(nomes, Marshal.ReadInt32(m, i)));
        });
    }

    public IReadOnlyList<LinhaRota> Rotas()
    {
        var nomes = NomesDasInterfaces();
        return Ler((IntPtr m, ref int t) => GetIpForwardTable(m, ref t, false), 56, (m, i) => new LinhaRota(
            new IPAddress((uint)Marshal.ReadInt32(m, i)),
            new IPAddress((uint)Marshal.ReadInt32(m, i + 4)),
            new IPAddress((uint)Marshal.ReadInt32(m, i + 12)),
            Nome(nomes, Marshal.ReadInt32(m, i + 16)),
            Marshal.ReadInt32(m, i + 36)));
    }

    public IReadOnlyList<LinhaConexao> Conexoes()
    {
        var programas = new Dictionary<int, string?>();
        var tcp = Ler((IntPtr m, ref int t) => GetExtendedTcpTable(m, ref t, false, Ipv4, TcpComPidTodas, 0), 24, (m, i) =>
        {
            var pid = Marshal.ReadInt32(m, i + 20);
            var estado = Marshal.ReadInt32(m, i);
            return new LinhaConexao("TCP",
                new IPAddress((uint)Marshal.ReadInt32(m, i + 4)),
                TextoTabelas.PortaDaApi((uint)Marshal.ReadInt32(m, i + 8)),
                estado == 2 ? null : new IPAddress((uint)Marshal.ReadInt32(m, i + 12)),
                TextoTabelas.PortaDaApi((uint)Marshal.ReadInt32(m, i + 16)),
                estado, pid, Programa(programas, pid));
        });
        var udp = Ler((IntPtr m, ref int t) => GetExtendedUdpTable(m, ref t, false, Ipv4, UdpComPid, 0), 12, (m, i) =>
        {
            var pid = Marshal.ReadInt32(m, i + 8);
            return new LinhaConexao("UDP",
                new IPAddress((uint)Marshal.ReadInt32(m, i)),
                TextoTabelas.PortaDaApi((uint)Marshal.ReadInt32(m, i + 4)),
                null, 0, 0, pid, Programa(programas, pid));
        });
        return [.. tcp, .. udp];
    }

    private delegate int Chamada(IntPtr memoria, ref int tamanho);

    /// <summary>Pede o tamanho, aloca, lê o número de linhas e monta cada uma. Falha vira lista vazia.</summary>
    private static List<T> Ler<T>(Chamada chamada, int tamanhoLinha, Func<IntPtr, int, T> montar)
    {
        var tamanho = 0;
        var r = chamada(IntPtr.Zero, ref tamanho);
        if (r != BufferPequeno && r != SemErro || tamanho <= 0)
        {
            return [];
        }

        var memoria = Marshal.AllocHGlobal(tamanho);
        try
        {
            if (chamada(memoria, ref tamanho) != SemErro)
            {
                return [];
            }

            var quantidade = Marshal.ReadInt32(memoria);
            var lista = new List<T>(quantidade);
            for (var n = 0; n < quantidade && 4 + ((n + 1) * tamanhoLinha) <= tamanho; n++)
            {
                lista.Add(montar(memoria, 4 + (n * tamanhoLinha)));
            }

            return lista;
        }
        finally
        {
            Marshal.FreeHGlobal(memoria);
        }
    }

    private static Dictionary<int, string> NomesDasInterfaces()
    {
        var nomes = new Dictionary<int, string>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            try
            {
                nomes[ni.GetIPProperties().GetIPv4Properties().Index] = ni.Name;
            }
            catch (NetworkInformationException)
            {
                // Placa sem IPv4.
            }
        }

        return nomes;
    }

    private static string Nome(Dictionary<int, string> nomes, int indice) =>
        nomes.TryGetValue(indice, out var nome) ? nome : $"interface {indice}";

    private static string? Programa(Dictionary<int, string?> cache, int pid)
    {
        if (pid <= 0)
        {
            return pid == 0 ? "Sistema ocioso" : null;
        }

        if (!cache.TryGetValue(pid, out var nome))
        {
            try
            {
                using var p = System.Diagnostics.Process.GetProcessById(pid);
                nome = p.ProcessName;
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                nome = null;
            }

            cache[pid] = nome;
        }

        return nome;
    }

    [LibraryImport("iphlpapi.dll")]
    private static partial int GetIpNetTable(IntPtr tabela, ref int tamanho, [MarshalAs(UnmanagedType.Bool)] bool ordenar);

    [LibraryImport("iphlpapi.dll")]
    private static partial int GetIpForwardTable(IntPtr tabela, ref int tamanho, [MarshalAs(UnmanagedType.Bool)] bool ordenar);

    [LibraryImport("iphlpapi.dll")]
    private static partial int GetExtendedTcpTable(IntPtr tabela, ref int tamanho, [MarshalAs(UnmanagedType.Bool)] bool ordenar, int familia, int classe, int reservado);

    [LibraryImport("iphlpapi.dll")]
    private static partial int GetExtendedUdpTable(IntPtr tabela, ref int tamanho, [MarshalAs(UnmanagedType.Bool)] bool ordenar, int familia, int classe, int reservado);
}

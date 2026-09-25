using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MapNet.Nucleo;

/// <summary>Descobre o MAC de um IP da mesma sub-rede.</summary>
public interface ISondaArp
{
    /// <summary>Devolve o MAC, ou null quando o host não respondeu ao ARP.</summary>
    byte[]? Resolver(IPAddress destino, IPAddress origem);
}

public static class SondaArp
{
    /// <summary>A sonda do sistema: SendARP no Windows, tabela do kernel no Linux.</summary>
    public static ISondaArp? Padrao()
    {
        if (OperatingSystem.IsWindows())
        {
            return new ArpWindows();
        }

        return OperatingSystem.IsLinux() ? new ArpLinux() : null;
    }
}

/// <summary>
/// SendARP do iphlpapi.dll. Manda um pedido ARP de verdade e espera a resposta, sem precisar
/// de administrador. Pega também o host que bloqueia ping no firewall, porque ninguém na
/// mesma rede consegue deixar de responder ao ARP.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class ArpWindows : ISondaArp
{
    private const int SemErro = 0;

    public byte[]? Resolver(IPAddress destino, IPAddress origem)
    {
        var mac = new byte[8];
        var tamanho = (uint)mac.Length;
        var resultado = SendARP(ParaIpApi(destino), ParaIpApi(origem), mac, ref tamanho);
        if (resultado != SemErro || tamanho < 6)
        {
            return null;
        }

        var seis = mac[..6];
        return EnderecoMac.Valido(seis) ? seis : null;
    }

    /// <summary>A API espera o IP na ordem da rede, lido como número da máquina.</summary>
    private static uint ParaIpApi(IPAddress ip) => BitConverter.ToUInt32(ip.GetAddressBytes(), 0);

    [LibraryImport("iphlpapi.dll")]
    private static partial int SendARP(uint destino, uint origem, [Out] byte[] mac, ref uint tamanho);
}

/// <summary>
/// No Linux não há SendARP sem privilégio. Serve para testar o núcleo fora do Windows:
/// lê a tabela ARP do kernel, que o ping acabou de preencher.
/// </summary>
public sealed class ArpLinux : ISondaArp
{
    public byte[]? Resolver(IPAddress destino, IPAddress origem)
    {
        const string arquivo = "/proc/net/arp";
        if (!File.Exists(arquivo))
        {
            return null;
        }

        foreach (var linha in File.ReadLines(arquivo).Skip(1))
        {
            var partes = linha.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length >= 4 && partes[0] == destino.ToString())
            {
                var mac = partes[3].Split(':').Select(p => byte.Parse(p, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToArray();
                return EnderecoMac.Valido(mac) ? mac : null;
            }
        }

        return null;
    }
}

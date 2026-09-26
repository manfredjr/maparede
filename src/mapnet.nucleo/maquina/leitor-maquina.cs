using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MapNet.Nucleo;

/// <summary>De onde vêm os dados da máquina. Os testes trocam cada parte por uma simulada.</summary>
public sealed class FontesMaquina
{
    public required Func<DadosComputador?> Computador { get; init; }

    public required Func<InterfaceRede, DadosPlaca?> Placa { get; init; }

    public IFonteWifi? Wifi { get; init; }

    /// <summary>As fontes do Windows. Fora do Windows, só o que o .NET lê em qualquer sistema.</summary>
    public static FontesMaquina Padrao() => new()
    {
        Computador = LeitorMaquina.LerComputador,
        Placa = LeitorMaquina.LerPlaca,
        Wifi = OperatingSystem.IsWindows() ? new WifiWindows() : null,
    };
}

/// <summary>
/// Monta as <see cref="InformacoesMaquina"/> da interface escolhida. Cada leitura que falha vira
/// "não informado" e as outras seguem: nada aqui derruba a tela.
/// </summary>
public static partial class LeitorMaquina
{
    public static InformacoesMaquina Ler(InterfaceRede interfaceRede, FontesMaquina fontes, int prefixoMinimo)
    {
        return new InformacoesMaquina
        {
            Interface = interfaceRede,
            Computador = Tentar(fontes.Computador),
            Placa = Tentar(() => fontes.Placa(interfaceRede)),
            Wifi = interfaceRede.Tipo == TipoInterface.WiFi && fontes.Wifi is { } wifi
                ? Tentar(() => wifi.Ler(interfaceRede.Id))
                : null,
            SubRedeAVarrer = Varredor.SubRedeAVarrer(interfaceRede, prefixoMinimo, out _),
        };
    }

    private static T? Tentar<T>(Func<T?> leitura)
        where T : class
    {
        try
        {
            return leitura();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>Nome, grupo de trabalho ou domínio e usuário, sem administrador.</summary>
    public static DadosComputador LerComputador()
    {
        string? grupo = null;
        var ehDominio = false;
        if (OperatingSystem.IsWindows())
        {
            (grupo, ehDominio) = LerAdesao();
        }

        if (string.IsNullOrEmpty(grupo))
        {
            var dominio = IPGlobalProperties.GetIPGlobalProperties().DomainName;
            if (!string.IsNullOrEmpty(dominio))
            {
                grupo = dominio;
                ehDominio = true;
            }
        }

        return new DadosComputador(Environment.MachineName, grupo, ehDominio, Environment.UserName);
    }

    /// <summary>MTU, IPv6, sufixo DNS e DHCP da placa, pelo que o .NET lê do Windows.</summary>
    public static DadosPlaca? LerPlaca(InterfaceRede interfaceRede)
    {
        var ni = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => n.Id == interfaceRede.Id);
        if (ni is null)
        {
            return null;
        }

        var propriedades = ni.GetIPProperties();
        var ipv6 = propriedades.UnicastAddresses
            .Select(u => u.Address)
            .Where(a => a.AddressFamily == AddressFamily.InterNetworkV6)
            .ToList();

        int? mtu = null;
        bool? dhcp = null;
        IPAddress? servidor = null;
        DateTimeOffset? obtida = null;
        DateTimeOffset? validade = null;
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var v4 = propriedades.GetIPv4Properties();
                mtu = v4.Mtu;
                dhcp = v4.IsDhcpEnabled;
            }
            catch (NetworkInformationException)
            {
                // Placa sem IPv4 configurado no Windows: segue sem MTU e sem DHCP.
            }

            servidor = propriedades.DhcpServerAddresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            if (dhcp == true)
            {
                (obtida, validade) = ConcessaoDhcpWindows.Ler(interfaceRede.Id);
            }
        }

        return new DadosPlaca
        {
            Mtu = mtu,
            Ipv6 = ipv6,
            SufixoDns = propriedades.DnsSuffix,
            DhcpLigado = dhcp,
            ServidorDhcp = servidor,
            ConcessaoObtida = obtida,
            ConcessaoValidade = validade,
        };
    }

    /// <summary>Grupo de trabalho ou domínio pelo NetGetJoinInformation, que responde sem administrador.</summary>
    [SupportedOSPlatform("windows")]
    private static (string? Nome, bool EhDominio) LerAdesao()
    {
        const int GrupoDeTrabalho = 2;
        const int Dominio = 3;
        if (NetGetJoinInformation(null, out var nome, out var estado) != 0)
        {
            return (null, false);
        }

        try
        {
            return estado is GrupoDeTrabalho or Dominio
                ? (Marshal.PtrToStringUni(nome), estado == Dominio)
                : (null, false);
        }
        finally
        {
            NetApiBufferFree(nome);
        }
    }

    [LibraryImport("netapi32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int NetGetJoinInformation(string? servidor, out IntPtr nome, out int estado);

    [LibraryImport("netapi32.dll")]
    private static partial int NetApiBufferFree(IntPtr memoria);
}

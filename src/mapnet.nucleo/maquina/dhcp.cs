using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MapNet.Nucleo;

/// <summary>Textos da concessão do DHCP.</summary>
public static class Dhcp
{
    private static readonly CultureInfo _ptBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// "IP fixo", "de 26/09 08:02 até 27/09 08:02", "vencida em 26/09 08:02" ou "não informado".
    /// As datas saem no fuso de "agora".
    /// </summary>
    public static string TextoValidade(bool? dhcpLigado, DateTimeOffset? obtida, DateTimeOffset? validade, DateTimeOffset agora)
    {
        if (dhcpLigado == false)
        {
            return "IP fixo";
        }

        if (validade is not { } fim)
        {
            return InformacoesMaquina.NaoInformado;
        }

        if (fim <= agora)
        {
            return $"vencida em {Data(fim, agora)}";
        }

        return obtida is { } inicio ? $"de {Data(inicio, agora)} até {Data(fim, agora)}" : $"até {Data(fim, agora)}";
    }

    /// <summary>Data no fuso de "agora", que na tela é a hora local do computador.</summary>
    private static string Data(DateTimeOffset data, DateTimeOffset agora) => data.ToOffset(agora.Offset).ToString("dd/MM HH:mm", _ptBr);

    /// <summary>Converte o time_t do Windows (segundos desde 1970, UTC). Zero ou negativo é "sem data".</summary>
    public static DateTimeOffset? DeTimeT(long segundos) =>
        segundos > 0 && segundos < 253_402_300_800 ? DateTimeOffset.FromUnixTimeSeconds(segundos) : null;
}

/// <summary>
/// Datas da concessão do DHCP pelo GetAdaptersInfo do iphlpapi.dll. O .NET dá o servidor e se o
/// DHCP está ligado, mas não as datas. Roda sem administrador.
/// </summary>
[SupportedOSPlatform("windows")]
public static partial class ConcessaoDhcpWindows
{
    private const int SemErro = 0;
    private const int BufferPequeno = 111;

    /// <summary>Datas de quando a concessão foi obtida e de quando vence, ou nulos.</summary>
    public static (DateTimeOffset? Obtida, DateTimeOffset? Validade) Ler(string idInterface)
    {
        uint tamanho = 0;
        var resultado = GetAdaptersInfo(IntPtr.Zero, ref tamanho);
        if (resultado != BufferPequeno || tamanho == 0)
        {
            return (null, null);
        }

        var memoria = Marshal.AllocHGlobal((int)tamanho);
        try
        {
            if (GetAdaptersInfo(memoria, ref tamanho) != SemErro)
            {
                return (null, null);
            }

            var atual = memoria;
            while (atual != IntPtr.Zero)
            {
                var placa = Marshal.PtrToStructure<IpAdapterInfo>(atual);
                if (string.Equals(placa.AdapterName, idInterface, StringComparison.OrdinalIgnoreCase))
                {
                    return placa.DhcpEnabled == 0
                        ? (null, null)
                        : (Dhcp.DeTimeT(placa.LeaseObtained), Dhcp.DeTimeT(placa.LeaseExpires));
                }

                atual = placa.Next;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(memoria);
        }

        return (null, null);
    }

    [LibraryImport("iphlpapi.dll")]
    private static partial int GetAdaptersInfo(IntPtr informacoes, ref uint tamanho);

    // Estruturas do IP_ADAPTER_INFO e do IP_ADDR_STRING, no Windows de 64 bits (time_t de 8 bytes).
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct IpAddrString
    {
        public IntPtr Next;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public string IpAddress;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public string IpMask;
        public uint Context;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct IpAdapterInfo
    {
        public IntPtr Next;
        public uint ComboIndex;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string AdapterName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 132)]
        public string Description;
        public uint AddressLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] Address;
        public uint Index;
        public uint Type;
        public uint DhcpEnabled;
        public IntPtr CurrentIpAddress;
        public IpAddrString IpAddressList;
        public IpAddrString GatewayList;
        public IpAddrString DhcpServer;
        public int HaveWins;
        public IpAddrString PrimaryWinsServer;
        public IpAddrString SecondaryWinsServer;
        public long LeaseObtained;
        public long LeaseExpires;
    }
}

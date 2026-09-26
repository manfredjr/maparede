using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace MapNet.Nucleo;

/// <summary>Contas do Wi-Fi que não dependem do Windows.</summary>
public static class Wifi
{
    /// <summary>
    /// Banda da conexão. A frequência decide quando vem, porque o 6 GHz reusa números de canal
    /// do 2,4 e do 5 GHz. Sem frequência, o canal decide: 1 a 14 é 2,4 GHz e 32 a 177 é 5 GHz.
    /// </summary>
    public static string? Banda(int? canal, long? frequenciaKhz)
    {
        switch (frequenciaKhz)
        {
            case >= 2_400_000 and < 2_500_000:
                return "2,4 GHz";
            case >= 5_150_000 and < 5_925_000:
                return "5 GHz";
            case >= 5_925_000 and <= 7_125_000:
                return "6 GHz";
        }

        return canal switch
        {
            >= 1 and <= 14 => "2,4 GHz",
            >= 32 and <= 177 => "5 GHz",
            _ => null,
        };
    }

    /// <summary>"78% (bom)". O Windows já mede de 0 a 100.</summary>
    public static string TextoSinal(int? qualidade)
    {
        if (qualidade is not { } q || q < 0)
        {
            return InformacoesMaquina.NaoInformado;
        }

        q = Math.Min(q, 100);
        var nota = q switch
        {
            >= 80 => "ótimo",
            >= 60 => "bom",
            >= 40 => "regular",
            _ => "fraco",
        };
        return $"{q}% ({nota})";
    }
}

/// <summary>Lê a conexão Wi-Fi de uma placa. Os testes trocam por uma fonte simulada.</summary>
public interface IFonteWifi
{
    /// <summary>Null quando a placa não está conectada a uma rede Wi-Fi ou a leitura falhou.</summary>
    DadosWifi? Ler(string idInterface);
}

/// <summary>
/// API de WLAN do Windows (wlanapi.dll). Não depende do idioma do Windows, ao contrário da saída
/// do netsh. A biblioteca só é carregada quando a placa é Wi-Fi.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class WifiWindows : IFonteWifi
{
    private const int SemErro = 0;
    private const int AcessoNegado = 5;
    private const int OpcodeConexaoAtual = 7;
    private const int OpcodeCanal = 8;
    private const int EstadoConectado = 1;
    private const int TipoBssQualquer = 3;

    // Posições dentro do WLAN_CONNECTION_ATTRIBUTES: estado (4), modo (4), perfil (256 WCHAR),
    // e então o WLAN_ASSOCIATION_ATTRIBUTES.
    private const int PosAssociacao = 8 + 512;
    private const int PosTamanhoSsid = PosAssociacao;
    private const int PosSsid = PosAssociacao + 4;
    private const int PosBssid = PosAssociacao + 40;
    private const int PosQualidade = PosAssociacao + 56;

    // Posições dentro do WLAN_BSS_ENTRY, que tem 360 bytes.
    private const int TamanhoEntradaBss = 360;
    private const int PosEntradaBssid = 40;
    private const int PosEntradaFrequencia = 92;

    public DadosWifi? Ler(string idInterface)
    {
        if (!Guid.TryParse(idInterface, out var guid))
        {
            return null;
        }

        try
        {
            return LerComGuid(guid);
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            // Windows sem o recurso de rede sem fio.
            return null;
        }
    }

    private static DadosWifi? LerComGuid(Guid guid)
    {
        if (WlanOpenHandle(2, IntPtr.Zero, out _, out var sessao) != SemErro)
        {
            return null;
        }

        try
        {
            var r = WlanQueryInterface(sessao, ref guid, OpcodeConexaoAtual, IntPtr.Zero, out _, out var dados, IntPtr.Zero);
            if (r == AcessoNegado)
            {
                return new DadosWifi { LocalizacaoNegada = true, Canal = LerCanal(sessao, guid) };
            }

            if (r != SemErro || dados == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                if (Marshal.ReadInt32(dados) != EstadoConectado)
                {
                    return null;
                }

                var tamanhoSsid = Math.Clamp(Marshal.ReadInt32(dados, PosTamanhoSsid), 0, 32);
                var ssidBytes = new byte[tamanhoSsid];
                Marshal.Copy(dados + PosSsid, ssidBytes, 0, tamanhoSsid);
                var bssid = new byte[6];
                Marshal.Copy(dados + PosBssid, bssid, 0, 6);
                var qualidade = Marshal.ReadInt32(dados, PosQualidade);

                return new DadosWifi
                {
                    Ssid = tamanhoSsid > 0 ? Ssid(ssidBytes) : null,
                    Canal = LerCanal(sessao, guid),
                    FrequenciaKhz = LerFrequencia(sessao, guid, bssid),
                    Sinal = qualidade,
                };
            }
            finally
            {
                WlanFreeMemory(dados);
            }
        }
        finally
        {
            WlanCloseHandle(sessao, IntPtr.Zero);
        }
    }

    /// <summary>SSID em UTF-8, que é o que quase todo roteador usa. Bytes inválidos viram o caractere de troca.</summary>
    internal static string Ssid(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    private static int? LerCanal(IntPtr sessao, Guid guid)
    {
        if (WlanQueryInterface(sessao, ref guid, OpcodeCanal, IntPtr.Zero, out _, out var dados, IntPtr.Zero) != SemErro
            || dados == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var canal = Marshal.ReadInt32(dados);
            return canal > 0 ? canal : null;
        }
        finally
        {
            WlanFreeMemory(dados);
        }
    }

    /// <summary>
    /// Frequência do ponto de acesso conectado, pela lista de BSS da última busca do Windows. A
    /// lista vem sem filtro e a entrada é achada pelo BSSID: com o filtro por SSID, o Windows
    /// devolve a lista vazia quando a segurança da rede não bate com o pedido.
    /// </summary>
    private static long? LerFrequencia(IntPtr sessao, Guid guid, byte[] bssid)
    {
        if (WlanGetNetworkBssList(sessao, ref guid, IntPtr.Zero, TipoBssQualquer, 0, IntPtr.Zero, out var lista) != SemErro
            || lista == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var quantidade = Marshal.ReadInt32(lista, 4);
            for (var n = 0; n < quantidade; n++)
            {
                var entrada = lista + 8 + (n * TamanhoEntradaBss);
                var mac = new byte[6];
                Marshal.Copy(entrada + PosEntradaBssid, mac, 0, 6);
                if (mac.AsSpan().SequenceEqual(bssid))
                {
                    var khz = (uint)Marshal.ReadInt32(entrada, PosEntradaFrequencia);
                    return khz > 0 ? khz : null;
                }
            }
        }
        finally
        {
            WlanFreeMemory(lista);
        }

        return null;
    }

    [LibraryImport("wlanapi.dll")]
    private static partial int WlanOpenHandle(uint versao, IntPtr reservado, out uint versaoNegociada, out IntPtr sessao);

    [LibraryImport("wlanapi.dll")]
    private static partial int WlanCloseHandle(IntPtr sessao, IntPtr reservado);

    [LibraryImport("wlanapi.dll")]
    private static partial int WlanQueryInterface(IntPtr sessao, ref Guid placa, int opcode, IntPtr reservado,
        out int tamanho, out IntPtr dados, IntPtr tipoValor);

    [LibraryImport("wlanapi.dll")]
    private static partial int WlanGetNetworkBssList(IntPtr sessao, ref Guid placa, IntPtr ssid, int tipoBss,
        int segura, IntPtr reservado, out IntPtr lista);

    [LibraryImport("wlanapi.dll")]
    private static partial void WlanFreeMemory(IntPtr memoria);
}

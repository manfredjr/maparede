namespace MapaRede.Nucleo;

/// <summary>Funções de apoio para endereço MAC de 6 bytes.</summary>
public static class EnderecoMac
{
    /// <summary>Formata no padrão AA:BB:CC:DD:EE:FF.</summary>
    public static string Formatar(ReadOnlySpan<byte> mac)
    {
        if (mac.IsEmpty)
        {
            return string.Empty;
        }

        return string.Join(":", mac.ToArray().Select(b => b.ToString("X2")));
    }

    /// <summary>
    /// Bit "administrado localmente" ligado: o MAC foi inventado pelo aparelho, e não gravado
    /// pelo fabricante. É o caso do endereço privativo do Android, do iPhone e do Windows.
    /// </summary>
    public static bool AdministradoLocalmente(ReadOnlySpan<byte> mac) => !mac.IsEmpty && (mac[0] & 0x02) != 0;

    /// <summary>Seis bytes, sem ser tudo zero nem tudo FF.</summary>
    public static bool Valido(ReadOnlySpan<byte> mac)
    {
        if (mac.Length != 6)
        {
            return false;
        }

        var zeros = true;
        var uns = true;
        foreach (var b in mac)
        {
            zeros &= b == 0x00;
            uns &= b == 0xFF;
        }

        return !zeros && !uns;
    }
}

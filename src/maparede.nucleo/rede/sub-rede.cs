using System.Net;
using System.Net.Sockets;

namespace MapaRede.Nucleo;

/// <summary>
/// Sub-rede IPv4 calculada a partir de um endereço e da máscara (ou do prefixo).
/// Guarda os valores como número de 32 bits para facilitar as contas.
/// </summary>
public sealed class SubRede
{
    private readonly uint _rede;
    private readonly uint _mascara;

    private SubRede(uint rede, int prefixo)
    {
        Prefixo = prefixo;
        _mascara = MascaraDoPrefixo(prefixo);
        _rede = rede & _mascara;
    }

    /// <summary>Tamanho do prefixo, de 0 a 32 (o "/24" de 192.168.0.0/24).</summary>
    public int Prefixo { get; }

    public IPAddress Rede => ParaEndereco(_rede);

    public IPAddress Mascara => ParaEndereco(_mascara);

    public IPAddress Broadcast => ParaEndereco(_rede | ~_mascara);

    /// <summary>Primeiro endereço usável. Em /31 e /32 não há endereço de rede reservado.</summary>
    public IPAddress PrimeiroHost => ParaEndereco(NumeroPrimeiroHost);

    public IPAddress UltimoHost => ParaEndereco(NumeroUltimoHost);

    public long QuantidadeHosts => (long)NumeroUltimoHost - NumeroPrimeiroHost + 1;

    private uint NumeroPrimeiroHost => Prefixo >= 31 ? _rede : _rede + 1;

    private uint NumeroUltimoHost => Prefixo >= 31 ? _rede | ~_mascara : (_rede | ~_mascara) - 1;

    public static SubRede Calcular(IPAddress ip, IPAddress mascara) => Calcular(ip, PrefixoDaMascara(mascara));

    public static SubRede Calcular(IPAddress ip, int prefixo)
    {
        if (prefixo < 0 || prefixo > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(prefixo), "O prefixo precisa estar entre 0 e 32.");
        }

        return new SubRede(ParaNumero(ip), prefixo);
    }

    /// <summary>Lista todos os endereços usáveis da sub-rede, em ordem.</summary>
    public IEnumerable<IPAddress> Hosts()
    {
        for (long n = NumeroPrimeiroHost; n <= NumeroUltimoHost; n++)
        {
            yield return ParaEndereco((uint)n);
        }
    }

    public bool Contem(IPAddress ip) =>
        ip.AddressFamily == AddressFamily.InterNetwork && (ParaNumero(ip) & _mascara) == _rede;

    public override string ToString() => $"{Rede}/{Prefixo}";

    /// <summary>Converte 192.168.0.10 em número, com o primeiro octeto no byte mais alto.</summary>
    public static uint ParaNumero(IPAddress ip)
    {
        if (ip.AddressFamily != AddressFamily.InterNetwork)
        {
            throw new ArgumentException("Só endereços IPv4 são aceitos.", nameof(ip));
        }

        var b = ip.GetAddressBytes();
        return (uint)(b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3]);
    }

    public static IPAddress ParaEndereco(uint numero) =>
        new([(byte)(numero >> 24), (byte)(numero >> 16), (byte)(numero >> 8), (byte)numero]);

    public static uint MascaraDoPrefixo(int prefixo) => prefixo == 0 ? 0u : uint.MaxValue << (32 - prefixo);

    /// <summary>Converte 255.255.255.0 em 24. Recusa máscara com bits 1 fora de sequência.</summary>
    public static int PrefixoDaMascara(IPAddress mascara)
    {
        var valor = ParaNumero(mascara);
        var prefixo = 0;
        while (prefixo < 32 && (valor & (0x80000000u >> prefixo)) != 0)
        {
            prefixo++;
        }

        if (MascaraDoPrefixo(prefixo) != valor)
        {
            throw new ArgumentException($"Máscara inválida: {mascara}.", nameof(mascara));
        }

        return prefixo;
    }
}

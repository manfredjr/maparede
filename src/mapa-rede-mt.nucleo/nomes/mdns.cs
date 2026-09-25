using System.Buffers.Binary;
using System.Net;
using System.Text;

namespace MapaRedeMt.Nucleo;

/// <summary>
/// Pergunta de nome por mDNS (RFC 6762) enviada direto ao host, na porta UDP 5353.
/// Celulares, Macs, impressoras e aparelhos Linux com Avahi costumam responder com o
/// nome "algo.local". Como a pergunta sai de uma porta comum, a resposta volta só para nós.
/// </summary>
public static class Mdns
{
    public const int Porta = 5353;

    private const ushort TipoPtr = 12;
    private const ushort ClasseIn = 0x0001;
    private const int SaltosMaximos = 20;

    /// <summary>192.168.0.10 vira 10.0.168.192.in-addr.arpa.</summary>
    public static string NomeReverso(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        return $"{b[3]}.{b[2]}.{b[1]}.{b[0]}.in-addr.arpa";
    }

    public static byte[] MontarConsultaPtr(ushort id, string nome)
    {
        var pacote = new List<byte>(64);
        var cabecalho = new byte[12];
        BinaryPrimitives.WriteUInt16BigEndian(cabecalho, id);
        BinaryPrimitives.WriteUInt16BigEndian(cabecalho.AsSpan(4), 1);
        pacote.AddRange(cabecalho);
        foreach (var rotulo in nome.TrimEnd('.').Split('.'))
        {
            var bytes = Encoding.UTF8.GetBytes(rotulo);
            pacote.Add((byte)bytes.Length);
            pacote.AddRange(bytes);
        }

        pacote.Add(0);
        pacote.AddRange([0, (byte)TipoPtr, 0, (byte)ClasseIn]);
        return pacote.ToArray();
    }

    /// <summary>
    /// Procura, entre os registros da resposta, o PTR do nome perguntado e devolve o nome
    /// apontado, sem o ponto final. Devolve null quando não há.
    /// </summary>
    public static string? InterpretarRespostaPtr(ReadOnlySpan<byte> dados, string nomePerguntado)
    {
        if (dados.Length < 12 || (BinaryPrimitives.ReadUInt16BigEndian(dados[2..]) & 0x8000) == 0)
        {
            return null;
        }

        var perguntas = BinaryPrimitives.ReadUInt16BigEndian(dados[4..]);
        var registros = BinaryPrimitives.ReadUInt16BigEndian(dados[6..])
            + BinaryPrimitives.ReadUInt16BigEndian(dados[8..])
            + BinaryPrimitives.ReadUInt16BigEndian(dados[10..]);
        var pos = 12;

        for (var i = 0; i < perguntas; i++)
        {
            if (LerNome(dados, ref pos) is null || pos + 4 > dados.Length)
            {
                return null;
            }

            pos += 4;
        }

        for (var i = 0; i < registros; i++)
        {
            var dono = LerNome(dados, ref pos);
            if (dono is null || pos + 10 > dados.Length)
            {
                return null;
            }

            var tipo = BinaryPrimitives.ReadUInt16BigEndian(dados[pos..]);
            var tamanho = BinaryPrimitives.ReadUInt16BigEndian(dados[(pos + 8)..]);
            pos += 10;
            if (pos + tamanho > dados.Length)
            {
                return null;
            }

            if (tipo == TipoPtr && string.Equals(dono, nomePerguntado.TrimEnd('.'), StringComparison.OrdinalIgnoreCase))
            {
                var inicio = pos;
                var alvo = LerNome(dados, ref inicio);
                if (!string.IsNullOrWhiteSpace(alvo))
                {
                    return alvo;
                }
            }

            pos += tamanho;
        }

        return null;
    }

    public static async Task<string?> ConsultarAsync(IPAddress ip, int tempoMs, CancellationToken cancelamento)
    {
        var nome = NomeReverso(ip);
        var id = (ushort)Random.Shared.Next(1, ushort.MaxValue);
        var resposta = await Udp.PerguntarAsync(new IPEndPoint(ip, Porta), MontarConsultaPtr(id, nome), tempoMs, cancelamento)
            .ConfigureAwait(false);
        return resposta is null ? null : InterpretarRespostaPtr(resposta, nome);
    }

    /// <summary>
    /// Lê um nome DNS a partir de <paramref name="pos"/>, seguindo ponteiros de compressão.
    /// Ao final, <paramref name="pos"/> fica logo depois do nome no pacote. Devolve null se o
    /// pacote estiver malformado.
    /// </summary>
    internal static string? LerNome(ReadOnlySpan<byte> dados, ref int pos)
    {
        var rotulos = new List<string>();
        var cursor = pos;
        var saltou = false;
        var saltos = 0;

        while (true)
        {
            if (cursor >= dados.Length)
            {
                return null;
            }

            var tamanho = dados[cursor];
            if ((tamanho & 0xC0) == 0xC0)
            {
                if (cursor + 1 >= dados.Length || ++saltos > SaltosMaximos)
                {
                    return null;
                }

                if (!saltou)
                {
                    pos = cursor + 2;
                    saltou = true;
                }

                cursor = ((tamanho & 0x3F) << 8) | dados[cursor + 1];
                continue;
            }

            if ((tamanho & 0xC0) != 0)
            {
                return null;
            }

            cursor++;
            if (tamanho == 0)
            {
                break;
            }

            if (cursor + tamanho > dados.Length)
            {
                return null;
            }

            rotulos.Add(Encoding.UTF8.GetString(dados.Slice(cursor, tamanho)));
            cursor += tamanho;
        }

        if (!saltou)
        {
            pos = cursor;
        }

        return string.Join(".", rotulos);
    }
}

using System.Buffers.Binary;
using System.Net;
using System.Text;

namespace MapaRede.Nucleo;

/// <summary>Um nome registrado no NetBIOS do host, com o sufixo (tipo de serviço).</summary>
public sealed record NomeNetBios(string Nome, byte Sufixo, bool Grupo);

/// <summary>Resposta ao pedido de status NetBIOS (o mesmo que o "nbtstat -A" faz).</summary>
public sealed record RespostaNetBios(string? NomeComputador, string? Grupo, byte[]? Mac, IReadOnlyList<NomeNetBios> Nomes);

/// <summary>
/// Consulta de status NetBIOS na porta UDP 137 (RFC 1002, seção 4.2.17). Windows e Samba
/// respondem com o nome do computador e o grupo de trabalho ou domínio.
/// </summary>
public static class NetBios
{
    public const int Porta = 137;

    private const ushort TipoNbstat = 0x0021;
    private const ushort ClasseIn = 0x0001;

    /// <summary>Pedido de status para o nome coringa "*", que qualquer máquina com NetBIOS atende.</summary>
    public static byte[] MontarConsultaStatus(ushort id)
    {
        var pacote = new byte[50];
        BinaryPrimitives.WriteUInt16BigEndian(pacote.AsSpan(0), id);
        BinaryPrimitives.WriteUInt16BigEndian(pacote.AsSpan(4), 1); // uma pergunta

        // Nome codificado: "*" seguido de 15 bytes zero, cada byte vira duas letras de A a P.
        pacote[12] = 32;
        var nome = new byte[16];
        nome[0] = (byte)'*';
        for (var i = 0; i < 16; i++)
        {
            pacote[13 + (i * 2)] = (byte)('A' + (nome[i] >> 4));
            pacote[14 + (i * 2)] = (byte)('A' + (nome[i] & 0x0F));
        }

        pacote[45] = 0;
        BinaryPrimitives.WriteUInt16BigEndian(pacote.AsSpan(46), TipoNbstat);
        BinaryPrimitives.WriteUInt16BigEndian(pacote.AsSpan(48), ClasseIn);
        return pacote;
    }

    /// <summary>Interpreta a resposta. Devolve null quando o pacote não é uma resposta de status válida.</summary>
    public static RespostaNetBios? InterpretarResposta(ReadOnlySpan<byte> dados, ushort? idEsperado = null)
    {
        if (dados.Length < 12)
        {
            return null;
        }

        var id = BinaryPrimitives.ReadUInt16BigEndian(dados);
        var bandeiras = BinaryPrimitives.ReadUInt16BigEndian(dados[2..]);
        var respostas = BinaryPrimitives.ReadUInt16BigEndian(dados[6..]);
        if ((idEsperado.HasValue && id != idEsperado.Value) || (bandeiras & 0x8000) == 0 || respostas == 0)
        {
            return null;
        }

        var pos = 12;
        if (!PularNome(dados, ref pos) || pos + 10 > dados.Length)
        {
            return null;
        }

        var tipo = BinaryPrimitives.ReadUInt16BigEndian(dados[pos..]);
        var tamanhoDados = BinaryPrimitives.ReadUInt16BigEndian(dados[(pos + 8)..]);
        pos += 10;
        if (tipo != TipoNbstat || pos >= dados.Length || pos + tamanhoDados > dados.Length)
        {
            return null;
        }

        var quantidade = dados[pos++];
        var nomes = new List<NomeNetBios>();
        for (var i = 0; i < quantidade; i++)
        {
            if (pos + 18 > dados.Length)
            {
                return null;
            }

            var texto = Encoding.Latin1.GetString(dados.Slice(pos, 15)).TrimEnd(' ', '\0');
            var sufixo = dados[pos + 15];
            var grupo = (BinaryPrimitives.ReadUInt16BigEndian(dados[(pos + 16)..]) & 0x8000) != 0;
            nomes.Add(new NomeNetBios(texto, sufixo, grupo));
            pos += 18;
        }

        byte[]? mac = null;
        if (pos + 6 <= dados.Length)
        {
            var candidato = dados.Slice(pos, 6).ToArray();
            mac = EnderecoMac.Valido(candidato) ? candidato : null;
        }

        var computador = nomes.FirstOrDefault(n => !n.Grupo && n.Sufixo == 0x00)?.Nome
            ?? nomes.FirstOrDefault(n => !n.Grupo && n.Sufixo == 0x20)?.Nome;
        var grupoTrabalho = nomes.FirstOrDefault(n => n.Grupo && n.Sufixo == 0x00)?.Nome;
        return new RespostaNetBios(Vazio(computador), Vazio(grupoTrabalho), mac, nomes);
    }

    public static async Task<RespostaNetBios?> ConsultarAsync(IPAddress ip, int tempoMs, CancellationToken cancelamento)
    {
        var id = (ushort)Random.Shared.Next(1, ushort.MaxValue);
        var resposta = await Udp.PerguntarAsync(new IPEndPoint(ip, Porta), MontarConsultaStatus(id), tempoMs, cancelamento)
            .ConfigureAwait(false);
        return resposta is null ? null : InterpretarResposta(resposta, id);
    }

    private static string? Vazio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto;

    private static bool PularNome(ReadOnlySpan<byte> dados, ref int pos)
    {
        while (pos < dados.Length)
        {
            var tamanho = dados[pos];
            if ((tamanho & 0xC0) == 0xC0)
            {
                pos += 2;
                return pos <= dados.Length;
            }

            pos++;
            if (tamanho == 0)
            {
                return true;
            }

            pos += tamanho;
        }

        return false;
    }
}

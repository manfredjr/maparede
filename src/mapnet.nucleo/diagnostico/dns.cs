using System.Buffers.Binary;
using System.Net;
using System.Text;

namespace MapNet.Nucleo;

/// <summary>Um registro da resposta DNS, já em texto.</summary>
public sealed record RegistroDns(string Tipo, string Nome, string Valor, uint Ttl);

/// <summary>Resposta DNS interpretada. <see cref="Codigo"/> 0 é sucesso, 3 é nome inexistente.</summary>
public sealed record RespostaDns(int Codigo, IReadOnlyList<RegistroDns> Registros)
{
    public string TextoCodigo => Codigo switch
    {
        0 => "sem erro",
        1 => "pergunta malformada",
        2 => "falha no servidor",
        3 => "nome inexistente",
        4 => "tipo de consulta não suportado",
        5 => "consulta recusada pelo servidor",
        _ => $"código {Codigo}",
    };
}

/// <summary>Pergunta DNS por UDP 53, montada e lida pelo próprio núcleo.</summary>
public static class ConsultaDns
{
    public const ushort TipoA = 1;
    public const ushort TipoCname = 5;
    public const ushort TipoPtr = 12;
    public const ushort TipoMx = 15;
    public const ushort TipoAaaa = 28;

    public static byte[] MontarConsulta(ushort id, string nome, ushort tipo)
    {
        var pacote = new List<byte>(64);
        var cabecalho = new byte[12];
        BinaryPrimitives.WriteUInt16BigEndian(cabecalho, id);
        BinaryPrimitives.WriteUInt16BigEndian(cabecalho.AsSpan(2), 0x0100); // pede recursão
        BinaryPrimitives.WriteUInt16BigEndian(cabecalho.AsSpan(4), 1);
        pacote.AddRange(cabecalho);
        foreach (var rotulo in nome.TrimEnd('.').Split('.'))
        {
            var bytes = Encoding.ASCII.GetBytes(rotulo);
            pacote.Add((byte)bytes.Length);
            pacote.AddRange(bytes);
        }

        pacote.Add(0);
        pacote.AddRange([(byte)(tipo >> 8), (byte)tipo, 0, 1]);
        return pacote.ToArray();
    }

    /// <summary>
    /// Lê a resposta. Devolve null quando o pacote é malformado, não é resposta ou não é a
    /// resposta da pergunta <paramref name="id"/>. Tipo desconhecido é pulado.
    /// </summary>
    public static RespostaDns? Interpretar(ReadOnlySpan<byte> dados, ushort id)
    {
        if (dados.Length < 12
            || BinaryPrimitives.ReadUInt16BigEndian(dados) != id
            || (BinaryPrimitives.ReadUInt16BigEndian(dados[2..]) & 0x8000) == 0)
        {
            return null;
        }

        var codigo = BinaryPrimitives.ReadUInt16BigEndian(dados[2..]) & 0x000F;
        var perguntas = BinaryPrimitives.ReadUInt16BigEndian(dados[4..]);
        var respostas = BinaryPrimitives.ReadUInt16BigEndian(dados[6..]);
        var pos = 12;
        for (var i = 0; i < perguntas; i++)
        {
            if (Mdns.LerNome(dados, ref pos) is null || pos + 4 > dados.Length)
            {
                return null;
            }

            pos += 4;
        }

        var registros = new List<RegistroDns>();
        for (var i = 0; i < respostas; i++)
        {
            var dono = Mdns.LerNome(dados, ref pos);
            if (dono is null || pos + 10 > dados.Length)
            {
                return null;
            }

            var tipo = BinaryPrimitives.ReadUInt16BigEndian(dados[pos..]);
            var ttl = BinaryPrimitives.ReadUInt32BigEndian(dados[(pos + 4)..]);
            var tamanho = BinaryPrimitives.ReadUInt16BigEndian(dados[(pos + 8)..]);
            pos += 10;
            if (pos + tamanho > dados.Length)
            {
                return null;
            }

            var valor = LerValor(dados, pos, tipo, tamanho);
            if (valor is { } v)
            {
                registros.Add(new RegistroDns(v.Tipo, dono, v.Texto, ttl));
            }

            pos += tamanho;
        }

        return new RespostaDns(codigo, registros);
    }

    private static (string Tipo, string Texto)? LerValor(ReadOnlySpan<byte> dados, int pos, ushort tipo, int tamanho)
    {
        switch (tipo)
        {
            case TipoA when tamanho == 4:
                return ("A", new IPAddress(dados.Slice(pos, 4)).ToString());
            case TipoAaaa when tamanho == 16:
                return ("AAAA", new IPAddress(dados.Slice(pos, 16)).ToString());
            case TipoCname or TipoPtr:
                var p = pos;
                return Mdns.LerNome(dados, ref p) is { } nome ? (tipo == TipoPtr ? "PTR" : "CNAME", nome) : null;
            case TipoMx when tamanho > 2:
                var preferencia = BinaryPrimitives.ReadUInt16BigEndian(dados[pos..]);
                var q = pos + 2;
                return Mdns.LerNome(dados, ref q) is { } troca ? ("MX", $"{troca} (preferência {preferencia})") : null;
            default:
                return null;
        }
    }
}

/// <summary>
/// Consulta DNS com escolha do servidor. Nome pergunta A e AAAA; IP pergunta o PTR (nome
/// reverso). O servidor padrão é o DNS da interface escolhida.
/// </summary>
public sealed class FerramentaDns(Func<IPEndPoint, byte[], int, CancellationToken, Task<byte[]?>>? perguntar = null) : IFerramenta
{
    public const int TempoMs = 3000;

    private readonly Func<IPEndPoint, byte[], int, CancellationToken, Task<byte[]?>> _perguntar = perguntar ?? Udp.PerguntarAsync;

    public string Titulo => "DNS";

    public string Descricao => "Pergunta o nome (ou o IP) a um servidor DNS escolhido e mostra a resposta.";

    public bool PedeAlvo => true;

    public bool PedeServidor => true;

    public bool PodeContinuo => false;

    public async Task ExecutarAsync(ParametrosFerramenta p, SaidaFerramenta saida, CancellationToken cancelamento)
    {
        if (!Alvo.Valido(p.Alvo))
        {
            throw new ArgumentException($"\"{p.Alvo}\" não é um IP nem um nome de host");
        }

        if (!IPAddress.TryParse(p.Servidor?.Trim(), out var servidor))
        {
            throw new ArgumentException("informe o IP do servidor DNS");
        }

        var alvo = p.Alvo.Trim().TrimEnd('.');
        var perguntas = IPAddress.TryParse(alvo, out var ip)
            ? new[] { (NomeReverso(ip), ConsultaDns.TipoPtr) }
            : new[] { (alvo, ConsultaDns.TipoA), (alvo, ConsultaDns.TipoAaaa) };

        saida.Linha($"Consulta DNS de {alvo} no servidor {servidor}:");
        foreach (var (nome, tipo) in perguntas)
        {
            var id = (ushort)Random.Shared.Next(1, ushort.MaxValue);
            var inicio = DateTimeOffset.UtcNow;
            var resposta = await _perguntar(new IPEndPoint(servidor, 53), ConsultaDns.MontarConsulta(id, nome, tipo), TempoMs, cancelamento)
                .ConfigureAwait(true);
            var ms = (int)(DateTimeOffset.UtcNow - inicio).TotalMilliseconds;
            var rotulo = tipo switch { ConsultaDns.TipoA => "A", ConsultaDns.TipoAaaa => "AAAA", _ => "PTR" };
            if (resposta is null)
            {
                saida.Texto($"  {rotulo}: o servidor não respondeu em {TempoMs / 1000} segundos.");
                continue;
            }

            var r = ConsultaDns.Interpretar(resposta, id);
            if (r is null)
            {
                saida.Texto($"  {rotulo}: resposta malformada.");
                continue;
            }

            if (r.Codigo != 0)
            {
                saida.Texto($"  {rotulo}: {r.TextoCodigo} ({ms} ms).");
                continue;
            }

            if (r.Registros.Count == 0)
            {
                saida.Texto($"  {rotulo}: sem registro ({ms} ms).");
                continue;
            }

            foreach (var reg in r.Registros)
            {
                saida.Texto($"  {reg.Tipo,-5} {reg.Nome}  {reg.Valor}  (TTL {reg.Ttl} s, {ms} ms)");
            }
        }
    }

    /// <summary>192.0.2.10 vira 10.2.0.192.in-addr.arpa; IPv6 vira os nibbles em ip6.arpa.</summary>
    public static string NomeReverso(IPAddress ip)
    {
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return Mdns.NomeReverso(ip);
        }

        var hex = Convert.ToHexString(ip.GetAddressBytes()).ToLowerInvariant();
        return string.Join('.', hex.Reverse()) + ".ip6.arpa";
    }
}

using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace MapNet.Nucleo;

/// <summary>Como o programa conversa com cada porta na identificação.</summary>
public enum ProtocoloServico
{
    Http,
    Https,
    Banner,
}

/// <summary>O que um serviço disse de si. Todo texto aqui veio da rede e já passou por <see cref="TextoRede.Limpar"/>.</summary>
public sealed class ServicoIdentificado
{
    public required int Porta { get; init; }

    /// <summary>Nome curto do serviço: HTTP, HTTPS, SSH, FTP, SMTP ou UPnP.</summary>
    public required string Protocolo { get; init; }

    public int? CodigoHttp { get; set; }

    public string? Titulo { get; set; }

    public string? Servidor { get; set; }

    /// <summary>Endereço do redirecionamento, anotado sem seguir.</summary>
    public string? Redireciona { get; set; }

    public string? CertificadoNome { get; set; }

    public string? CertificadoEmissor { get; set; }

    public DateTimeOffset? CertificadoValidade { get; set; }

    public string? Banner { get; set; }

    /// <summary>Nome amigável, fabricante e modelo pela descrição UPnP.</summary>
    public string? Modelo { get; set; }

    /// <summary>Uma linha para o detalhe: 80 HTTP: título "Painel", servidor lighttpd.</summary>
    public string Texto
    {
        get
        {
            var partes = new List<string>();
            if (Modelo != null)
            {
                partes.Add(Modelo);
            }

            if (Titulo != null)
            {
                partes.Add($"título \"{Titulo}\"");
            }

            if (Servidor != null)
            {
                partes.Add($"servidor {Servidor}");
            }

            if (Redireciona != null)
            {
                partes.Add($"redireciona para {Redireciona}");
            }

            if (CertificadoNome != null)
            {
                var validade = CertificadoValidade is { } v ? $", válido até {v.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}" : string.Empty;
                partes.Add($"certificado {CertificadoNome}" + (CertificadoEmissor != null && CertificadoEmissor != CertificadoNome ? $" emitido por {CertificadoEmissor}" : string.Empty) + validade);
            }

            if (Banner != null)
            {
                partes.Add(Banner);
            }

            var porta = Porta > 0 ? $"{Porta.ToString(CultureInfo.InvariantCulture)} " : string.Empty;
            return $"{porta}{Protocolo}: " + (partes.Count > 0 ? string.Join(", ", partes) : "respondeu sem se identificar");
        }
    }
}

/// <summary>Limpeza do texto que vem da rede, que pode trazer qualquer coisa.</summary>
public static class TextoRede
{
    public const int Maximo = 200;

    /// <summary>Tira caractere de controle, junta espaços, apara e corta em <see cref="Maximo"/>. Vazio vira null.</summary>
    public static string? Limpar(string? texto, int maximo = Maximo)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return null;
        }

        var sb = new StringBuilder(Math.Min(texto.Length, maximo + 1));
        var espaco = false;
        foreach (var c in texto)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                espaco = sb.Length > 0;
                continue;
            }

            if (char.GetUnicodeCategory(c) == UnicodeCategory.Format)
            {
                continue;
            }

            if (espaco)
            {
                sb.Append(' ');
                espaco = false;
            }

            sb.Append(c);
            if (sb.Length >= maximo)
            {
                break;
            }
        }

        return sb.Length > 0 ? sb.ToString() : null;
    }
}

/// <summary>Leitura de uma resposta HTTP já recebida: código, cabeçalhos e título da página.</summary>
public static partial class RespostaHttp
{
    public const int MaximoBytes = 64 * 1024;

    public sealed record Dados(int? Codigo, string? Servidor, string? Localizacao, string? Titulo, string? Corpo);

    /// <summary>Pedido que o programa manda: só GET, sem cookie, sem credencial.</summary>
    public static byte[] Pedido(string host, string caminho) =>
        Encoding.ASCII.GetBytes(
            $"GET {caminho} HTTP/1.1\r\nHost: {host}\r\nUser-Agent: MapNet-MT/{ResultadoVarredura.VersaoPrograma}\r\n"
            + "Accept: text/html,application/xml;q=0.9,*/*;q=0.5\r\nConnection: close\r\n\r\n");

    public static Dados Interpretar(ReadOnlySpan<byte> resposta)
    {
        var bytes = resposta.Length > MaximoBytes ? resposta[..MaximoBytes] : resposta;
        var fim = bytes.IndexOf("\r\n\r\n"u8);
        var cabecalho = Encoding.Latin1.GetString(fim >= 0 ? bytes[..fim] : bytes);
        var corpo = fim >= 0 ? bytes[(fim + 4)..] : [];
        var linhas = cabecalho.Split("\r\n");

        int? codigo = null;
        var primeira = linhas[0].Split(' ', 3);
        if (primeira.Length >= 2 && primeira[0].StartsWith("HTTP/", StringComparison.Ordinal)
            && int.TryParse(primeira[1], NumberStyles.None, CultureInfo.InvariantCulture, out var c))
        {
            codigo = c;
        }

        string? servidor = null, localizacao = null, tipo = null;
        foreach (var linha in linhas.Skip(1))
        {
            var dois = linha.IndexOf(':');
            if (dois <= 0)
            {
                continue;
            }

            var nome = linha[..dois].Trim();
            var valor = linha[(dois + 1)..].Trim();
            if (nome.Equals("Server", StringComparison.OrdinalIgnoreCase))
            {
                servidor = valor;
            }
            else if (nome.Equals("Location", StringComparison.OrdinalIgnoreCase))
            {
                localizacao = valor;
            }
            else if (nome.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                tipo = valor;
            }
        }

        var texto = Decodificar(corpo, tipo);
        return new Dados(codigo, TextoRede.Limpar(servidor), TextoRede.Limpar(localizacao), Titulo(texto), texto);
    }

    /// <summary>Título da página, com as entidades HTML resolvidas.</summary>
    public static string? Titulo(string? html)
    {
        if (html is null)
        {
            return null;
        }

        var m = TagTitulo().Match(html);
        return m.Success ? TextoRede.Limpar(WebUtility.HtmlDecode(m.Groups[1].Value)) : null;
    }

    /// <summary>Usa o charset do cabeçalho, depois o da página, e por fim UTF-8.</summary>
    private static string Decodificar(ReadOnlySpan<byte> corpo, string? tipo)
    {
        var nome = CharsetDe(tipo);
        if (nome is null)
        {
            var inicio = Encoding.Latin1.GetString(corpo[..Math.Min(corpo.Length, 2048)]);
            var meta = MetaCharset().Match(inicio);
            nome = meta.Success ? meta.Groups[1].Value : null;
        }

        var codificacao = Encoding.UTF8;
        if (nome != null)
        {
            try
            {
                codificacao = nome.Equals("iso-8859-1", StringComparison.OrdinalIgnoreCase) || nome.Equals("latin1", StringComparison.OrdinalIgnoreCase)
                    ? Encoding.Latin1
                    : Encoding.GetEncoding(nome);
            }
            catch (ArgumentException)
            {
                // Charset que o .NET não conhece: fica o UTF-8.
            }
        }

        return codificacao.GetString(corpo);
    }

    private static string? CharsetDe(string? tipo)
    {
        if (tipo is null)
        {
            return null;
        }

        var m = CharsetCabecalho().Match(tipo);
        return m.Success ? m.Groups[1].Value : null;
    }

    [GeneratedRegex(@"<title[^>]*>(.*?)</title\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TagTitulo();

    [GeneratedRegex(@"<meta[^>]+charset\s*=\s*[""']?([A-Za-z0-9_\-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex MetaCharset();

    [GeneratedRegex(@"charset\s*=\s*[""']?([A-Za-z0-9_\-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex CharsetCabecalho();
}

/// <summary>Resposta de um equipamento ao M-SEARCH do UPnP.</summary>
public sealed record RespostaSsdp(IPAddress Origem, string? Servidor, string? Localizacao);

/// <summary>UPnP: o pedido M-SEARCH, a leitura das respostas e a descrição XML do equipamento.</summary>
public static class Ssdp
{
    public static readonly IPEndPoint Grupo = new(IPAddress.Parse("239.255.255.250"), 1900);

    public static byte[] Pedido() => Encoding.ASCII.GetBytes(
        "M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp:discover\"\r\nMX: 2\r\nST: upnp:rootdevice\r\n\r\n");

    public static RespostaSsdp? Interpretar(IPAddress origem, ReadOnlySpan<byte> pacote)
    {
        var texto = Encoding.Latin1.GetString(pacote);
        var linhas = texto.Split("\r\n");
        if (!linhas[0].StartsWith("HTTP/1.1 200", StringComparison.Ordinal))
        {
            return null;
        }

        string? servidor = null, local = null;
        foreach (var linha in linhas.Skip(1))
        {
            var dois = linha.IndexOf(':');
            if (dois <= 0)
            {
                continue;
            }

            var nome = linha[..dois].Trim();
            var valor = linha[(dois + 1)..].Trim();
            if (nome.Equals("SERVER", StringComparison.OrdinalIgnoreCase))
            {
                servidor = TextoRede.Limpar(valor);
            }
            else if (nome.Equals("LOCATION", StringComparison.OrdinalIgnoreCase))
            {
                local = valor;
            }
        }

        return new RespostaSsdp(origem, servidor, local);
    }

    /// <summary>
    /// Endereço da descrição, só quando aponta para o próprio equipamento que respondeu, por http.
    /// Assim a leitura nunca vai a outro host.
    /// </summary>
    public static Uri? DescricaoDoProprio(RespostaSsdp r) =>
        Uri.TryCreate(r.Localizacao, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttp
        && IPAddress.TryParse(uri.Host, out var ip) && ip.Equals(r.Origem)
            ? uri
            : null;

    /// <summary>"Nome amigável (fabricante modelo)" da descrição UPnP. DTD é recusado, o que barra entidade externa.</summary>
    public static string? Modelo(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        var ajustes = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = RespostaHttp.MaximoBytes };
        string? amigavel = null, fabricante = null, modelo = null;
        try
        {
            using var leitor = XmlReader.Create(new StringReader(xml), ajustes);
            leitor.Read();
            while (!leitor.EOF)
            {
                // ReadElementContentAsString já anda para o nó seguinte, então não chama Read depois dele.
                if (leitor.NodeType == XmlNodeType.Element && leitor.LocalName == "friendlyName" && amigavel is null)
                {
                    amigavel = TextoRede.Limpar(leitor.ReadElementContentAsString());
                }
                else if (leitor.NodeType == XmlNodeType.Element && leitor.LocalName == "manufacturer" && fabricante is null)
                {
                    fabricante = TextoRede.Limpar(leitor.ReadElementContentAsString());
                }
                else if (leitor.NodeType == XmlNodeType.Element && leitor.LocalName == "modelName" && modelo is null)
                {
                    modelo = TextoRede.Limpar(leitor.ReadElementContentAsString());
                }
                else
                {
                    leitor.Read();
                }
            }
        }
        catch (XmlException)
        {
            return null;
        }

        var complemento = string.Join(' ', new[] { fabricante, modelo }.Where(s => s != null));
        return TextoRede.Limpar(amigavel is null ? complemento : complemento.Length > 0 ? $"{amigavel} ({complemento})" : amigavel);
    }
}

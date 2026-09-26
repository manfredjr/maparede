using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace MapNet.Nucleo;

/// <summary>Formatos em que o relatório pode ser gravado.</summary>
public enum FormatoRelatorio
{
    Html,
    Xml,
    Csv,
}

/// <summary>Escolhe o formato pela extensão do arquivo e grava.</summary>
public static class Relatorios
{
    public static string Extensao(FormatoRelatorio formato) => formato switch
    {
        FormatoRelatorio.Xml => ".xml",
        FormatoRelatorio.Csv => ".csv",
        _ => ".html",
    };

    /// <summary>.xml e .csv pelo nome; qualquer outra extensão vira HTML.</summary>
    public static FormatoRelatorio FormatoDe(string caminho) => Path.GetExtension(caminho).ToLowerInvariant() switch
    {
        ".xml" => FormatoRelatorio.Xml,
        ".csv" => FormatoRelatorio.Csv,
        _ => FormatoRelatorio.Html,
    };

    /// <summary>Nome sugerido, como mapnet-20260926-1430-192-0-2-0-24.csv.</summary>
    public static string NomeArquivo(ResultadoVarredura r, FormatoRelatorio formato) =>
        Path.ChangeExtension(RelatorioHtml.NomeArquivo(r), Extensao(formato));

    public static async Task<string> SalvarAsync(ResultadoVarredura r, string caminho, CancellationToken cancelamento = default)
    {
        var formato = FormatoDe(caminho);
        if (formato == FormatoRelatorio.Html)
        {
            return await RelatorioHtml.SalvarAsync(r, caminho, cancelamento).ConfigureAwait(false);
        }

        var pasta = Path.GetDirectoryName(Path.GetFullPath(caminho));
        if (!string.IsNullOrEmpty(pasta))
        {
            Directory.CreateDirectory(pasta);
        }

        // CSV com BOM, para o Excel abrir os acentos certos; XML sem, como é o costume.
        var (texto, codificacao) = formato == FormatoRelatorio.Csv
            ? (RelatorioCsv.Gerar(r), new UTF8Encoding(true))
            : (RelatorioXml.Gerar(r), new UTF8Encoding(false));
        await File.WriteAllTextAsync(caminho, texto, codificacao, cancelamento).ConfigureAwait(false);
        return Path.GetFullPath(caminho);
    }
}

/// <summary>
/// Relatório em CSV, uma linha por host, para abrir em planilha. Separador ponto e vírgula, que é
/// o que o Excel em português espera. Campo que começa com =, +, - ou @ ganha um apóstrofo na
/// frente, para a planilha não tratar texto vindo da rede como fórmula.
/// </summary>
public static class RelatorioCsv
{
    public const char Separador = ';';

    public static readonly string[] Colunas =
    [
        "IP", "Nome", "Origem do nome", "MAC", "Fabricante", "Tipo provável", "Motivos do tipo", "Ping (ms)", "TTL",
        "Portas abertas", "Serviço", "Serviços", "Observação", "Grupo NetBIOS",
    ];

    public static string Gerar(ResultadoVarredura r)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(Separador, Colunas.Select(Campo)));
        foreach (var h in r.Hosts.OrderBy(h => h.IpNumero))
        {
            var c = h.Classificacao;
            var valores = new[]
            {
                h.Ip.ToString(), h.Nome, h.OrigemNome, h.MacTexto, h.Fabricante,
                c.Tipo == TipoEquipamento.Desconhecido ? string.Empty : c.Nome, string.Join(", ", c.Motivos),
                h.TempoPingMs?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, h.Ttl?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                h.PortasTexto, h.ServicoResumo, string.Join(" | ", h.Servicos.Select(s => s.Texto)),
                string.Join(", ", h.Marcas), h.GrupoNetBios ?? string.Empty,
            };
            sb.AppendLine(string.Join(Separador, valores.Select(Campo)));
        }

        return sb.ToString();
    }

    /// <summary>Aspas quando precisa, aspas dobradas por dentro e o apóstrofo contra fórmula.</summary>
    public static string Campo(string? valor)
    {
        var v = (valor ?? string.Empty).Replace("\r", " ").Replace("\n", " ");
        if (v.Length > 0 && v[0] is '=' or '+' or '-' or '@' or '\t')
        {
            v = "'" + v;
        }

        return v.IndexOfAny([Separador, '"', ',']) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
    }
}

/// <summary>
/// Relatório em XML, com tudo o que o HTML mostra, para outro programa ler. Caractere que o XML não
/// aceita é tirado antes, porque nome de equipamento pode trazer qualquer coisa.
/// </summary>
public static class RelatorioXml
{
    public const string Versao = "1";

    public static string Gerar(ResultadoVarredura r)
    {
        var i = r.Interface;
        var raiz = new XElement("inventario",
            new XAttribute("formato", Versao),
            new XAttribute("programa", $"MapNet - MT {ResultadoVarredura.VersaoPrograma}"),
            new XElement("varredura",
                A("inicio", r.Inicio.ToString("o", CultureInfo.InvariantCulture)),
                A("fim", r.Fim.ToString("o", CultureInfo.InvariantCulture)),
                A("duracaoSegundos", ((int)r.Duracao.TotalSeconds).ToString(CultureInfo.InvariantCulture)),
                A("interrompida", r.Cancelada ? "sim" : "nao"),
                A("computador", r.NomeComputador),
                A("subRede", r.SubRedeVarrida.ToString()),
                A("interface", i.Nome),
                A("ip", i.Ip.ToString()),
                A("gateway", i.Gateway?.ToString()),
                A("dns", string.Join(" ", i.Dns)),
                A("ipPublico", r.IpPublico),
                A("portasVerificadas", r.PortasVerificadas is { } p ? string.Join(" ", p) : null),
                A("identificacaoDeServicos", r.IdentificacaoFeita ? "sim" : "nao")),
            new XElement("avisos", r.Avisos.Select(a => new XElement("aviso", T(a)))));

        if (r.Maquina is { } maquina)
        {
            raiz.Add(new XElement("maquina", maquina.Grupos(r.Fim, r.IpPublico).Select(g =>
                new XElement("grupo", A("titulo", g.Titulo), g.Itens.Select(it => new XElement("item", A("rotulo", it.Rotulo), T(it.Valor)))))));
        }

        raiz.Add(new XElement("hosts", r.Hosts.OrderBy(h => h.IpNumero).Select(Host)));
        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), raiz);

        var sb = new StringBuilder();
        using (var escritor = XmlWriter.Create(new StringWriterUtf8(sb), new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) }))
        {
            doc.Save(escritor);
        }

        return sb.ToString();
    }

    private static XElement Host(HostEncontrado h)
    {
        var c = h.Classificacao;
        return new XElement("host",
            A("ip", h.Ip.ToString()),
            A("mac", h.MacTexto),
            A("fabricante", h.Fabricante),
            A("macAleatorio", h.MacAleatorio ? "sim" : null),
            A("gateway", h.EhGateway ? "sim" : null),
            A("esteComputador", h.EhEsteComputador ? "sim" : null),
            new XElement("nomes",
                A("exibido", h.Nome), A("origem", h.OrigemNome), A("dns", h.NomeDns), A("netbios", h.NomeNetBios), A("grupo", h.GrupoNetBios), A("mdns", h.NomeMdns)),
            new XElement("ping", A("respondeu", h.RespondeuPing ? "sim" : "nao"), A("ms", h.TempoPingMs?.ToString(CultureInfo.InvariantCulture)), A("ttl", h.Ttl?.ToString(CultureInfo.InvariantCulture))),
            new XElement("arp", A("respondeu", h.RespondeuArp ? "sim" : "nao")),
            new XElement("tipo", A("nome", c.Nome), c.Motivos.Select(m => new XElement("motivo", T(m)))),
            new XElement("portas",
                A("verificadas", h.PortasVerificadas ? "sim" : "nao"),
                A("motivo", h.MotivoSemPortas),
                h.PortasAbertas.Select(p => new XElement("porta", A("numero", p.ToString(CultureInfo.InvariantCulture)), A("servico", ListaPortas.Servico(p))))),
            new XElement("servicos", h.Servicos.Select(s => new XElement("servico",
                A("porta", s.Porta > 0 ? s.Porta.ToString(CultureInfo.InvariantCulture) : null),
                A("protocolo", s.Protocolo),
                A("codigoHttp", s.CodigoHttp?.ToString(CultureInfo.InvariantCulture)),
                A("titulo", s.Titulo),
                A("servidor", s.Servidor),
                A("redireciona", s.Redireciona),
                A("certificado", s.CertificadoNome),
                A("emissor", s.CertificadoEmissor),
                A("validade", s.CertificadoValidade?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                A("banner", s.Banner),
                A("modelo", s.Modelo)))),
            new XElement("marcas", h.Marcas.Select(m => new XElement("marca", T(m)))));
    }

    /// <summary>Atributo só quando há valor, já sem caractere que o XML recusa.</summary>
    private static XAttribute? A(string nome, string? valor) =>
        string.IsNullOrEmpty(valor) ? null : new XAttribute(nome, T(valor));

    /// <summary>Tira o caractere que o XML 1.0 não aceita.</summary>
    public static string T(string? texto)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(texto.Length);
        for (var k = 0; k < texto.Length; k++)
        {
            var c = texto[k];
            if (char.IsHighSurrogate(c) && k + 1 < texto.Length && char.IsLowSurrogate(texto[k + 1]))
            {
                sb.Append(c).Append(texto[++k]);
            }
            else if (XmlConvert.IsXmlChar(c))
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    /// <summary>StringWriter que declara UTF-8, para o cabeçalho do XML sair certo.</summary>
    private sealed class StringWriterUtf8(StringBuilder sb) : StringWriter(sb, CultureInfo.InvariantCulture)
    {
        public override Encoding Encoding => new UTF8Encoding(false);
    }
}

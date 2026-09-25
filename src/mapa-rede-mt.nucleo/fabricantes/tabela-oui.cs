using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace MapaRedeMt.Nucleo;

/// <summary>
/// Tabela OUI do IEEE: os três primeiros bytes do MAC dizem quem fabricou a placa de rede.
/// A tabela vai embutida no programa (dados/oui.txt.gz) e não depende de internet.
/// </summary>
public sealed class TabelaOui
{
    public const string TextoMacAleatorio = "MAC aleatório (privativo)";
    public const string TextoNaoIdentificado = "Não identificado";

    private static readonly Lazy<TabelaOui> _embutida = new(CarregarEmbutida);

    private readonly Dictionary<int, string> _itens;

    private TabelaOui(Dictionary<int, string> itens) => _itens = itens;

    /// <summary>Tabela que vem dentro do programa, carregada na primeira vez que é usada.</summary>
    public static TabelaOui Embutida => _embutida.Value;

    public int Quantidade => _itens.Count;

    /// <summary>
    /// Lê a tabela em dois formatos: o compacto do programa ("AABBCC", TAB, organização) e o
    /// oui.txt original do IEEE (linhas com "(base 16)"). Linhas que não casam são ignoradas.
    /// </summary>
    public static TabelaOui Ler(TextReader leitor)
    {
        var itens = new Dictionary<int, string>();
        string? linha;
        while ((linha = leitor.ReadLine()) != null)
        {
            if (linha.Length < 7 || linha[0] == '#')
            {
                continue;
            }

            if (!int.TryParse(linha.AsSpan(0, 6), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var prefixo))
            {
                continue;
            }

            string resto;
            var marcaIeee = linha.IndexOf("(base 16)", StringComparison.Ordinal);
            if (marcaIeee >= 0)
            {
                resto = linha[(marcaIeee + "(base 16)".Length)..];
            }
            else if (linha[6] == '\t')
            {
                resto = linha[7..];
            }
            else
            {
                continue;
            }

            var nome = resto.Trim();
            if (nome.Length > 0)
            {
                itens.TryAdd(prefixo, nome);
            }
        }

        return new TabelaOui(itens);
    }

    public static TabelaOui CarregarEmbutida()
    {
        using var recurso = typeof(TabelaOui).Assembly.GetManifestResourceStream("oui.txt.gz")
            ?? throw new InvalidOperationException("A tabela OUI embutida não foi encontrada no programa.");
        using var descompactado = new GZipStream(recurso, CompressionMode.Decompress);
        using var leitor = new StreamReader(descompactado, Encoding.UTF8);
        return Ler(leitor);
    }

    /// <summary>Nome da organização dona do prefixo, ou null quando não consta.</summary>
    public string? Buscar(ReadOnlySpan<byte> mac)
    {
        if (mac.Length < 3)
        {
            return null;
        }

        var prefixo = mac[0] << 16 | mac[1] << 8 | mac[2];
        return _itens.TryGetValue(prefixo, out var nome) ? nome : null;
    }

    /// <summary>Texto para o relatório: fabricante, MAC aleatório ou não identificado.</summary>
    public string DescreverFabricante(byte[]? mac)
    {
        if (mac is not { Length: > 0 })
        {
            return string.Empty;
        }

        return Buscar(mac)
            ?? (EnderecoMac.AdministradoLocalmente(mac) ? TextoMacAleatorio : TextoNaoIdentificado);
    }
}

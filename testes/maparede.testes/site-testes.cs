using System.Text.RegularExpressions;

namespace MapaRede.Testes;

/// <summary>
/// A pasta public/ é a raiz do site maparede.manfred.com.br e tudo nela fica público.
/// Estes testes barram o que não for arquivo de site e conferem que a página aponta para
/// o executável com o nome que o projeto gera.
/// </summary>
public partial class SiteTestes
{
    private static readonly string[] _extensoesDeSite = [".html", ".css", ".js", ".svg", ".png", ".ico", ".txt", ".webmanifest"];

    [Fact]
    public void Public_so_tem_arquivo_de_site()
    {
        var publico = Path.Combine(CaracteresProibidosTestes.RaizDoRepositorio(), "public");
        var fora = Directory.EnumerateFiles(publico, "*", SearchOption.AllDirectories)
            .Where(a => !_extensoesDeSite.Contains(Path.GetExtension(a).ToLowerInvariant()))
            .Select(a => Path.GetRelativePath(publico, a))
            .ToList();

        Assert.True(fora.Count == 0, "Arquivo que não é de site em public/: " + string.Join(", ", fora));
    }

    [Fact]
    public void Link_de_download_usa_o_nome_do_executavel()
    {
        var raiz = CaracteresProibidosTestes.RaizDoRepositorio();
        var projeto = File.ReadAllText(Path.Combine(raiz, "src", "maparede", "maparede.csproj"));
        var nome = NomeDoAssembly().Match(projeto).Groups[1].Value + ".exe";
        var pagina = File.ReadAllText(Path.Combine(raiz, "public", "index.html"));

        Assert.Equal("maparede.exe", nome);
        Assert.Contains($"https://github.com/manfredjr/maparede/releases/latest/download/{nome}", pagina);
    }

    [Fact]
    public void Deploy_confere_os_arquivos_que_existem()
    {
        var raiz = CaracteresProibidosTestes.RaizDoRepositorio();
        var deploy = File.ReadAllText(Path.Combine(raiz, ".cpanel.yml"));
        var conferidos = ArquivoConferido().Matches(deploy).Select(m => m.Groups[1].Value).ToList();

        Assert.NotEmpty(conferidos);
        Assert.All(conferidos, a => Assert.True(File.Exists(Path.Combine(raiz, a)), $"O .cpanel.yml confere {a}, que não existe."));
    }

    [GeneratedRegex("<AssemblyName>([^<]+)</AssemblyName>")]
    private static partial Regex NomeDoAssembly();

    [GeneratedRegex(@"test -f \$REPO/(\S+)")]
    private static partial Regex ArquivoConferido();
}

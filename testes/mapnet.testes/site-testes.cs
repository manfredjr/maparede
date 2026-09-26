using System.Text.RegularExpressions;

namespace MapNet.Testes;

/// <summary>
/// A pasta public/ é a raiz do site mapnet.manfred.com.br e tudo nela fica público.
/// Estes testes barram o que não for arquivo de site e conferem que a página aponta para
/// o executável com o nome que o projeto gera.
/// </summary>
public partial class SiteTestes
{
    private static readonly string[] _extensoesDeSite = [".html", ".css", ".js", ".svg", ".png", ".webp", ".ico", ".woff2", ".txt", ".webmanifest"];

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
        var projeto = File.ReadAllText(Path.Combine(raiz, "src", "mapnet", "mapnet.csproj"));
        var nome = NomeDoAssembly().Match(projeto).Groups[1].Value + ".exe";
        var pagina = File.ReadAllText(Path.Combine(raiz, "public", "index.html"));

        Assert.Equal("mapnet.exe", nome);
        Assert.Contains($"https://github.com/manfredjr/mapnet/releases/latest/download/{nome}", pagina);
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

    [Fact]
    public void Links_internos_das_paginas_apontam_para_arquivos_que_existem()
    {
        var publico = Path.Combine(CaracteresProibidosTestes.RaizDoRepositorio(), "public");
        var quebrados = new List<string>();
        foreach (var pagina in Directory.EnumerateFiles(publico, "*.html"))
        {
            foreach (Match m in LinkLocal().Matches(File.ReadAllText(pagina)))
            {
                var alvo = m.Groups[1].Value.Split('#', '?')[0];
                if (alvo is "" or "./")
                {
                    continue;
                }

                if (!File.Exists(Path.Combine(publico, alvo)))
                {
                    quebrados.Add($"{Path.GetFileName(pagina)}: {alvo}");
                }
            }
        }

        Assert.True(quebrados.Count == 0, "Link interno quebrado: " + string.Join(", ", quebrados));
    }

    [GeneratedRegex(@"(?:href|src)=""(?!https?:|mailto:|#)([^""]+)""")]
    private static partial Regex LinkLocal();

    [GeneratedRegex("<AssemblyName>([^<]+)</AssemblyName>")]
    private static partial Regex NomeDoAssembly();

    [GeneratedRegex(@"test -f \$REPO/(\S+)")]
    private static partial Regex ArquivoConferido();
}

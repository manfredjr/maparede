using System.Text.RegularExpressions;

namespace MapNet.Testes;

/// <summary>
/// Arquivos da tela WPF que não dá para abrir no Linux, conferidos pelo conteúdo: tema com as
/// cores da MT, fonte embutida com a licença, versão do manifesto e nada de WinForms.
/// </summary>
public partial class TelaTestes
{
    private static readonly string _app = Path.Combine(CaracteresProibidosTestes.RaizDoRepositorio(), "src", "mapnet");

    [Theory]
    [InlineData("#006B2D")]
    [InlineData("#0F8F2F")]
    [InlineData("#43A92C")]
    [InlineData("#9AD52B")]
    [InlineData("#202020")]
    [InlineData("#F4F4F4")]
    public void Tema_usa_as_cores_oficiais_da_mt(string cor)
    {
        var tema = File.ReadAllText(Path.Combine(_app, "tema", "tema-mt.xaml"));

        Assert.Contains(cor, tema, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Tema_nao_corta_texto_com_reticencias_de_um_caractere()
    {
        var tema = File.ReadAllText(Path.Combine(_app, "tema", "tema-mt.xaml"));
        var janela = File.ReadAllText(Path.Combine(_app, "janela-principal.xaml"));

        Assert.DoesNotContain("Ellipsis", tema);
        Assert.DoesNotContain("Ellipsis", janela);
    }

    [Theory]
    [InlineData("montserrat-regular.ttf")]
    [InlineData("montserrat-semibold.ttf")]
    [InlineData("montserrat-extrabold.ttf")]
    public void Fonte_montserrat_vai_embutida(string arquivo)
    {
        var caminho = Path.Combine(_app, "recursos", "fontes", arquivo);
        var dados = File.ReadAllBytes(caminho);
        var projeto = File.ReadAllText(Path.Combine(_app, "mapnet.csproj"));

        // TrueType começa com 00 01 00 00. O WPF não lê WOFF2.
        Assert.Equal([0x00, 0x01, 0x00, 0x00], dados[..4]);
        Assert.Contains(@"<Resource Include=""recursos\fontes\*.ttf"" />", projeto);
    }

    [Fact]
    public void Licenca_da_fonte_vai_junto()
    {
        var licenca = File.ReadAllText(Path.Combine(_app, "recursos", "fontes", "ofl.txt"));
        var projeto = File.ReadAllText(Path.Combine(_app, "mapnet.csproj"));

        Assert.Contains("SIL OPEN FONT LICENSE Version 1.1", licenca);
        Assert.Contains(@"<Resource Include=""recursos\fontes\ofl.txt"" />", projeto);
    }

    [Fact]
    public void Manifesto_tem_a_mesma_versao_do_programa()
    {
        var raiz = CaracteresProibidosTestes.RaizDoRepositorio();
        var props = File.ReadAllText(Path.Combine(raiz, "Directory.Build.props"));
        var manifesto = File.ReadAllText(Path.Combine(_app, "app.manifest"));

        var versao = VersaoNoProps().Match(props).Groups[1].Value;

        Assert.NotEmpty(versao);
        Assert.Contains($"version=\"{versao}.0\"", manifesto);
    }

    [Fact]
    public void Aplicativo_nao_usa_mais_winforms()
    {
        var projeto = File.ReadAllText(Path.Combine(_app, "mapnet.csproj"));
        var fontes = Directory.EnumerateFiles(_app, "*.cs", SearchOption.AllDirectories)
            .Where(c => !c.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !c.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

        Assert.Contains("<UseWPF>true</UseWPF>", projeto);
        Assert.DoesNotContain("UseWindowsForms", projeto);
        Assert.All(fontes, c => Assert.DoesNotContain("System.Windows.Forms", File.ReadAllText(c)));
    }

    [GeneratedRegex(@"<Version>([0-9.]+)</Version>")]
    private static partial Regex VersaoNoProps();
}

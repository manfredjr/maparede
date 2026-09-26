using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MapNet.Testes;

/// <summary>
/// Trava da regra "o que nunca vai para o GitHub" do AGENTS.md. O repositório é público, então
/// confere, no que está no git: documentação e página sem IP privado nem MAC (sinal de dado de
/// rede real), nenhum relatório gerado e imagem só nas pastas que recebem imagem de exemplo.
/// Os testes e o código podem usar IP privado, porque conferem o cálculo de sub-redes privadas.
/// </summary>
public partial class DadosReaisTestes
{
    private static readonly string[] _pastasDeImagem = ["docs/superpowers/specs/img/", "public/", "src/mapnet/recursos/"];

    private static readonly string[] _extensoesDeImagem = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp"];

    [Fact]
    public void Documentacao_e_pagina_nao_tem_ip_privado_nem_mac()
    {
        var raiz = CaracteresProibidosTestes.RaizDoRepositorio();
        var problemas = new List<string>();
        foreach (var relativo in ArquivosNoGit().Where(EhDocumentacao))
        {
            var linhas = File.ReadAllLines(Path.Combine(raiz, relativo));
            for (var i = 0; i < linhas.Length; i++)
            {
                problemas.AddRange(IpPrivado().Matches(linhas[i]).Select(m => $"{relativo}:{i + 1}: IP privado {m.Value}"));
                problemas.AddRange(Mac().Matches(linhas[i]).Select(m => $"{relativo}:{i + 1}: MAC {m.Value}"));
            }
        }

        Assert.True(problemas.Count == 0,
            "Dado que parece de rede real na documentação (use 192.0.2.x, 198.51.100.x ou 203.0.113.x):" + Environment.NewLine
            + string.Join(Environment.NewLine, problemas));
    }

    [Fact]
    public void Nenhum_relatorio_gerado_esta_no_git()
    {
        var relatorios = ArquivosNoGit().Where(c => Relatorio().IsMatch(Path.GetFileName(c))).ToList();

        Assert.True(relatorios.Count == 0, string.Join(Environment.NewLine, relatorios));
    }

    [Fact]
    public void Imagem_so_nas_pastas_de_imagem_de_exemplo()
    {
        var fora = ArquivosNoGit()
            .Where(c => _extensoesDeImagem.Contains(Path.GetExtension(c).ToLowerInvariant()))
            .Where(c => !_pastasDeImagem.Any(p => c.StartsWith(p, StringComparison.Ordinal)))
            .ToList();

        Assert.True(fora.Count == 0, "Imagem fora das pastas permitidas (print de tela real não vai para o GitHub):"
            + Environment.NewLine + string.Join(Environment.NewLine, fora));
    }

    [Theory]
    [InlineData("rede 192.168.1.82/24", true)]
    [InlineData("gateway 10.0.0.1", true)]
    [InlineData("172.16.5.4 e 172.31.0.9", true)]
    [InlineData("192.0.2.1, 198.51.100.7 e 203.0.113.45", false)]
    [InlineData("versão 10.2.3", false)]
    [InlineData("172.32.0.1", false)]
    [InlineData("1.10.0.0.1", false)]
    public void Reconhece_ip_privado(string texto, bool privado)
    {
        Assert.Equal(privado, IpPrivado().IsMatch(texto));
    }

    [Theory]
    [InlineData("MAC 70:A8:D3:00:11:22", true)]
    [InlineData("MAC 70-A8-D3-00-11-22", true)]
    [InlineData("hora 14:02:11", false)]
    public void Reconhece_mac(string texto, bool mac)
    {
        Assert.Equal(mac, Mac().IsMatch(texto));
    }

    private static bool EhDocumentacao(string relativo) =>
        relativo.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
        || (relativo.StartsWith("public/", StringComparison.Ordinal) && Path.GetExtension(relativo) is ".html" or ".txt" or ".md");

    /// <summary>Arquivos que estão no git. Sem git (pacote do código solto), cai para o disco.</summary>
    private static IReadOnlyList<string> ArquivosNoGit()
    {
        var raiz = CaracteresProibidosTestes.RaizDoRepositorio();
        try
        {
            using var git = Process.Start(new ProcessStartInfo("git", "ls-files -z")
            {
                WorkingDirectory = raiz,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (git != null)
            {
                var saida = git.StandardOutput.ReadToEnd();
                git.WaitForExit();
                if (git.ExitCode == 0)
                {
                    return saida.Split('\0', StringSplitOptions.RemoveEmptyEntries);
                }
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Sem git instalado.
        }

        return Directory.EnumerateFiles(raiz, "*", SearchOption.AllDirectories)
            .Select(c => Path.GetRelativePath(raiz, c).Replace('\\', '/'))
            .Where(c => !c.Split('/').Any(p => p is "bin" or "obj" or ".git" or ".vs" or "publicar" or ".superpowers" or "TestResults"))
            .ToList();
    }

    [GeneratedRegex(@"(?<![\d.])(10\.\d{1,3}\.\d{1,3}\.\d{1,3}|172\.(1[6-9]|2\d|3[01])\.\d{1,3}\.\d{1,3}|192\.168\.\d{1,3}\.\d{1,3})(?![\d.]*\d)")]
    private static partial Regex IpPrivado();

    [GeneratedRegex(@"(?<![0-9A-Fa-f:-])([0-9A-Fa-f]{2}[:-]){5}[0-9A-Fa-f]{2}(?![0-9A-Fa-f:-])")]
    private static partial Regex Mac();

    [GeneratedRegex(@"^mapnet-\d{8}-\d{4}-.*\.(html|xml|csv)$", RegexOptions.IgnoreCase)]
    private static partial Regex Relatorio();
}

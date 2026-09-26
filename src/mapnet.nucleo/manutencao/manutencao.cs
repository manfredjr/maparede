using System.Diagnostics;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MapNet.Nucleo;

/// <summary>Ações de manutenção. A lista é fechada: nenhum texto da tela vira comando.</summary>
public enum AcaoManutencao
{
    LimparCacheDns,
    RenovarIp,
    LimparArp,
    ResetarRede,
}

/// <summary>Resultado de uma ação: cancelada no UAC, ou o código e a saída dos comandos.</summary>
public sealed record ResultadoManutencao(bool Cancelada, int Codigo, string Saida);

/// <summary>Os comandos oficiais do Windows de cada ação, e o que o técnico precisa saber antes.</summary>
public static class Manutencao
{
    public static string Titulo(AcaoManutencao acao) => acao switch
    {
        AcaoManutencao.LimparCacheDns => "Limpar cache DNS",
        AcaoManutencao.RenovarIp => "Liberar e renovar IP",
        AcaoManutencao.LimparArp => "Limpar tabela ARP",
        AcaoManutencao.ResetarRede => "Resetar Winsock e TCP/IP",
        _ => throw new ArgumentOutOfRangeException(nameof(acao)),
    };

    /// <summary>Programa e argumentos, fixos no código.</summary>
    public static IReadOnlyList<(string Programa, string Argumentos)> Comandos(AcaoManutencao acao) => acao switch
    {
        AcaoManutencao.LimparCacheDns => [("ipconfig.exe", "/flushdns")],
        AcaoManutencao.RenovarIp => [("ipconfig.exe", "/release"), ("ipconfig.exe", "/renew")],
        AcaoManutencao.LimparArp => [("netsh.exe", "interface ip delete arpcache")],
        AcaoManutencao.ResetarRede => [("netsh.exe", "winsock reset"), ("netsh.exe", "int ip reset")],
        _ => throw new ArgumentOutOfRangeException(nameof(acao)),
    };

    /// <summary>
    /// O ipconfig /flushdns roda como usuário comum no Windows 10 e 11. As outras mexem na
    /// configuração da rede e pedem administrador.
    /// </summary>
    public static bool PedeAdministrador(AcaoManutencao acao) => acao != AcaoManutencao.LimparCacheDns;

    /// <summary>Texto da confirmação, para as ações que derrubam a rede. Null quando não precisa.</summary>
    public static string? Confirmacao(AcaoManutencao acao) => acao switch
    {
        AcaoManutencao.RenovarIp =>
            "Liberar e renovar o IP derruba a conexão de todas as placas por alguns segundos, até o DHCP entregar o endereço de novo. Continuar?",
        AcaoManutencao.ResetarRede =>
            "O reset do Winsock e da pilha TCP/IP volta a configuração de rede do Windows ao padrão e só vale depois de reiniciar o computador. Configuração manual de IP pode se perder. Continuar?",
        _ => null,
    };

    /// <summary>Linha de comando como o técnico digitaria, para o console.</summary>
    public static string TextoComandos(AcaoManutencao acao) =>
        string.Join(" e ", Comandos(acao).Select(c => $"{Path.GetFileNameWithoutExtension(c.Programa)} {c.Argumentos}"));

    /// <summary>Roda os comandos da ação em sequência, com a saída na página de código OEM do Windows.</summary>
    public static async Task<(int Codigo, string Saida)> RodarAsync(AcaoManutencao acao, CancellationToken cancelamento)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var codificacao = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        var saida = new StringBuilder();
        var codigo = 0;
        foreach (var (programa, argumentos) in Comandos(acao))
        {
            var inicio = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, programa), argumentos)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = codificacao,
                StandardErrorEncoding = codificacao,
            };
            using var processo = Process.Start(inicio) ?? throw new InvalidOperationException($"não foi possível abrir o {programa}");
            var texto = processo.StandardOutput.ReadToEndAsync(cancelamento);
            var erro = processo.StandardError.ReadToEndAsync(cancelamento);
            await processo.WaitForExitAsync(cancelamento).ConfigureAwait(false);
            saida.AppendLine($"> {Path.GetFileNameWithoutExtension(programa)} {argumentos}");
            saida.Append(await texto.ConfigureAwait(false));
            saida.Append(await erro.ConfigureAwait(false));
            codigo = processo.ExitCode != 0 ? processo.ExitCode : codigo;
        }

        return (codigo, saida.ToString());
    }
}

/// <summary>Roda uma ação, com ou sem administrador. Os testes trocam por um simulado.</summary>
public interface IExecutorManutencao
{
    Task<ResultadoManutencao> ExecutarAsync(AcaoManutencao acao, CancellationToken cancelamento);
}

/// <summary>
/// Modo auxiliar do próprio mapnet.exe, aberto com elevação (mapnet.exe --auxiliar Acao arquivo).
/// Roda só a ação pedida, da lista fechada, grava a saída no arquivo e termina. Nenhuma outra
/// parte do programa roda como administrador.
/// </summary>
public static partial class Auxiliar
{
    public const string Argumento = "--auxiliar";

    /// <summary>Nome do arquivo de saída: mapnet-auxiliar- e um GUID, na pasta temporária.</summary>
    public static string NovoArquivo() => Path.Combine(Path.GetTempPath(), $"mapnet-auxiliar-{Guid.NewGuid():N}.txt");

    /// <summary>
    /// Confere os argumentos. Só passa ação da lista e arquivo com o nome no formato esperado,
    /// numa pasta que existe, sem ".." no caminho.
    /// </summary>
    public static bool Interpretar(IReadOnlyList<string> args, out AcaoManutencao acao, out string arquivo)
    {
        acao = default;
        arquivo = string.Empty;
        if (args.Count != 3 || args[0] != Argumento
            || !Enum.TryParse(args[1], ignoreCase: false, out acao) || !Enum.IsDefined(acao)
            || args[1].Any(char.IsDigit))
        {
            return false;
        }

        var caminho = args[2];
        if (caminho.Contains("..", StringComparison.Ordinal) || !Path.IsPathFullyQualified(caminho)
            || !NomeDoArquivo().IsMatch(Path.GetFileName(caminho))
            || !Directory.Exists(Path.GetDirectoryName(caminho)))
        {
            return false;
        }

        arquivo = caminho;
        return true;
    }

    /// <summary>Ponto de entrada do modo auxiliar. Devolve o código de saída do processo.</summary>
    public static int Executar(IReadOnlyList<string> args)
    {
        if (!Interpretar(args, out var acao, out var arquivo))
        {
            return 2;
        }

        try
        {
            var (codigo, saida) = Manutencao.RodarAsync(acao, CancellationToken.None).GetAwaiter().GetResult();
            File.WriteAllText(arquivo, saida, new UTF8Encoding(false));
            return codigo;
        }
        catch (Exception e)
        {
            File.WriteAllText(arquivo, $"Falha ao rodar a ação: {e.Message}", new UTF8Encoding(false));
            return 3;
        }
    }

    [GeneratedRegex("^mapnet-auxiliar-[0-9a-f]{32}\\.txt$")]
    private static partial Regex NomeDoArquivo();
}

/// <summary>
/// Executor de verdade. A ação que não pede administrador roda aqui mesmo. A que pede abre o
/// próprio mapnet.exe no modo auxiliar com "runas": o Windows mostra a tela do UAC.
/// </summary>
public sealed class ExecutorManutencaoWindows(string caminhoExe) : IExecutorManutencao
{
    private const int UacCancelado = 1223;

    public async Task<ResultadoManutencao> ExecutarAsync(AcaoManutencao acao, CancellationToken cancelamento)
    {
        if (!Manutencao.PedeAdministrador(acao))
        {
            var (codigo, saida) = await Manutencao.RodarAsync(acao, cancelamento).ConfigureAwait(false);
            return new ResultadoManutencao(false, codigo, saida);
        }

        var arquivo = Auxiliar.NovoArquivo();
        var inicio = new ProcessStartInfo(caminhoExe)
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        inicio.ArgumentList.Add(Auxiliar.Argumento);
        inicio.ArgumentList.Add(acao.ToString());
        inicio.ArgumentList.Add(arquivo);

        Process? processo;
        try
        {
            processo = Process.Start(inicio);
        }
        catch (Win32Exception e) when (e.NativeErrorCode == UacCancelado)
        {
            return new ResultadoManutencao(true, 0, string.Empty);
        }

        if (processo is null)
        {
            throw new InvalidOperationException("o Windows não abriu o modo auxiliar");
        }

        using (processo)
        {
            await processo.WaitForExitAsync(cancelamento).ConfigureAwait(false);
            var texto = File.Exists(arquivo) ? await File.ReadAllTextAsync(arquivo, cancelamento).ConfigureAwait(false) : string.Empty;
            TentarApagar(arquivo);
            return new ResultadoManutencao(false, processo.ExitCode, texto);
        }
    }

    private static void TentarApagar(string arquivo)
    {
        try
        {
            File.Delete(arquivo);
        }
        catch (IOException)
        {
            // Arquivo temporário: o Windows limpa depois.
        }
        catch (UnauthorizedAccessException)
        {
            // Idem.
        }
    }
}

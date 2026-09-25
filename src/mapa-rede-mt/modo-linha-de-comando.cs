using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using MapaRedeMt.Nucleo;

namespace MapaRedeMt;

/// <summary>
/// Modo linha de comando. O .exe é de janela (WinExe), então ele se liga ao console de quem
/// o chamou para escrever nele. No Prompt de Comando, use "start /wait" para o prompt esperar
/// o fim; no PowerShell, termine a linha com "| Out-Host".
/// </summary>
internal static partial class ModoLinhaDeComando
{
    private const int ConsoleDoPai = -1;

    public const int CodigoSucesso = 0;
    public const int CodigoArgumentos = 1;
    public const int CodigoSemInterface = 2;
    public const int CodigoFalha = 3;
    public const int CodigoCancelado = 4;

    public static int Executar(ArgumentosCli argumentos)
    {
        LigarConsole();
        try
        {
            return ExecutarAsync(argumentos).GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            // Com MAPA_REDE_DEPURAR=1 sai o rastro completo, para suporte.
            var depurar = Environment.GetEnvironmentVariable("MAPA_REDE_DEPURAR") == "1";
            Console.Error.WriteLine($"Erro: {(depurar ? e.ToString() : e.Message)}");
            return CodigoFalha;
        }
        finally
        {
            Console.Out.Flush();
        }
    }

    private static async Task<int> ExecutarAsync(ArgumentosCli argumentos)
    {
        Console.WriteLine();
        if (!argumentos.Valido)
        {
            foreach (var erro in argumentos.Erros)
            {
                Console.Error.WriteLine(erro);
            }

            Console.Error.WriteLine("Use --ajuda para ver as opções.");
            return CodigoArgumentos;
        }

        switch (argumentos.Comando)
        {
            case ComandoCli.Ajuda:
                Console.WriteLine(ArgumentosCli.TextoAjuda);
                return CodigoSucesso;
            case ComandoCli.Versao:
                Console.WriteLine($"MT Mapa de Rede {ResultadoVarredura.VersaoPrograma}");
                return CodigoSucesso;
            case ComandoCli.ListarInterfaces:
                return ListarInterfaces();
            case ComandoCli.Varrer:
                return await VarrerAsync(argumentos).ConfigureAwait(false);
            default:
                Console.WriteLine(ArgumentosCli.TextoAjuda);
                return CodigoArgumentos;
        }
    }

    private static int ListarInterfaces()
    {
        var interfaces = LeitorInterfaces.Listar();
        if (interfaces.Count == 0)
        {
            Console.Error.WriteLine("Nenhuma interface de rede ativa com IPv4 foi encontrada.");
            return CodigoSemInterface;
        }

        Console.WriteLine("Interfaces de rede ativas:");
        for (var i = 0; i < interfaces.Count; i++)
        {
            var n = interfaces[i];
            Console.WriteLine($"  {i + 1}. {n.Nome} ({n.TipoTexto}){(n.Virtual ? " [virtual]" : "")}");
            Console.WriteLine($"     {n.Descricao}");
            Console.WriteLine($"     IP {n.Ip}/{n.Prefixo}, sub-rede {n.SubRede}, gateway {n.Gateway?.ToString() ?? "nenhum"}");
        }

        return CodigoSucesso;
    }

    private static async Task<int> VarrerAsync(ArgumentosCli argumentos)
    {
        var interfaceRede = ArgumentosCli.EscolherInterface(LeitorInterfaces.Listar(), argumentos.Interface, out var erro);
        if (interfaceRede is null)
        {
            Console.Error.WriteLine(erro);
            return CodigoSemInterface;
        }

        using var cancelamento = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancelamento.Cancel();
            Console.WriteLine();
            Console.WriteLine("Interrompendo...");
        };

        var subRede = Varredor.SubRedeAVarrer(interfaceRede, argumentos.Opcoes.PrefixoMinimo, out _);
        Console.WriteLine($"Interface: {interfaceRede.Resumo}");
        Console.WriteLine($"Sub-rede:  {subRede} ({subRede.QuantidadeHosts} endereços)");
        Console.WriteLine();

        var varredor = new Varredor(argumentos.Opcoes);
        var resultado = await varredor.VarrerAsync(interfaceRede, new ProgressoConsole(), cancelamento.Token).ConfigureAwait(false);
        Console.WriteLine();

        foreach (var aviso in resultado.Avisos)
        {
            Console.WriteLine($"Aviso: {aviso}");
        }

        Console.WriteLine($"{resultado.Hosts.Count} hosts encontrados em {RelatorioHtml.Duracao(resultado.Duracao)}:");
        foreach (var h in resultado.Hosts)
        {
            var marcas = h.Marcas.Count > 0 ? $"  [{string.Join(", ", h.Marcas)}]" : string.Empty;
            Console.WriteLine($"  {h.Ip,-15}  {h.MacTexto,-17}  {Cortar(h.Fabricante, 28),-28}  {h.Nome}{marcas}");
        }

        var caminho = DefinirCaminho(argumentos.Saida, resultado);
        caminho = await RelatorioHtml.SalvarAsync(resultado, caminho).ConfigureAwait(false);
        Console.WriteLine();
        Console.WriteLine($"Relatório gravado em: {caminho}");

        if (argumentos.Abrir)
        {
            Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true });
        }

        return resultado.Cancelada ? CodigoCancelado : CodigoSucesso;
    }

    private static string DefinirCaminho(string? saida, ResultadoVarredura resultado)
    {
        var nome = RelatorioHtml.NomeArquivo(resultado);
        if (string.IsNullOrWhiteSpace(saida))
        {
            return Path.Combine(Environment.CurrentDirectory, nome);
        }

        return saida.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || saida.EndsWith(".htm", StringComparison.OrdinalIgnoreCase)
            ? saida
            : Path.Combine(saida, nome);
    }

    private static string Cortar(string texto, int tamanho) => texto.Length <= tamanho ? texto : texto[..(tamanho - 3)] + "...";

    /// <summary>
    /// Liga ao console de quem chamou. Se não houver (atalho com argumentos), cria um. Quando a
    /// saída já vem redirecionada para arquivo ou pipe, não mexe, para não perder o redirecionamento.
    /// </summary>
    private static void LigarConsole()
    {
        if (SaidaRedirecionada())
        {
            // Arquivo ou pipe: grava em UTF-8, para os acentos chegarem inteiros.
            var utf8 = new UTF8Encoding(false);
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError(), utf8) { AutoFlush = true });
            return;
        }

        if (!AttachConsole(ConsoleDoPai))
        {
            AllocConsole();
        }

        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch (IOException)
        {
            // Console sem suporte à troca de página de código: segue com a padrão.
        }
    }

    private static bool SaidaRedirecionada()
    {
        const int saidaPadrao = -11;
        const uint tipoArquivo = 1;
        const uint tipoPipe = 3;
        var identificador = GetStdHandle(saidaPadrao);
        if (identificador == 0 || identificador == -1)
        {
            return false;
        }

        var tipo = GetFileType(identificador);
        return tipo is tipoArquivo or tipoPipe;
    }

    [LibraryImport("kernel32.dll")]
    private static partial nint GetStdHandle(int qual);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetFileType(nint identificador);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(int processo);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllocConsole();

    /// <summary>Mostra o andamento numa linha só, reescrita no lugar.</summary>
    private sealed class ProgressoConsole : IProgress<ProgressoVarredura>
    {
        private readonly object _trava = new();
        private string _etapa = string.Empty;
        private int _ultimoPercentual = -1;

        public void Report(ProgressoVarredura valor)
        {
            lock (_trava)
            {
                var percentual = valor.Total == 0 ? 100 : valor.Concluidos * 100 / valor.Total;
                if (valor.Etapa != _etapa)
                {
                    if (_etapa.Length > 0)
                    {
                        Console.WriteLine();
                    }

                    _etapa = valor.Etapa;
                    _ultimoPercentual = -1;
                }

                if (percentual == _ultimoPercentual)
                {
                    return;
                }

                _ultimoPercentual = percentual;
                Console.Write($"\r{valor.Etapa}: {valor.Concluidos} de {valor.Total} ({percentual}%)   ");
            }
        }
    }
}

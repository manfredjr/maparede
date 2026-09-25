using MapaRedeMt.Nucleo;

namespace MapaRedeMt;

internal static class Programa
{
    /// <summary>
    /// Sem argumentos abre a janela. Com argumentos roda no modo linha de comando, no mesmo .exe.
    /// </summary>
    [STAThread]
    private static int Main(string[] args)
    {
        var argumentos = ArgumentosCli.Interpretar(args);
        if (args.Length > 0)
        {
            return ModoLinhaDeComando.Executar(argumentos);
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new FormularioPrincipal());
        return 0;
    }
}

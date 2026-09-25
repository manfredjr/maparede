using System.Windows;
using System.Windows.Threading;
using MapNet.Nucleo;

namespace MapNet;

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

        var aplicativo = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        aplicativo.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/mapnet;component/tema/tema-mt.xaml", UriKind.Absolute),
        });
        aplicativo.DispatcherUnhandledException += AoErroNaoTratado;
        return aplicativo.Run(new JanelaPrincipal());
    }

    /// <summary>Erro que escapou da tela: mostra a mensagem e mantém o programa aberto.</summary>
    private static void AoErroNaoTratado(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Aconteceu um erro inesperado: {e.Exception.Message}",
            "MapNet - MT",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}

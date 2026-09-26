using System.Windows;
using System.Windows.Threading;
using MapNet.Nucleo;

namespace MapNet;

internal static class Programa
{
    /// <summary>
    /// Sem argumentos abre a janela. Com argumentos roda no modo linha de comando, no mesmo .exe.
    /// O argumento --demonstracao abre a janela com dados de exemplo, sem tocar na rede.
    /// </summary>
    [STAThread]
    private static int Main(string[] args)
    {
        // Modo auxiliar, aberto com elevação pela aba Manutenção: roda uma ação e termina, sem janela.
        if (args.Length > 0 && args[0] == Auxiliar.Argumento)
        {
            return Auxiliar.Executar(args);
        }

        var demonstracao = args is [Demonstracao.Argumento];
        var argumentos = ArgumentosCli.Interpretar(args);
        if (args.Length > 0 && !demonstracao)
        {
            return ModoLinhaDeComando.Executar(argumentos);
        }

        var aplicativo = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        aplicativo.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/mapnet;component/tema/tema-mt.xaml", UriKind.Absolute),
        });
        aplicativo.DispatcherUnhandledException += AoErroNaoTratado;
        return aplicativo.Run(demonstracao
            ? new JanelaPrincipal(Demonstracao.Dependencias(), " (demonstração)")
            : new JanelaPrincipal(DependenciasPainel.Padrao(), string.Empty));
    }

    private static bool _erroMostrado;

    /// <summary>
    /// Erro que escapou da tela: mostra a mensagem uma vez e fecha o programa. Um erro de
    /// desenho da tela se repete a cada tentativa de redesenhar, e manter o programa aberto
    /// empilharia uma janela de erro atrás da outra.
    /// </summary>
    private static void AoErroNaoTratado(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        if (_erroMostrado)
        {
            return;
        }

        _erroMostrado = true;
        MessageBox.Show(
            $"Aconteceu um erro inesperado: {e.Exception.Message}\n\nO MapNet vai fechar. Abra de novo e, se o erro voltar, avise a MT.",
            "MapNet - MT",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        Application.Current.Shutdown(1);
    }
}

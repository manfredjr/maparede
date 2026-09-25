using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Data;
using MapNet.Nucleo;
using Microsoft.Win32;

namespace MapNet;

/// <summary>
/// Janela do leiaute C. Liga os controles ao <see cref="PainelVarredura"/> e cuida só do que
/// depende do Windows: janelas de diálogo, abrir arquivo e pasta, área de transferência.
/// </summary>
public partial class JanelaPrincipal : Window
{
    private readonly PainelVarredura _painel;

    public JanelaPrincipal()
    {
        InitializeComponent();
        _painel = new PainelVarredura(DependenciasPainel.Padrao());
        DataContext = _painel;
        Title = PainelVarredura.Titulo;

        var vista = CollectionViewSource.GetDefaultView(_painel.Hosts);
        vista.Filter = _painel.Aceita;
        _painel.FiltroMudou += vista.Refresh;
        TabelaHosts.ItemsSource = vista;

        // O console acompanha a última linha, como um terminal.
        _painel.Console.Linhas.CollectionChanged += AoMudarConsole;

        // Erro de verdade aparece numa janela: o técnico precisa ler antes de seguir.
        _painel.Falhou += mensagem =>
            MessageBox.Show(this, mensagem, "MapNet - MT", MessageBoxButton.OK, MessageBoxImage.Error);

        Loaded += (_, _) => _painel.CarregarInterfaces();
        Closing += (_, _) => _painel.Cancelar();
    }

    private void AoMudarConsole(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && ListaConsole.Items.Count > 0)
        {
            ListaConsole.ScrollIntoView(ListaConsole.Items[^1]);
        }
    }

    private void AoClicarRelatorio(object sender, RoutedEventArgs e)
    {
        if (BotaoRelatorio.ContextMenu is { } menu)
        {
            menu.DataContext = _painel;
            menu.PlacementTarget = BotaoRelatorio;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    private void AoAbrirRelatorio(object sender, RoutedEventArgs e) => AbrirNoSistema(_painel.UltimoRelatorio);

    private async void AoSalvarComo(object sender, RoutedEventArgs e)
    {
        if (_painel.UltimoResultado is not { } resultado)
        {
            return;
        }

        Directory.CreateDirectory(_painel.PastaRelatorios);
        var dialogo = new SaveFileDialog
        {
            Title = "Salvar relatório",
            Filter = "Relatório HTML (*.html)|*.html",
            FileName = RelatorioHtml.NomeArquivo(resultado),
            InitialDirectory = _painel.PastaRelatorios,
        };
        if (dialogo.ShowDialog(this) == true)
        {
            await _painel.SalvarComoAsync(dialogo.FileName);
        }
    }

    private void AoAbrirPasta(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_painel.PastaRelatorios);
        AbrirNoSistema(_painel.PastaRelatorios);
    }

    private void AoCopiarConsole(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_painel.Console.Texto);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Outro programa segurando a área de transferência: basta clicar de novo.
        }
    }

    private void AoLimparConsole(object sender, RoutedEventArgs e) => _painel.Console.Limpar();

    private void AbrirNoSistema(string? caminho)
    {
        if (string.IsNullOrEmpty(caminho))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true });
        }
        catch (Exception erro)
        {
            MessageBox.Show(this, erro.Message, "Não foi possível abrir", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}

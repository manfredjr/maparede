using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
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

    public JanelaPrincipal(DependenciasPainel dependencias, string complementoTitulo)
    {
        InitializeComponent();
        dependencias.Abrir ??= (arquivo, argumentos) =>
            Process.Start(new ProcessStartInfo(arquivo, argumentos ?? string.Empty) { UseShellExecute = true })?.Dispose();
        dependencias.Copiar ??= texto =>
        {
            try
            {
                Clipboard.SetText(texto);
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                // Outro programa segurando a área de transferência: basta clicar de novo.
            }
        };
        dependencias.Confirmar ??= pergunta =>
            MessageBox.Show(this, pergunta, "MapNet - MT", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
        _painel = new PainelVarredura(dependencias);
        DataContext = _painel;
        Title = PainelVarredura.Titulo + complementoTitulo;

        var vista = CollectionViewSource.GetDefaultView(_painel.Hosts);
        vista.Filter = _painel.Aceita;
        _painel.FiltroMudou += vista.Refresh;
        TabelaHosts.ItemsSource = vista;

        // O console acompanha a última linha, como um terminal, em cada aba.
        foreach (var aba in _painel.Abas)
        {
            aba.Registro.Linhas.CollectionChanged += (_, e) =>
            {
                if (ReferenceEquals(aba, _painel.AbaSelecionada))
                {
                    AoMudarConsole(e);
                }
            };
        }

        _painel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PainelVarredura.AbaSelecionada))
            {
                RolarAoFim();
            }
        };

        // Erro de verdade aparece numa janela: o técnico precisa ler antes de seguir.
        _painel.Falhou += mensagem =>
            MessageBox.Show(this, mensagem, "MapNet - MT", MessageBoxButton.OK, MessageBoxImage.Error);

        Loaded += (_, _) => _painel.CarregarInterfaces();

        // Esc fecha o detalhe do host.
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape && _painel.TemHostSelecionado)
            {
                _painel.HostSelecionado = null;
                e.Handled = true;
            }
        };
        Closing += AoFechar;
    }

    /// <summary>
    /// O relatório não é gravado sozinho. Fechar com a última varredura sem salvar pergunta antes,
    /// para o técnico não perder o levantamento.
    /// </summary>
    private async void AoFechar(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_fecharSemPerguntar && _painel.Estado == EstadoPainel.Parado && _painel.RelatorioNaoSalvo)
        {
            var resposta = MessageBox.Show(this, "O relatório da última varredura ainda não foi salvo. Salvar antes de fechar?",
                "MapNet - MT", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Yes);
            if (resposta == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (resposta == MessageBoxResult.Yes)
            {
                e.Cancel = true;
                if (await SalvarComDialogoAsync())
                {
                    _fecharSemPerguntar = true;
                    Close();
                }

                return;
            }
        }

        _painel.Cancelar();
        _painel.PararFerramentas();
    }

    private bool _fecharSemPerguntar;

    /// <summary>Abre a janela de salvar e grava. Devolve falso se o técnico desistiu ou a gravação falhou.</summary>
    private async Task<bool> SalvarComDialogoAsync()
    {
        if (_painel.UltimoResultado is null)
        {
            return false;
        }

        Directory.CreateDirectory(_painel.PastaInicial);
        // O nome vai sem extensão: o tipo escolhido na lista acrescenta .html, .csv ou .xml.
        var dialogo = new SaveFileDialog
        {
            Title = "Salvar relatório",
            Filter = "Relatório HTML, para abrir no navegador (*.html)|*.html|Planilha CSV, para o Excel (*.csv)|*.csv|Dados XML, para outro programa (*.xml)|*.xml",
            FileName = Path.GetFileNameWithoutExtension(_painel.NomeSugerido),
            DefaultExt = ".html",
            AddExtension = true,
            InitialDirectory = _painel.PastaInicial,
        };
        if (dialogo.ShowDialog(this) != true)
        {
            return false;
        }

        var caminho = dialogo.FileName!;
        if (!Path.HasExtension(caminho))
        {
            caminho += dialogo.FilterIndex switch { 2 => ".csv", 3 => ".xml", _ => ".html" };
        }

        await _painel.SalvarComoAsync(caminho);
        return !_painel.RelatorioNaoSalvo;
    }

    private void AoMudarConsole(NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            RolarAoFim();
        }
    }

    private void RolarAoFim()
    {
        // A rolagem espera a lista registrar a linha nova. Rolar aqui dentro, no meio do aviso
        // de mudança, mede a lista antes de ela contar a linha, e o WPF derruba o programa com
        // "ItemsControl is inconsistent with its items source".
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            if (ListaConsole.Items.Count > 0)
            {
                ListaConsole.ScrollIntoView(ListaConsole.Items[^1]);
            }
        });
    }

    /// <summary>Clicar de novo na linha que já está aberta fecha o detalhe.</summary>
    private void AoClicarNaTabela(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var elemento = e.OriginalSource as DependencyObject;
        while (elemento != null && elemento is not System.Windows.Controls.DataGridRow)
        {
            elemento = System.Windows.Media.VisualTreeHelper.GetParent(elemento);
        }

        if (elemento is System.Windows.Controls.DataGridRow linha && ReferenceEquals(linha.Item, _painel.HostSelecionado))
        {
            _painel.HostSelecionado = null;
            TabelaHosts.SelectedItem = null;
            e.Handled = true;
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

    private async void AoSalvarComo(object sender, RoutedEventArgs e) => await SalvarComDialogoAsync();

    private void AoAbrirPasta(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_painel.PastaInicial);
        AbrirNoSistema(_painel.PastaInicial);
    }

    private void AoCopiarConsole(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_painel.AbaSelecionada.Registro.Texto);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Outro programa segurando a área de transferência: basta clicar de novo.
        }
    }

    private void AoLimparConsole(object sender, RoutedEventArgs e) => _painel.AbaSelecionada.Registro.Limpar();

    /// <summary>O navegador abre o site da MT. O programa em si não manda nada para a internet.</summary>
    private void AoClicarMarcaMt(object sender, RoutedEventArgs e) => AbrirNoSistema(SiteMt);

    public const string SiteMt = "https://www.manfred.com.br";

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

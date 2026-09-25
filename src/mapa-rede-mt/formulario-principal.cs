using System.Diagnostics;
using MapaRedeMt.Nucleo;

namespace MapaRedeMt;

/// <summary>Janela principal: escolha da interface, varredura, lista de hosts e relatório.</summary>
internal sealed class FormularioPrincipal : Form
{
    private readonly ComboBox _comboInterfaces = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 520 };
    private readonly Button _botaoAtualizar = new() { Text = "Atualizar lista", AutoSize = true };
    private readonly Label _rotuloDetalhes = new() { AutoSize = true, Padding = new Padding(0, 4, 0, 4) };
    private readonly Button _botaoVarrer = new() { Text = "Iniciar varredura", AutoSize = true };
    private readonly Button _botaoCancelar = new() { Text = "Cancelar", AutoSize = true, Enabled = false };
    private readonly Button _botaoAbrir = new() { Text = "Abrir relatório", AutoSize = true, Enabled = false };
    private readonly Button _botaoSalvarComo = new() { Text = "Salvar relatório como...", AutoSize = true, Enabled = false };
    private readonly Button _botaoPasta = new() { Text = "Abrir pasta dos relatórios", AutoSize = true };
    private readonly ProgressBar _barra = new() { Dock = DockStyle.Fill, Height = 18 };
    private readonly Label _rotuloAndamento = new() { AutoSize = true, Text = "Pronto para varrer.", Padding = new Padding(0, 2, 0, 2) };
    private readonly ListView _lista = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        GridLines = true,
        HideSelection = false,
    };

    private readonly ToolStripStatusLabel _status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Dictionary<uint, ListViewItem> _itens = [];

    private CancellationTokenSource? _cancelamento;
    private ResultadoVarredura? _ultimoResultado;
    private string? _ultimoRelatorio;
    private int _colunaOrdenada;
    private bool _ordemCrescente = true;

    public FormularioPrincipal()
    {
        Text = $"MT Mapa de Rede {ResultadoVarredura.VersaoPrograma} - MT - Manfred Tecnologia";

        // Fonte de mensagens do sistema: Segoe UI no Windows 10 e 11, sem quebrar onde ela não existe.
        Font = SystemFonts.MessageBoxFont ?? Font;
        MinimumSize = new Size(900, 560);
        Size = new Size(1100, 700);
        StartPosition = FormStartPosition.CenterScreen;

        MontarTela();

        _botaoAtualizar.Click += (_, _) => CarregarInterfaces();
        _comboInterfaces.SelectedIndexChanged += (_, _) => MostrarDetalhesInterface();
        _botaoVarrer.Click += async (_, _) => await VarrerAsync();
        _botaoCancelar.Click += (_, _) => _cancelamento?.Cancel();
        _botaoAbrir.Click += (_, _) => AbrirNoSistema(_ultimoRelatorio);
        _botaoSalvarComo.Click += async (_, _) => await SalvarComoAsync();
        _botaoPasta.Click += (_, _) =>
        {
            Directory.CreateDirectory(PastaRelatorios);
            AbrirNoSistema(PastaRelatorios);
        };
        _lista.ColumnClick += (_, e) => Ordenar(e.Column);
        FormClosing += (_, _) => _cancelamento?.Cancel();
        Load += (_, _) => CarregarInterfaces();
    }

    /// <summary>Documentos\MT Mapa de Rede, onde cada varredura grava o relatório.</summary>
    private static string PastaRelatorios =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MT Mapa de Rede");

    private void MontarTela()
    {
        var linhaInterface = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        linhaInterface.Controls.Add(new Label { Text = "Interface de rede:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        linhaInterface.Controls.Add(_comboInterfaces);
        linhaInterface.Controls.Add(_botaoAtualizar);

        var linhaBotoes = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        linhaBotoes.Controls.AddRange([_botaoVarrer, _botaoCancelar, _botaoAbrir, _botaoSalvarComo, _botaoPasta]);

        _lista.Columns.Add("IP", 120);
        _lista.Columns.Add("Nome", 200);
        _lista.Columns.Add("Origem do nome", 110);
        _lista.Columns.Add("MAC", 140);
        _lista.Columns.Add("Fabricante", 230);
        _lista.Columns.Add("Ping (ms)", 75, HorizontalAlignment.Right);
        _lista.Columns.Add("Observação", 200);

        var grade = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(10) };
        grade.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grade.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grade.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grade.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grade.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grade.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        grade.Controls.Add(linhaInterface, 0, 0);
        grade.Controls.Add(_rotuloDetalhes, 0, 1);
        grade.Controls.Add(linhaBotoes, 0, 2);
        grade.Controls.Add(_barra, 0, 3);
        grade.Controls.Add(_rotuloAndamento, 0, 4);
        grade.Controls.Add(_lista, 0, 5);

        var barraStatus = new StatusStrip();
        barraStatus.Items.Add(_status);

        Controls.Add(grade);
        Controls.Add(barraStatus);
    }

    private void CarregarInterfaces()
    {
        _comboInterfaces.Items.Clear();
        IReadOnlyList<InterfaceRede> interfaces;
        try
        {
            interfaces = LeitorInterfaces.Listar();
        }
        catch (Exception e)
        {
            interfaces = [];
            _status.Text = $"Não foi possível ler as interfaces de rede: {e.Message}";
        }

        foreach (var i in interfaces)
        {
            _comboInterfaces.Items.Add(i);
        }

        if (_comboInterfaces.Items.Count > 0)
        {
            _comboInterfaces.SelectedIndex = 0;
            _botaoVarrer.Enabled = true;
        }
        else
        {
            _rotuloDetalhes.Text = "Nenhuma interface de rede ativa com IPv4 foi encontrada. Conecte o cabo ou o Wi-Fi e clique em Atualizar lista.";
            _botaoVarrer.Enabled = false;
        }
    }

    private void MostrarDetalhesInterface()
    {
        if (_comboInterfaces.SelectedItem is not InterfaceRede i)
        {
            return;
        }

        var subRede = Varredor.SubRedeAVarrer(i, new OpcoesVarredura().PrefixoMinimo, out var aviso);
        _rotuloDetalhes.Text =
            $"{i.Descricao}\n"
            + $"IP {i.Ip}   Máscara {i.Mascara}   Gateway {i.Gateway?.ToString() ?? "nenhum"}   "
            + $"DNS {(i.Dns.Count > 0 ? string.Join(", ", i.Dns) : "nenhum")}\n"
            + $"Sub-rede a varrer: {subRede} ({subRede.QuantidadeHosts} endereços)"
            + (aviso != null ? $"\n{aviso}" : string.Empty);
    }

    private async Task VarrerAsync()
    {
        if (_comboInterfaces.SelectedItem is not InterfaceRede interfaceRede)
        {
            return;
        }

        _cancelamento = new CancellationTokenSource();
        AlternarVarredura(emAndamento: true);
        _lista.Items.Clear();
        _itens.Clear();
        _status.Text = string.Empty;

        var progresso = new Progress<ProgressoVarredura>(MostrarProgresso);
        try
        {
            var resultado = await new Varredor().VarrerAsync(interfaceRede, progresso, _cancelamento.Token);
            _ultimoResultado = resultado;
            PreencherLista(resultado);

            Directory.CreateDirectory(PastaRelatorios);
            _ultimoRelatorio = await RelatorioHtml.SalvarAsync(resultado, Path.Combine(PastaRelatorios, RelatorioHtml.NomeArquivo(resultado)));

            _rotuloAndamento.Text = resultado.Cancelada
                ? $"Varredura interrompida. {resultado.Hosts.Count} hosts levantados até ali."
                : $"Varredura concluída: {resultado.Hosts.Count} hosts em {RelatorioHtml.Duracao(resultado.Duracao)}.";
            _status.Text = $"Relatório gravado em {_ultimoRelatorio}";
            if (resultado.Avisos.Count > 0)
            {
                MessageBox.Show(this, string.Join("\n\n", resultado.Avisos), "Avisos da varredura", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception e)
        {
            _rotuloAndamento.Text = "A varredura falhou.";
            MessageBox.Show(this, e.Message, "Erro na varredura", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _cancelamento.Dispose();
            _cancelamento = null;
            AlternarVarredura(emAndamento: false);
        }
    }

    private void AlternarVarredura(bool emAndamento)
    {
        _botaoVarrer.Enabled = !emAndamento;
        _botaoAtualizar.Enabled = !emAndamento;
        _comboInterfaces.Enabled = !emAndamento;
        _botaoCancelar.Enabled = emAndamento;
        _botaoAbrir.Enabled = !emAndamento && _ultimoRelatorio != null;
        _botaoSalvarComo.Enabled = !emAndamento && _ultimoResultado != null;
        UseWaitCursor = emAndamento;
        _lista.UseWaitCursor = false;
    }

    private void MostrarProgresso(ProgressoVarredura p)
    {
        _barra.Maximum = Math.Max(1, p.Total);
        _barra.Value = Math.Min(p.Concluidos, _barra.Maximum);
        _rotuloAndamento.Text = $"{p.Etapa}: {p.Concluidos} de {p.Total}";
        if (p.Host != null)
        {
            AtualizarItem(p.Host);
        }
    }

    private void PreencherLista(ResultadoVarredura resultado)
    {
        _lista.BeginUpdate();
        _lista.Items.Clear();
        _itens.Clear();
        foreach (var h in resultado.Hosts)
        {
            AtualizarItem(h);
        }

        _lista.EndUpdate();
        Ordenar(_colunaOrdenada, manterOrdem: true);
    }

    private void AtualizarItem(HostEncontrado h)
    {
        var textos = new[]
        {
            h.Ip.ToString(),
            h.Nome,
            h.OrigemNome,
            h.MacTexto,
            h.Fabricante,
            h.TempoPingMs?.ToString() ?? "-",
            string.Join(", ", h.Marcas),
        };

        if (!_itens.TryGetValue(h.IpNumero, out var item))
        {
            item = new ListViewItem(textos) { Tag = h };
            _itens[h.IpNumero] = item;
            _lista.Items.Add(item);
            return;
        }

        item.Tag = h;
        for (var i = 0; i < textos.Length; i++)
        {
            item.SubItems[i].Text = textos[i];
        }
    }

    private void Ordenar(int coluna, bool manterOrdem = false)
    {
        if (!manterOrdem)
        {
            _ordemCrescente = coluna != _colunaOrdenada || !_ordemCrescente;
        }

        _colunaOrdenada = coluna;
        _lista.ListViewItemSorter = new ComparadorItens(coluna, _ordemCrescente);
        _lista.Sort();
    }

    private async Task SalvarComoAsync()
    {
        if (_ultimoResultado is null)
        {
            return;
        }

        using var dialogo = new SaveFileDialog
        {
            Title = "Salvar relatório",
            Filter = "Relatório HTML (*.html)|*.html",
            FileName = RelatorioHtml.NomeArquivo(_ultimoResultado),
            InitialDirectory = PastaRelatorios,
        };
        if (dialogo.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var caminho = await RelatorioHtml.SalvarAsync(_ultimoResultado, dialogo.FileName);
        _status.Text = $"Relatório gravado em {caminho}";
    }

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
        catch (Exception e)
        {
            MessageBox.Show(this, e.Message, "Não foi possível abrir", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>Ordena a lista: IP e ping por número, o resto por texto.</summary>
    private sealed class ComparadorItens(int coluna, bool crescente) : System.Collections.IComparer
    {
        public int Compare(object? x, object? y)
        {
            if (x is not ListViewItem a || y is not ListViewItem b || a.Tag is not HostEncontrado ha || b.Tag is not HostEncontrado hb)
            {
                return 0;
            }

            var resultado = coluna switch
            {
                0 => ha.IpNumero.CompareTo(hb.IpNumero),
                5 => (ha.TempoPingMs ?? long.MaxValue).CompareTo(hb.TempoPingMs ?? long.MaxValue),
                _ => string.Compare(a.SubItems[coluna].Text, b.SubItems[coluna].Text, StringComparison.CurrentCultureIgnoreCase),
            };
            if (resultado == 0 && coluna != 0)
            {
                resultado = ha.IpNumero.CompareTo(hb.IpNumero);
            }

            return crescente ? resultado : -resultado;
        }
    }
}

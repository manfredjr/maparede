using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MapNet.Nucleo;

/// <summary>Em que ponto a tela está. Os botões saem só daqui.</summary>
public enum EstadoPainel
{
    Parado,
    Varrendo,
    Cancelando,
}

/// <summary>
/// O que a tela usa de fora: interfaces, varredura, gravação do relatório e o jeito de levar o
/// andamento para a linha da tela. Os testes trocam cada parte por uma versão simulada.
/// </summary>
public sealed class DependenciasPainel
{
    public required Func<IReadOnlyList<InterfaceRede>> ListarInterfaces { get; init; }

    public required Func<InterfaceRede, IProgress<ProgressoVarredura>, CancellationToken, Task<ResultadoVarredura>> Varrer { get; init; }

    public required Func<ResultadoVarredura, string, Task<string>> SalvarEm { get; init; }

    /// <summary>Na tela, um <see cref="Progress{T}"/>, que entrega o andamento na linha da janela.</summary>
    public Func<Action<ProgressoVarredura>, IProgress<ProgressoVarredura>> CriarProgresso { get; init; } =
        acao => new Progress<ProgressoVarredura>(acao);

    public int PrefixoMinimo { get; init; } = new OpcoesVarredura().PrefixoMinimo;

    public string PastaRelatorios { get; init; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MapNet - MT");

    /// <summary>As partes de verdade: interfaces do Windows, varredor e relatório HTML.</summary>
    public static DependenciasPainel Padrao() => new()
    {
        ListarInterfaces = LeitorInterfaces.Listar,
        Varrer = (i, p, c) => new Varredor().VarrerAsync(i, p, c),
        SalvarEm = (r, caminho) => RelatorioHtml.SalvarAsync(r, caminho),
    };
}

/// <summary>
/// Modelo da tela principal: estado, interface escolhida, "Minha máquina", tabela de hosts,
/// console e barra de estado. Não usa nenhum tipo do WPF, para rodar nos testes.
/// </summary>
public sealed class PainelVarredura : INotifyPropertyChanged
{
    private readonly DependenciasPainel _dep;
    private readonly Dictionary<uint, LinhaHost> _porIp = [];
    private readonly HashSet<string> _avisosEscritos = [];
    private CancellationTokenSource? _cancelamento;
    private EstadoPainel _estado = EstadoPainel.Parado;
    private InterfaceRede? _interfaceSelecionada;
    private string _filtro = string.Empty;
    private double _progresso;
    private string _textoAndamento = "Pronto para varrer.";
    private string _textoResumo = string.Empty;
    private string _textoAviso = string.Empty;
    private string _textoRelatorio = string.Empty;

    public PainelVarredura(DependenciasPainel dependencias)
    {
        _dep = dependencias;
        ComandoPrincipal = new Comando(AcionarPrincipal, () => PodeAcionarPrincipal);
        ComandoAtualizar = new Comando(CarregarInterfaces, () => PodeTrocarInterface);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>A varredura ou a gravação do relatório falhou. A tela mostra a mensagem numa janela.</summary>
    public event Action<string>? Falhou;

    /// <summary>O filtro mudou e a tabela precisa ser refeita.</summary>
    public event Action? FiltroMudou;

    public static string Titulo => $"MapNet - MT {ResultadoVarredura.VersaoPrograma}";

    public ObservableCollection<InterfaceRede> Interfaces { get; } = [];

    /// <summary>Hosts em ordem de IP. A tela pode reordenar por coluna.</summary>
    public ObservableCollection<LinhaHost> Hosts { get; } = [];

    public RegistroConsole Console { get; } = new();

    public Comando ComandoPrincipal { get; }

    public Comando ComandoAtualizar { get; }

    public string PastaRelatorios => _dep.PastaRelatorios;

    public ResultadoVarredura? UltimoResultado { get; private set; }

    public string? UltimoRelatorio { get; private set; }

    public EstadoPainel Estado
    {
        get => _estado;
        private set
        {
            if (_estado == value)
            {
                return;
            }

            _estado = value;
            Avisar();
            AvisarBotoes();
        }
    }

    public InterfaceRede? InterfaceSelecionada
    {
        get => _interfaceSelecionada;
        set
        {
            if (ReferenceEquals(_interfaceSelecionada, value) || !PodeTrocarInterface)
            {
                return;
            }

            _interfaceSelecionada = value;
            Avisar();
            Avisar(nameof(MinhaMaquina));
            AvisarBotoes();
        }
    }

    /// <summary>Linhas da coluna "Minha máquina", com o que o núcleo já lê da placa escolhida.</summary>
    public IReadOnlyList<ItemMinhaMaquina> MinhaMaquina
    {
        get
        {
            if (_interfaceSelecionada is not { } i)
            {
                return [new ItemMinhaMaquina("Interface", "Nenhuma interface de rede ativa com IPv4. Conecte o cabo ou o Wi-Fi e clique em Atualizar.")];
            }

            var subRede = Varredor.SubRedeAVarrer(i, _dep.PrefixoMinimo, out _);
            return
            [
                new ItemMinhaMaquina("Placa", i.Descricao.Length > 0 ? i.Descricao : i.Nome),
                new ItemMinhaMaquina("Tipo", i.TipoTexto),
                new ItemMinhaMaquina("MAC", i.Mac.Length > 0 ? EnderecoMac.Formatar(i.Mac) : "não informado"),
                new ItemMinhaMaquina("IPv4", $"{i.Ip}/{i.Prefixo}"),
                new ItemMinhaMaquina("Máscara", i.Mascara.ToString()),
                new ItemMinhaMaquina("Gateway", i.Gateway?.ToString() ?? "nenhum"),
                new ItemMinhaMaquina("DNS", i.Dns.Count > 0 ? string.Join(", ", i.Dns) : "nenhum"),
                new ItemMinhaMaquina("Sub-rede a varrer", $"{subRede} ({subRede.QuantidadeHosts} endereços)"),
            ];
        }
    }

    public string Filtro
    {
        get => _filtro;
        set
        {
            var novo = value ?? string.Empty;
            if (_filtro == novo)
            {
                return;
            }

            _filtro = novo;
            Avisar();
            FiltroMudou?.Invoke();
        }
    }

    public bool PodeTrocarInterface => _estado == EstadoPainel.Parado;

    public bool PodeAcionarPrincipal =>
        (_estado == EstadoPainel.Parado && _interfaceSelecionada != null) || _estado == EstadoPainel.Varrendo;

    public string TextoBotaoPrincipal => _estado switch
    {
        EstadoPainel.Varrendo => "Cancelar",
        EstadoPainel.Cancelando => "Cancelando...",
        _ => "Iniciar varredura",
    };

    public bool Varrendo => _estado != EstadoPainel.Parado;

    public bool PodeAbrirRelatorio => _estado == EstadoPainel.Parado && UltimoRelatorio != null;

    public bool PodeSalvarComo => _estado == EstadoPainel.Parado && UltimoResultado != null;

    public string TituloHosts => $"Hosts da rede: {Hosts.Count}";

    /// <summary>De 0 a 100, para a barra de progresso.</summary>
    public double Progresso
    {
        get => _progresso;
        private set => Trocar(ref _progresso, value);
    }

    public string TextoAndamento
    {
        get => _textoAndamento;
        private set => Trocar(ref _textoAndamento, value);
    }

    /// <summary>Total de hosts e tempo da última varredura.</summary>
    public string TextoResumo
    {
        get => _textoResumo;
        private set => Trocar(ref _textoResumo, value);
    }

    /// <summary>Aviso curto na barra de estado. O texto completo fica no console.</summary>
    public string TextoAviso
    {
        get => _textoAviso;
        private set => Trocar(ref _textoAviso, value);
    }

    public string TextoRelatorio
    {
        get => _textoRelatorio;
        private set => Trocar(ref _textoRelatorio, value);
    }

    /// <summary>Filtro da tabela: a tela chama para cada linha.</summary>
    public bool Aceita(object? item) => item is LinhaHost linha && linha.Contem(_filtro);

    public void CarregarInterfaces()
    {
        if (!PodeTrocarInterface)
        {
            return;
        }

        var anterior = _interfaceSelecionada?.Id;
        IReadOnlyList<InterfaceRede> lista;
        try
        {
            lista = _dep.ListarInterfaces();
        }
        catch (Exception e)
        {
            lista = [];
            Console.Escrever($"Não foi possível ler as interfaces de rede: {e.Message}");
        }

        Interfaces.Clear();
        foreach (var i in lista)
        {
            Interfaces.Add(i);
        }

        _interfaceSelecionada = null;
        InterfaceSelecionada = Interfaces.FirstOrDefault(i => i.Id == anterior) ?? Interfaces.FirstOrDefault();
        if (_interfaceSelecionada is null)
        {
            Avisar(nameof(InterfaceSelecionada));
            Avisar(nameof(MinhaMaquina));
            AvisarBotoes();
            TextoAndamento = "Nenhuma interface de rede ativa com IPv4 foi encontrada.";
        }
        else
        {
            TextoAndamento = "Pronto para varrer.";
        }
    }

    public void Cancelar()
    {
        if (_estado != EstadoPainel.Varrendo)
        {
            return;
        }

        Estado = EstadoPainel.Cancelando;
        TextoAndamento = "Cancelando...";
        _cancelamento?.Cancel();
    }

    /// <summary>
    /// Varre a rede da interface escolhida. O estado volta a Parado assim que a varredura
    /// termina, antes do relatório e de qualquer aviso, para os botões ficarem certos.
    /// </summary>
    public async Task VarrerAsync()
    {
        if (_estado != EstadoPainel.Parado || _interfaceSelecionada is not { } interfaceRede)
        {
            return;
        }

        _cancelamento = new CancellationTokenSource();
        Hosts.Clear();
        _porIp.Clear();
        _avisosEscritos.Clear();
        UltimoResultado = null;
        UltimoRelatorio = null;
        Progresso = 0;
        TextoResumo = string.Empty;
        TextoAviso = string.Empty;
        TextoRelatorio = string.Empty;
        Avisar(nameof(TituloHosts));

        var subRede = Varredor.SubRedeAVarrer(interfaceRede, _dep.PrefixoMinimo, out var aviso);
        Console.Escrever($"Varredura de {subRede} ({subRede.QuantidadeHosts} endereços) pela interface {interfaceRede.Nome}.");
        if (aviso != null)
        {
            EscreverAviso(aviso);
            TextoAviso = $"Só o bloco {subRede} foi varrido. Detalhes no console.";
        }

        Estado = EstadoPainel.Varrendo;
        TextoAndamento = "Iniciando a varredura...";

        ResultadoVarredura resultado;
        try
        {
            var progresso = _dep.CriarProgresso(AoProgresso);
            resultado = await _dep.Varrer(interfaceRede, progresso, _cancelamento.Token);
        }
        catch (Exception e)
        {
            Encerrar();
            TextoAndamento = "A varredura falhou.";
            Console.Escrever($"A varredura falhou: {e.Message}");
            Falhou?.Invoke($"A varredura falhou: {e.Message}");
            return;
        }

        Encerrar();
        UltimoResultado = resultado;
        foreach (var h in resultado.Hosts)
        {
            ColocarHost(h);
        }

        Progresso = 100;
        var duracao = RelatorioHtml.Duracao(resultado.Duracao);
        TextoAndamento = resultado.Cancelada ? "Varredura interrompida." : "Varredura concluída.";
        TextoResumo = $"{resultado.Hosts.Count} hosts em {duracao}";
        Console.Escrever(resultado.Cancelada
            ? $"Varredura interrompida. {resultado.Hosts.Count} hosts levantados até ali, em {duracao}."
            : $"Varredura concluída: {resultado.Hosts.Count} hosts em {duracao}.");
        foreach (var a in resultado.Avisos)
        {
            EscreverAviso(a);
        }

        AvisarBotoes();
        await SalvarAsync(resultado, Path.Combine(PastaRelatorios, RelatorioHtml.NomeArquivo(resultado)));
    }

    /// <summary>Grava o último resultado em outro lugar, escolhido pelo técnico.</summary>
    public Task SalvarComoAsync(string caminho) =>
        UltimoResultado is { } r && _estado == EstadoPainel.Parado ? SalvarAsync(r, caminho) : Task.CompletedTask;

    private async Task SalvarAsync(ResultadoVarredura resultado, string caminho)
    {
        try
        {
            var pasta = Path.GetDirectoryName(caminho);
            if (!string.IsNullOrEmpty(pasta))
            {
                Directory.CreateDirectory(pasta);
            }

            UltimoRelatorio = await _dep.SalvarEm(resultado, caminho);
            TextoRelatorio = $"Relatório em {UltimoRelatorio}";
            Console.Escrever($"Relatório gravado em {UltimoRelatorio}");
        }
        catch (Exception e)
        {
            Console.Escrever($"Não foi possível gravar o relatório: {e.Message}");
            Falhou?.Invoke($"Não foi possível gravar o relatório: {e.Message}");
        }

        AvisarBotoes();
    }

    private void AcionarPrincipal()
    {
        if (_estado == EstadoPainel.Varrendo)
        {
            Cancelar();
        }
        else
        {
            _ = VarrerAsync();
        }
    }

    private void Encerrar()
    {
        _cancelamento?.Dispose();
        _cancelamento = null;
        Estado = EstadoPainel.Parado;
    }

    private void AoProgresso(ProgressoVarredura p)
    {
        if (_estado == EstadoPainel.Parado)
        {
            return;
        }

        Progresso = p.Total > 0 ? 100.0 * p.Concluidos / p.Total : 0;
        if (_estado == EstadoPainel.Varrendo)
        {
            TextoAndamento = $"{p.Etapa}: {p.Concluidos} de {p.Total}";
        }

        if (p.Host != null)
        {
            ColocarHost(p.Host);
        }
    }

    /// <summary>Acrescenta o host na posição do IP, ou atualiza a linha que já existe.</summary>
    private void ColocarHost(HostEncontrado host)
    {
        if (_porIp.TryGetValue(host.IpNumero, out var existente))
        {
            existente.Atualizar(host);
            return;
        }

        var linha = new LinhaHost(host);
        _porIp[host.IpNumero] = linha;
        var inicio = 0;
        var fim = Hosts.Count;
        while (inicio < fim)
        {
            var meio = (inicio + fim) / 2;
            if (Hosts[meio].IpNumero < host.IpNumero)
            {
                inicio = meio + 1;
            }
            else
            {
                fim = meio;
            }
        }

        Hosts.Insert(inicio, linha);
        Avisar(nameof(TituloHosts));
    }

    /// <summary>Cada aviso vai ao console uma vez só, mesmo que a varredura o repita no fim.</summary>
    private void EscreverAviso(string aviso)
    {
        if (_avisosEscritos.Add(aviso))
        {
            Console.Escrever($"Aviso: {aviso}");
        }
    }

    private void AvisarBotoes()
    {
        Avisar(nameof(PodeTrocarInterface));
        Avisar(nameof(PodeAcionarPrincipal));
        Avisar(nameof(TextoBotaoPrincipal));
        Avisar(nameof(Varrendo));
        Avisar(nameof(PodeAbrirRelatorio));
        Avisar(nameof(PodeSalvarComo));
        ComandoPrincipal.Reavaliar();
        ComandoAtualizar.Reavaliar();
    }

    private void Trocar<T>(ref T campo, T valor, [CallerMemberName] string? propriedade = null)
    {
        if (EqualityComparer<T>.Default.Equals(campo, valor))
        {
            return;
        }

        campo = valor;
        Avisar(propriedade);
    }

    private void Avisar([CallerMemberName] string? propriedade = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propriedade));
}

/// <summary>Uma linha da coluna "Minha máquina": rótulo e valor.</summary>
public sealed record ItemMinhaMaquina(string Rotulo, string Valor);

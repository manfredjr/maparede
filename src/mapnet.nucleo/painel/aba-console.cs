using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MapNet.Nucleo;

/// <summary>
/// Uma aba do console: a de Varredura, que só registra, ou a de uma ferramenta, com os campos
/// que ela pede e o botão Executar, que vira Parar enquanto a ferramenta roda.
/// </summary>
public sealed class AbaConsole : INotifyPropertyChanged
{
    private string _alvo = string.Empty;
    private string _servidor = string.Empty;
    private string _alvoPadrao = string.Empty;
    private string _servidorPadrao = string.Empty;
    private bool _continuo;
    private CancellationTokenSource? _cancelamento;

    public AbaConsole(string titulo, RegistroConsole registro, IFerramenta? ferramenta = null)
    {
        Titulo = titulo;
        Registro = registro;
        Ferramenta = ferramenta;
        ComandoExecutar = new Comando(() =>
        {
            if (Rodando)
            {
                Parar();
            }
            else
            {
                _ = ExecutarAsync();
            }
        }, () => Ferramenta != null);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Titulo { get; }

    public string Descricao => Ferramenta?.Descricao
        ?? (EhManutencao ? "Limpar cache DNS, renovar IP, limpar ARP e resetar a rede, pelos comandos do Windows." : "Registro da varredura.");

    public RegistroConsole Registro { get; }

    public IFerramenta? Ferramenta { get; }

    public bool EhFerramenta => Ferramenta != null;

    /// <summary>A aba Manutenção, que mostra os botões das ações em vez dos campos de uma ferramenta.</summary>
    public bool EhManutencao { get; init; }

    public bool PedeAlvo => Ferramenta?.PedeAlvo == true;

    public bool PedeServidor => Ferramenta?.PedeServidor == true;

    public bool PodeContinuo => Ferramenta?.PodeContinuo == true;

    public Comando ComandoExecutar { get; }

    public bool Rodando => _cancelamento != null;

    public string TextoBotao => Rodando ? "Parar" : "Executar";

    public string Alvo
    {
        get => _alvo;
        set => Trocar(ref _alvo, value ?? string.Empty);
    }

    public string Servidor
    {
        get => _servidor;
        set => Trocar(ref _servidor, value ?? string.Empty);
    }

    public bool Continuo
    {
        get => _continuo;
        set => Trocar(ref _continuo, value);
    }

    /// <summary>
    /// Preenche os campos com o gateway e o DNS da interface escolhida. O que o técnico digitou
    /// fica: só troca o campo que ainda está vazio ou com o padrão anterior.
    /// </summary>
    public void DefinirPadroes(string alvo, string servidor)
    {
        if (_alvo.Length == 0 || _alvo == _alvoPadrao)
        {
            Alvo = alvo;
        }

        if (_servidor.Length == 0 || _servidor == _servidorPadrao)
        {
            Servidor = servidor;
        }

        _alvoPadrao = alvo;
        _servidorPadrao = servidor;
    }

    public async Task ExecutarAsync()
    {
        if (Ferramenta is not { } ferramenta || Rodando)
        {
            return;
        }

        _cancelamento = new CancellationTokenSource();
        AvisarRodando();
        var saida = new SaidaFerramenta(Registro.Escrever, Registro.EscreverSemHora);
        try
        {
            await ferramenta.ExecutarAsync(new ParametrosFerramenta(_alvo, _servidor, _continuo), saida, _cancelamento.Token);
        }
        catch (OperationCanceledException)
        {
            Registro.Escrever($"{ferramenta.Titulo} interrompido.");
        }
        catch (Exception e)
        {
            Registro.Escrever($"{ferramenta.Titulo}: {e.Message}");
        }
        finally
        {
            _cancelamento.Dispose();
            _cancelamento = null;
            AvisarRodando();
        }
    }

    public void Parar() => _cancelamento?.Cancel();

    /// <summary>O leitor de tela e a automação do Windows leem a aba pelo título.</summary>
    public override string ToString() => Titulo;

    private void AvisarRodando()
    {
        Avisar(nameof(Rodando));
        Avisar(nameof(TextoBotao));
        ComandoExecutar.Reavaliar();
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

using System.Collections.ObjectModel;

namespace MapNet.Nucleo;

/// <summary>
/// Registro que aparece no console da tela, uma linha por acontecimento, com a hora na frente.
/// Guarda no máximo <see cref="LimiteLinhas"/> linhas, para a tela não crescer sem fim.
/// </summary>
public sealed class RegistroConsole
{
    public const int LimiteLinhas = 2000;

    private readonly Func<DateTimeOffset> _agora;

    public RegistroConsole(Func<DateTimeOffset>? agora = null)
    {
        _agora = agora ?? (() => DateTimeOffset.Now);
    }

    public ObservableCollection<string> Linhas { get; } = [];

    public void Escrever(string texto)
    {
        Linhas.Add($"[{_agora():HH:mm:ss}] {texto}");
        while (Linhas.Count > LimiteLinhas)
        {
            Linhas.RemoveAt(0);
        }
    }

    public void Limpar() => Linhas.Clear();

    /// <summary>Todo o registro num texto só, para o botão de copiar.</summary>
    public string Texto => string.Join(Environment.NewLine, Linhas);
}

using System.Windows.Input;

namespace MapNet.Nucleo;

/// <summary>
/// Comando de botão sem biblioteca externa. A tela pergunta <see cref="CanExecute"/> de novo
/// quando o painel chama <see cref="Reavaliar"/>.
/// </summary>
public sealed class Comando : ICommand
{
    private readonly Action _executar;
    private readonly Func<bool> _pode;

    public Comando(Action executar, Func<bool>? pode = null)
    {
        _executar = executar;
        _pode = pode ?? (() => true);
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _pode();

    public void Execute(object? parameter)
    {
        if (_pode())
        {
            _executar();
        }
    }

    public void Reavaliar() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

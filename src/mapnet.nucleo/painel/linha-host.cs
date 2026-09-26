using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MapNet.Nucleo;

/// <summary>
/// Uma linha da tabela "Hosts da rede", com os textos já prontos para a tela. A tabela ordena
/// o IP por <see cref="IpNumero"/> e o ping por <see cref="PingOrdem"/>, e não pelo texto.
/// </summary>
public sealed class LinhaHost : INotifyPropertyChanged
{
    private HostEncontrado _host;

    public LinhaHost(HostEncontrado host)
    {
        _host = host;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public HostEncontrado Host => _host;

    public string Ip => _host.Ip.ToString();

    public uint IpNumero => _host.IpNumero;

    public string Nome => _host.Nome;

    public string OrigemNome => _host.OrigemNome;

    public string Mac => _host.MacTexto;

    public string Fabricante => _host.Fabricante;

    public string Ping => _host.TempoPingMs?.ToString() ?? "-";

    /// <summary>Host sem resposta de ping vai para o fim da ordem crescente.</summary>
    public long PingOrdem => _host.TempoPingMs ?? long.MaxValue;

    public string Observacao => string.Join(", ", _host.Marcas);

    /// <summary>Portas abertas, só os números: "22, 80, 443".</summary>
    public string Portas => _host.PortasTexto;

    /// <summary>Dica da coluna: cada porta com o serviço, ou o motivo de não ter sido verificada.</summary>
    public string? PortasDica =>
        _host.PortasAbertas.Count > 0 ? ListaPortas.Texto(_host.PortasAbertas)
        : _host.MotivoSemPortas is { } motivo ? char.ToUpperInvariant(motivo[0]) + motivo[1..]
        : null;

    public bool EhGateway => _host.EhGateway;

    public bool EhEsteComputador => _host.EhEsteComputador;

    /// <summary>Troca o host pelos dados mais novos, que chegam na etapa de nomes.</summary>
    public void Atualizar(HostEncontrado host)
    {
        _host = host;
        Avisar(string.Empty);
    }

    /// <summary>
    /// O filtro procura o texto no IP, no nome, no MAC, no fabricante e na observação. Um número
    /// que é porta aberta também acha o host: "9100" lista as impressoras.
    /// </summary>
    public bool Contem(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return true;
        }

        var t = texto.Trim();

        // MAC também pode ser procurado sem separador ou com hífen, como o Windows mostra.
        var macBusca = t.Replace(":", string.Empty).Replace("-", string.Empty);
        return Ip.Contains(t, StringComparison.OrdinalIgnoreCase)
            || Nome.Contains(t, StringComparison.OrdinalIgnoreCase)
            || Mac.Contains(t, StringComparison.OrdinalIgnoreCase)
            || (macBusca.Length > 0 && Mac.Replace(":", string.Empty).Contains(macBusca, StringComparison.OrdinalIgnoreCase))
            || Fabricante.Contains(t, StringComparison.OrdinalIgnoreCase)
            || Observacao.Contains(t, StringComparison.OrdinalIgnoreCase)
            || _host.PortasAbertas.Any(p => p.ToString(System.Globalization.CultureInfo.InvariantCulture) == t);
    }

    private void Avisar([CallerMemberName] string? propriedade = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propriedade));
}

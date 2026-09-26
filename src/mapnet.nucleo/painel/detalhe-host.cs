using System.Net;
using System.Net.Sockets;

namespace MapNet.Nucleo;

/// <summary>Ações do painel de detalhe que abrem outro programa do Windows.</summary>
public enum AcaoHost
{
    AbrirHttp,
    AbrirHttps,
    AreaDeTrabalhoRemota,
    PastaCompartilhada,
}

/// <summary>
/// Linhas e ações do painel de detalhe do host. O destino de cada ação é montado só a partir do
/// IP, que já passou pelo <see cref="IPAddress"/>: nome de host, que vem da rede e pode ter
/// qualquer coisa, nunca entra na linha de comando.
/// </summary>
public static class DetalheHost
{
    /// <summary>Arquivo (ou endereço) e argumentos para abrir no Windows. Só IPv4.</summary>
    public static (string Arquivo, string? Argumentos) Destino(AcaoHost acao, IPAddress ip)
    {
        if (ip.AddressFamily != AddressFamily.InterNetwork)
        {
            throw new ArgumentException("o painel de detalhe só abre endereço IPv4", nameof(ip));
        }

        var texto = ip.ToString();
        return acao switch
        {
            AcaoHost.AbrirHttp => ($"http://{texto}/", null),
            AcaoHost.AbrirHttps => ($"https://{texto}/", null),
            AcaoHost.AreaDeTrabalhoRemota => ("mstsc.exe", $"/v:{texto}"),
            AcaoHost.PastaCompartilhada => ($@"\\{texto}", null),
            _ => throw new ArgumentOutOfRangeException(nameof(acao)),
        };
    }

    /// <summary>Pista do sistema pelo TTL da resposta ao ping.</summary>
    public static string? PistaDoTtl(int? ttl) => ttl switch
    {
        null => null,
        > 128 => "equipamento de rede (TTL inicial 255)",
        > 64 => "provavelmente Windows (TTL inicial 128)",
        > 0 => "provavelmente Linux, Android, macOS ou iOS (TTL inicial 64)",
        _ => null,
    };

    public static IReadOnlyList<ItemMinhaMaquina> Itens(HostEncontrado h)
    {
        var ping = h.RespondeuPing ? $"respondeu em {h.TempoPingMs} ms" + (h.Ttl.HasValue ? $", TTL {h.Ttl}" : "") : "não respondeu";
        var itens = new List<ItemMinhaMaquina>
        {
            new("IP", h.Ip.ToString()),
            new("MAC", h.MacTexto.Length > 0 ? h.MacTexto : "não obtido"),
            new("Fabricante", h.Fabricante.Length > 0 ? h.Fabricante : "não identificado"),
            new("Nome pelo DNS reverso", h.NomeDns ?? "sem resposta"),
            new("Nome NetBIOS", h.NomeNetBios ?? "sem resposta"),
            new("Grupo de trabalho ou domínio", h.GrupoNetBios ?? "sem resposta"),
            new("Nome mDNS", h.NomeMdns ?? "sem resposta"),
            new("Ping", ping),
        };
        if (PistaDoTtl(h.Ttl) is { } pista)
        {
            itens.Add(new("Sistema", pista));
        }

        itens.Add(new("ARP", h.RespondeuArp ? "respondeu" : h.EhEsteComputador ? "não se aplica (este computador)" : "não respondeu"));
        if (h.Marcas.Count > 0)
        {
            itens.Add(new("Observação", string.Join(", ", h.Marcas)));
        }

        return itens;
    }

    /// <summary>Texto para o botão Copiar: uma linha por item.</summary>
    public static string Texto(HostEncontrado h) =>
        string.Join(Environment.NewLine, Itens(h).Select(i => $"{i.Rotulo}: {i.Valor}"));
}

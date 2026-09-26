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
    public static (string Arquivo, string? Argumentos) Destino(AcaoHost acao, IPAddress ip, int? porta = null)
    {
        if (ip.AddressFamily != AddressFamily.InterNetwork)
        {
            throw new ArgumentException("o painel de detalhe só abre endereço IPv4", nameof(ip));
        }

        var texto = ip.ToString();
        var sufixo = porta is int p ? ":" + p.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
        return acao switch
        {
            AcaoHost.AbrirHttp => ($"http://{texto}{sufixo}/", null),
            AcaoHost.AbrirHttps => ($"https://{texto}{sufixo}/", null),
            AcaoHost.AreaDeTrabalhoRemota => ("mstsc.exe", $"/v:{texto}"),
            AcaoHost.PastaCompartilhada => ($@"\\{texto}", null),
            _ => throw new ArgumentOutOfRangeException(nameof(acao)),
        };
    }

    /// <summary>
    /// Porta que o atalho http ou https deve usar. Quando a porta comum (80 ou 443) está fechada
    /// e uma alternativa está aberta, como a 8080 de muito painel de câmera, vai a alternativa.
    /// Null é a porta comum.
    /// </summary>
    public static int? PortaWeb(HostEncontrado h, bool https)
    {
        if (!h.PortasVerificadas)
        {
            return null;
        }

        var (comum, alternativas) = https ? (443, new[] { 8443 }) : (80, new[] { 8080, 8000 });
        if (h.PortasAbertas.Contains(comum))
        {
            return null;
        }

        return alternativas.FirstOrDefault(h.PortasAbertas.Contains) is var a and > 0 ? a : null;
    }

    /// <summary>
    /// Diz se vale oferecer a ação. Sem verificação de portas, ou quando nenhuma porta do serviço
    /// entrou na lista, não há como saber, e a ação fica ligada. Quando as portas do serviço foram
    /// verificadas e todas estão fechadas, a ação desliga: abrir daria só erro.
    /// </summary>
    public static bool Disponivel(HostEncontrado h, AcaoHost acao)
    {
        int[] portas = acao switch
        {
            AcaoHost.AbrirHttp => [80, 8080, 8000],
            AcaoHost.AbrirHttps => [443, 8443],
            AcaoHost.AreaDeTrabalhoRemota => [3389],
            AcaoHost.PastaCompartilhada => [445, 139],
            _ => [],
        };
        var testadas = portas.Where(h.PortasTestadas.Contains).ToList();
        return testadas.Count == 0 || testadas.Any(h.PortasAbertas.Contains);
    }

    /// <summary>Texto das portas para o detalhe e o relatório.</summary>
    public static string TextoPortas(HostEncontrado h) =>
        h.PortasVerificadas
            ? (h.PortasAbertas.Count > 0 ? ListaPortas.Texto(h.PortasAbertas) : "nenhuma das portas verificadas está aberta")
            : h.MotivoSemPortas ?? "não verificadas nesta varredura";

    /// <summary>Uma linha por serviço, para o detalhe e o relatório.</summary>
    public static string TextoServicos(HostEncontrado h) =>
        h.Servicos.Count > 0 ? string.Join(Environment.NewLine, h.Servicos.Select(s => s.Texto)) : "nenhum serviço se identificou";

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
            new("Tipo provável", h.Classificacao.Texto),
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
        itens.Add(new("Portas abertas", TextoPortas(h)));
        if (h.ServicosIdentificados)
        {
            itens.Add(new("Serviços", TextoServicos(h)));
        }
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

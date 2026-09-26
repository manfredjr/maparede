namespace MapNet.Nucleo;

/// <summary>Tipo provável do equipamento. A ordem desempata: em pontuação igual, vale o que vem antes.</summary>
public enum TipoEquipamento
{
    Desconhecido,
    Roteador,
    Impressora,
    Camera,
    Nas,
    ComputadorWindows,
    Linux,
    AccessPoint,
    TvMidia,
    Console,
    Celular,
    Automacao,
}

/// <summary>Tipo provável e os sinais que levaram a ele, para o técnico conferir.</summary>
public sealed record Classificacao(TipoEquipamento Tipo, IReadOnlyList<string> Motivos)
{
    public static readonly Classificacao Nenhuma = new(TipoEquipamento.Desconhecido, []);

    public string Nome => Classificador.Nome(Tipo);

    /// <summary>"Impressora (porta 9100 aberta, fabricante Brother)".</summary>
    public string Texto => Tipo == TipoEquipamento.Desconhecido ? "não deu para saber" : $"{Nome} ({string.Join(", ", Motivos)})";
}

/// <summary>
/// Classificação por heurística, com o que as outras etapas já levantaram: fabricante, nomes,
/// TTL, portas, serviços e marcas. Não manda nada para a rede. Cada sinal soma pontos para um
/// tipo e deixa o motivo escrito. Ganha o tipo com mais pontos, a partir de um mínimo; abaixo
/// dele, fica Desconhecido. É um palpite com os motivos à vista, não uma certeza.
/// </summary>
public static class Classificador
{
    private const int Minimo = 3;

    public static string Nome(TipoEquipamento tipo) => tipo switch
    {
        TipoEquipamento.Roteador => "Roteador",
        TipoEquipamento.Impressora => "Impressora",
        TipoEquipamento.Camera => "Câmera ou gravador",
        TipoEquipamento.Nas => "NAS",
        TipoEquipamento.ComputadorWindows => "Computador Windows",
        TipoEquipamento.Linux => "Linux",
        TipoEquipamento.AccessPoint => "Access point",
        TipoEquipamento.TvMidia => "TV ou mídia",
        TipoEquipamento.Console => "Videogame",
        TipoEquipamento.Celular => "Celular ou tablet",
        TipoEquipamento.Automacao => "Automação",
        _ => "Desconhecido",
    };

    private static readonly (string Trecho, TipoEquipamento Tipo, int Pontos)[] _fabricantes =
    [
        ("Hewlett Packard", TipoEquipamento.Impressora, 2), ("HP Inc", TipoEquipamento.Impressora, 2), ("Brother", TipoEquipamento.Impressora, 3),
        ("Seiko Epson", TipoEquipamento.Impressora, 3), ("Canon", TipoEquipamento.Impressora, 2), ("Lexmark", TipoEquipamento.Impressora, 3),
        ("Xerox", TipoEquipamento.Impressora, 3), ("Ricoh", TipoEquipamento.Impressora, 3), ("Kyocera", TipoEquipamento.Impressora, 3),
        ("Hikvision", TipoEquipamento.Camera, 3), ("Dahua", TipoEquipamento.Camera, 3), ("Axis Communications", TipoEquipamento.Camera, 3),
        ("Synology", TipoEquipamento.Nas, 4), ("QNAP", TipoEquipamento.Nas, 4),
        ("Ubiquiti", TipoEquipamento.AccessPoint, 3), ("Ruckus", TipoEquipamento.AccessPoint, 3), ("Aruba", TipoEquipamento.AccessPoint, 2),
        ("MikroTik", TipoEquipamento.Roteador, 2), ("Routerboard", TipoEquipamento.Roteador, 2),
        ("Tuya", TipoEquipamento.Automacao, 4), ("Espressif", TipoEquipamento.Automacao, 3), ("Shelly", TipoEquipamento.Automacao, 4),
        ("Philips Lighting", TipoEquipamento.Automacao, 4), ("Signify", TipoEquipamento.Automacao, 4), ("iTEAD", TipoEquipamento.Automacao, 4),
        ("Sony Interactive", TipoEquipamento.Console, 4), ("Nintendo", TipoEquipamento.Console, 4),
        ("Roku", TipoEquipamento.TvMidia, 4), ("Raspberry Pi", TipoEquipamento.Linux, 2),
    ];

    private static readonly (string Trecho, TipoEquipamento Tipo, int Pontos)[] _palavras =
    [
        ("impressora", TipoEquipamento.Impressora, 3), ("printer", TipoEquipamento.Impressora, 3), ("laserjet", TipoEquipamento.Impressora, 4),
        ("officejet", TipoEquipamento.Impressora, 4), ("deskjet", TipoEquipamento.Impressora, 4), ("ecotank", TipoEquipamento.Impressora, 4),
        ("câmera", TipoEquipamento.Camera, 3), ("camera", TipoEquipamento.Camera, 3), ("nvr", TipoEquipamento.Camera, 3), ("dvr", TipoEquipamento.Camera, 3),
        ("gravador", TipoEquipamento.Camera, 2), ("ipc", TipoEquipamento.Camera, 1),
        ("roteador", TipoEquipamento.Roteador, 3), ("router", TipoEquipamento.Roteador, 3), ("gateway", TipoEquipamento.Roteador, 2),
        ("access point", TipoEquipamento.AccessPoint, 3), ("unifi", TipoEquipamento.AccessPoint, 3),
        ("nas", TipoEquipamento.Nas, 2), ("diskstation", TipoEquipamento.Nas, 4),
        ("webos", TipoEquipamento.TvMidia, 4), ("tv", TipoEquipamento.TvMidia, 2), ("chromecast", TipoEquipamento.TvMidia, 4), ("tizen", TipoEquipamento.TvMidia, 3),
        ("ps4", TipoEquipamento.Console, 4), ("ps5", TipoEquipamento.Console, 4), ("xbox", TipoEquipamento.Console, 4),
        ("iphone", TipoEquipamento.Celular, 4), ("ipad", TipoEquipamento.Celular, 4), ("android", TipoEquipamento.Celular, 3), ("galaxy", TipoEquipamento.Celular, 3),
    ];

    public static Classificacao Classificar(HostEncontrado h)
    {
        var pontos = new Dictionary<TipoEquipamento, int>();
        var motivos = new Dictionary<TipoEquipamento, List<string>>();

        void Somar(TipoEquipamento tipo, int valor, string motivo)
        {
            pontos[tipo] = pontos.GetValueOrDefault(tipo) + valor;
            if (!motivos.TryGetValue(tipo, out var lista))
            {
                motivos[tipo] = lista = [];
            }

            if (!lista.Contains(motivo))
            {
                lista.Add(motivo);
            }
        }

        bool Aberta(int porta) => h.PortasAbertas.Contains(porta);

        if (h.EhGateway)
        {
            Somar(TipoEquipamento.Roteador, 5, "é o gateway da rede");
        }

        if (h.EhEsteComputador)
        {
            Somar(TipoEquipamento.ComputadorWindows, 6, "é este computador, e o MapNet roda no Windows");
        }
        else if (h.MacAleatorio)
        {
            Somar(TipoEquipamento.Celular, 3, "MAC aleatório");
        }

        foreach (var (trecho, tipo, valor) in _fabricantes)
        {
            if (h.Fabricante.Contains(trecho, StringComparison.OrdinalIgnoreCase))
            {
                Somar(tipo, valor, $"fabricante {h.Fabricante}");
            }
        }

        // Nomes e textos dos serviços: nome do host, modelo UPnP, título da página, certificado.
        var textos = new[] { h.NomeDns, h.NomeNetBios, h.NomeMdns }
            .Concat(h.Servicos.SelectMany(s => new[] { s.Modelo, s.Titulo, s.Servidor }))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t!)
            .ToList();
        foreach (var (trecho, tipo, valor) in _palavras)
        {
            var achado = textos.FirstOrDefault(t => ContemPalavra(t, trecho));
            if (achado != null)
            {
                Somar(tipo, valor, $"\"{Cortar(achado)}\"");
            }
        }

        if (Aberta(9100) || Aberta(515) || Aberta(631))
        {
            Somar(TipoEquipamento.Impressora, 4, $"porta de impressão aberta ({string.Join(", ", new[] { 9100, 515, 631 }.Where(Aberta))})");
        }

        if (Aberta(554) || Aberta(37777))
        {
            Somar(TipoEquipamento.Camera, 4, $"porta de câmera aberta ({string.Join(", ", new[] { 554, 37777 }.Where(Aberta))})");
        }

        if (Aberta(445) || Aberta(135) || Aberta(3389))
        {
            Somar(TipoEquipamento.ComputadorWindows, 3, $"portas do Windows abertas ({string.Join(", ", new[] { 135, 139, 445, 3389 }.Where(Aberta))})");
        }

        if (Aberta(5000) && (Aberta(445) || Aberta(139)))
        {
            Somar(TipoEquipamento.Nas, 2, "portas 5000 e de compartilhamento abertas");
        }

        if (Aberta(8291))
        {
            Somar(TipoEquipamento.Roteador, 4, "porta 8291 (Winbox) aberta");
        }

        if (Aberta(53) && (Aberta(80) || Aberta(443)))
        {
            Somar(TipoEquipamento.Roteador, 2, "DNS e página web abertos");
        }

        if (h.Servicos.Any(s => s.Protocolo == "SSH") && !Aberta(445))
        {
            Somar(TipoEquipamento.Linux, 2, "SSH aberto");
        }

        if (h.Ttl is > 64 and <= 128 && h.NomeNetBios != null)
        {
            Somar(TipoEquipamento.ComputadorWindows, 2, "TTL 128 e nome NetBIOS");
        }

        if (pontos.Count == 0)
        {
            return Classificacao.Nenhuma;
        }

        var melhor = pontos.OrderByDescending(p => p.Value).ThenBy(p => (int)p.Key).First();
        return melhor.Value >= Minimo ? new Classificacao(melhor.Key, motivos[melhor.Key]) : Classificacao.Nenhuma;
    }

    /// <summary>Palavra inteira, para "tv" não achar "tvbox" nem "nas" achar "nascimento".</summary>
    private static bool ContemPalavra(string texto, string palavra)
    {
        var i = 0;
        while ((i = texto.IndexOf(palavra, i, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var antes = i == 0 || !char.IsLetterOrDigit(texto[i - 1]);
            var fim = i + palavra.Length;
            var depois = fim >= texto.Length || !char.IsLetterOrDigit(texto[fim]);
            if (antes && depois)
            {
                return true;
            }

            i = fim;
        }

        return false;
    }

    private static string Cortar(string texto) => texto.Length <= 40 ? texto : texto[..37] + "...";
}

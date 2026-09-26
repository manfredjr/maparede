using System.Globalization;

namespace MapNet.Nucleo;

public enum ComandoCli
{
    Janela,
    Ajuda,
    Versao,
    ListarInterfaces,
    Varrer,
}

/// <summary>Argumentos da linha de comando já interpretados.</summary>
public sealed class ArgumentosCli
{
    public const string TextoAjuda = """
        MapNet - MT: inventário da rede local
        https://mapnet.manfred.com.br

        Uso:
          mapnet                        abre a janela
          mapnet --interfaces           lista as interfaces de rede ativas
          mapnet --varrer [opções]      varre a sub-rede e grava o relatório HTML
          mapnet --ajuda                mostra esta ajuda
          mapnet --versao               mostra a versão

        Opções de --varrer:
          --interface <n ou nome>   interface pelo número de --interfaces ou por parte do nome
                                    (padrão: a primeira da lista, que é a que tem gateway)
          --saida <pasta ou arquivo> onde gravar o relatório (padrão: pasta atual). Arquivo
                                    terminado em .html, .csv ou .xml define o formato
          --formato <f>             html, csv, xml ou todos (padrão: html). Com todos,
                                    grava os três na pasta de --saida
          --abrir                   abre o relatório no navegador ao terminar
          --tempo-ping <ms>         espera de cada ping, de 100 a 10000 (padrão: 1000)
          --paralelo <n>            endereços sondados ao mesmo tempo, de 1 a 256 (padrão: 64)
          --sem-arp                 usa só o ping para descobrir hosts
          --portas [lista]          verifica as portas TCP dos hosts encontrados, só abrindo
                                    e fechando a conexão. Sem lista, usa as 24 portas comuns;
                                    com lista, por exemplo 22,80,443 (até 100 portas)
          --incluir-mac-aleatorio   inclui nas portas os aparelhos com MAC aleatório, quase
                                    sempre pessoais, que ficam de fora por padrão
          --sem-identificar         com --portas, não identifica os serviços. Sem esta
                                    opção, nas portas abertas o programa pede a página
                                    inicial web, lê o certificado HTTPS e o banner de SSH,
                                    FTP e SMTP, e faz uma busca UPnP na rede local

        Exemplos:
          mapnet --varrer --interface Wi-Fi --saida C:\Relatorios --abrir
          mapnet --varrer --portas 80,443,9100

        Use a verificação de portas só em rede que você tem autorização para verificar.
        """;

    public ComandoCli Comando { get; private set; } = ComandoCli.Janela;

    public string? Interface { get; private set; }

    public string? Saida { get; private set; }

    public bool Abrir { get; private set; }

    /// <summary>Formatos a gravar. Padrão: só o HTML.</summary>
    public IReadOnlyList<FormatoRelatorio> Formatos { get; private set; } = [FormatoRelatorio.Html];

    public OpcoesVarredura Opcoes { get; } = new();

    public List<string> Erros { get; } = [];

    public bool Valido => Erros.Count == 0;

    public static ArgumentosCli Interpretar(IReadOnlyList<string> args)
    {
        var a = new ArgumentosCli();
        var comandos = 0;
        var semIdentificar = false;
        var formatoDado = false;

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i].Trim();
            var nome = arg.StartsWith('/') && arg.Length > 1 ? "--" + arg[1..] : arg;
            switch (nome.ToLowerInvariant())
            {
                case "--ajuda" or "-h" or "--help" or "-?" or "--?":
                    a.Comando = ComandoCli.Ajuda;
                    comandos++;
                    break;
                case "--versao" or "--version":
                    a.Comando = ComandoCli.Versao;
                    comandos++;
                    break;
                case "--interfaces":
                    a.Comando = ComandoCli.ListarInterfaces;
                    comandos++;
                    break;
                case "--varrer":
                    a.Comando = ComandoCli.Varrer;
                    comandos++;
                    break;
                case "--interface":
                    a.Interface = Valor(args, ref i, arg, a.Erros);
                    break;
                case "--saida":
                    a.Saida = Valor(args, ref i, arg, a.Erros);
                    break;
                case "--abrir":
                    a.Abrir = true;
                    break;
                case "--formato":
                    var formato = Valor(args, ref i, arg, a.Erros)?.Trim().ToLowerInvariant();
                    IReadOnlyList<FormatoRelatorio>? escolhidos = formato switch
                    {
                        null => null,
                        "html" => [FormatoRelatorio.Html],
                        "csv" => [FormatoRelatorio.Csv],
                        "xml" => [FormatoRelatorio.Xml],
                        "todos" => [FormatoRelatorio.Html, FormatoRelatorio.Csv, FormatoRelatorio.Xml],
                        _ => null,
                    };
                    if (escolhidos is null && formato != null)
                    {
                        a.Erros.Add($"Formato desconhecido: {formato}. Use html, csv, xml ou todos.");
                    }

                    a.Formatos = escolhidos ?? a.Formatos;
                    formatoDado = true;
                    break;
                case "--sem-arp":
                    a.Opcoes.UsarArp = false;
                    break;
                case "--portas":
                    a.Opcoes.OlharPortas = true;
                    var lista = i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[++i] : null;
                    if (ListaPortas.Interpretar(lista, out var erroPortas) is { } portas)
                    {
                        a.Opcoes.Portas = portas;
                    }
                    else
                    {
                        a.Erros.Add(erroPortas!);
                    }

                    break;
                case "--incluir-mac-aleatorio":
                    a.Opcoes.PortasEmMacAleatorio = true;
                    break;
                case "--sem-identificar":
                    a.Opcoes.IdentificarServicos = false;
                    semIdentificar = true;
                    break;
                case "--tempo-ping":
                    if (Numero(Valor(args, ref i, arg, a.Erros), 100, 10000, arg, a.Erros) is int tempo)
                    {
                        a.Opcoes.TempoPingMs = tempo;
                    }

                    break;
                case "--paralelo":
                    if (Numero(Valor(args, ref i, arg, a.Erros), 1, 256, arg, a.Erros) is int paralelo)
                    {
                        a.Opcoes.Paralelismo = paralelo;
                    }

                    break;
                default:
                    a.Erros.Add($"Opção desconhecida: {arg}");
                    break;
            }
        }

        if (comandos > 1)
        {
            a.Erros.Add("Use só um comando por vez: --varrer, --interfaces, --ajuda ou --versao.");
        }

        var opcoesDeVarredura = a.Interface != null || a.Saida != null || a.Abrir || a.Opcoes.OlharPortas || formatoDado;
        if (opcoesDeVarredura && a.Comando == ComandoCli.Janela)
        {
            a.Erros.Add("As opções --interface, --saida, --formato, --abrir e --portas pedem o comando --varrer.");
        }

        // Arquivo com extensão conhecida em --saida define o formato. Com mais de um formato, --saida tem que ser pasta.
        if (a.Saida is { } saida && Path.HasExtension(saida) && Path.GetExtension(saida).ToLowerInvariant() is ".html" or ".htm" or ".csv" or ".xml")
        {
            if (a.Formatos.Count > 1)
            {
                a.Erros.Add("Com --formato todos, --saida tem que ser uma pasta.");
            }
            else
            {
                a.Formatos = [Relatorios.FormatoDe(saida)];
            }
        }

        if (a.Opcoes.PortasEmMacAleatorio && !a.Opcoes.OlharPortas)
        {
            a.Erros.Add("A opção --incluir-mac-aleatorio só vale junto com --portas.");
        }

        if (semIdentificar && !a.Opcoes.OlharPortas)
        {
            a.Erros.Add("A opção --sem-identificar só vale junto com --portas.");
        }

        return a;
    }

    /// <summary>
    /// Arquivos a gravar, um por formato. Sem --saida, vão para a pasta atual; com uma pasta, para
    /// ela; com um arquivo de extensão conhecida, é ele mesmo.
    /// </summary>
    public IReadOnlyList<string> Caminhos(ResultadoVarredura resultado, string pastaAtual)
    {
        if (Saida is { } saida && Formatos.Count == 1
            && Path.GetExtension(saida).ToLowerInvariant() is ".html" or ".htm" or ".csv" or ".xml")
        {
            return [saida];
        }

        var pasta = string.IsNullOrWhiteSpace(Saida) ? pastaAtual : Saida;
        return Formatos.Select(f => Path.Combine(pasta, Relatorios.NomeArquivo(resultado, f))).ToList();
    }

    /// <summary>
    /// Escolhe a interface pelo número (a partir de 1, na ordem de --interfaces) ou por parte
    /// do nome ou da descrição. Sem seletor, fica a primeira da lista.
    /// </summary>
    public static InterfaceRede? EscolherInterface(IReadOnlyList<InterfaceRede> interfaces, string? seletor, out string? erro)
    {
        erro = null;
        if (interfaces.Count == 0)
        {
            erro = "Nenhuma interface de rede ativa com IPv4 foi encontrada.";
            return null;
        }

        if (string.IsNullOrWhiteSpace(seletor))
        {
            return interfaces[0];
        }

        if (int.TryParse(seletor, NumberStyles.None, CultureInfo.InvariantCulture, out var numero))
        {
            if (numero >= 1 && numero <= interfaces.Count)
            {
                return interfaces[numero - 1];
            }

            erro = $"Não existe a interface {numero}. Use um número de 1 a {interfaces.Count}.";
            return null;
        }

        var achadas = interfaces
            .Where(i => i.Nome.Equals(seletor, StringComparison.CurrentCultureIgnoreCase)
                || i.Ip.ToString() == seletor)
            .ToList();
        if (achadas.Count == 0)
        {
            achadas = interfaces
                .Where(i => i.Nome.Contains(seletor, StringComparison.CurrentCultureIgnoreCase)
                    || i.Descricao.Contains(seletor, StringComparison.CurrentCultureIgnoreCase))
                .ToList();
        }

        if (achadas.Count == 1)
        {
            return achadas[0];
        }

        erro = achadas.Count == 0
            ? $"Nenhuma interface corresponde a \"{seletor}\". Veja a lista com --interfaces."
            : $"Mais de uma interface corresponde a \"{seletor}\". Use o número mostrado em --interfaces.";
        return null;
    }

    private static string? Valor(IReadOnlyList<string> args, ref int i, string opcao, List<string> erros)
    {
        if (i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            i++;
            return args[i];
        }

        erros.Add($"Falta o valor de {opcao}.");
        return null;
    }

    private static int? Numero(string? texto, int minimo, int maximo, string opcao, List<string> erros)
    {
        if (texto is null)
        {
            return null;
        }

        if (int.TryParse(texto, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= minimo && n <= maximo)
        {
            return n;
        }

        erros.Add($"Valor inválido em {opcao}: {texto}. Use um número de {minimo} a {maximo}.");
        return null;
    }
}

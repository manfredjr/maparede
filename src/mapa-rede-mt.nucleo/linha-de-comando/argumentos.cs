using System.Globalization;

namespace MapaRedeMt.Nucleo;

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
        MT Mapa de Rede - inventário da rede local

        Uso:
          mt-mapa-rede                        abre a janela
          mt-mapa-rede --interfaces           lista as interfaces de rede ativas
          mt-mapa-rede --varrer [opções]      varre a sub-rede e grava o relatório HTML
          mt-mapa-rede --ajuda                mostra esta ajuda
          mt-mapa-rede --versao               mostra a versão

        Opções de --varrer:
          --interface <n ou nome>   interface pelo número de --interfaces ou por parte do nome
                                    (padrão: a primeira da lista, que é a que tem gateway)
          --saida <pasta ou .html>  onde gravar o relatório (padrão: pasta atual)
          --abrir                   abre o relatório no navegador ao terminar
          --tempo-ping <ms>         espera de cada ping, de 100 a 10000 (padrão: 1000)
          --paralelo <n>            endereços sondados ao mesmo tempo, de 1 a 256 (padrão: 64)
          --sem-arp                 usa só o ping para descobrir hosts

        Exemplo:
          mt-mapa-rede --varrer --interface Wi-Fi --saida C:\Relatorios --abrir
        """;

    public ComandoCli Comando { get; private set; } = ComandoCli.Janela;

    public string? Interface { get; private set; }

    public string? Saida { get; private set; }

    public bool Abrir { get; private set; }

    public OpcoesVarredura Opcoes { get; } = new();

    public List<string> Erros { get; } = [];

    public bool Valido => Erros.Count == 0;

    public static ArgumentosCli Interpretar(IReadOnlyList<string> args)
    {
        var a = new ArgumentosCli();
        var comandos = 0;

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
                case "--sem-arp":
                    a.Opcoes.UsarArp = false;
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

        var opcoesDeVarredura = a.Interface != null || a.Saida != null || a.Abrir;
        if (opcoesDeVarredura && a.Comando == ComandoCli.Janela)
        {
            a.Erros.Add("As opções --interface, --saida e --abrir pedem o comando --varrer.");
        }

        return a;
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

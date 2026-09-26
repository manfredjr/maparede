using System.Globalization;
using System.Net;
using System.Text;

namespace MapNet.Nucleo;

/// <summary>
/// Relatório HTML em arquivo único: estilo e script vão dentro do próprio arquivo, para abrir
/// em qualquer navegador sem internet. Todo texto que vem da rede passa por codificação HTML,
/// porque nome de equipamento é escolhido por quem configurou o equipamento.
/// </summary>
public static class RelatorioHtml
{
    private static readonly CultureInfo _ptBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Nome sugerido, por exemplo mapnet-20260925-1430-192-168-0-0-24.html.</summary>
    public static string NomeArquivo(ResultadoVarredura resultado)
    {
        var rede = resultado.SubRedeVarrida.Rede.ToString().Replace('.', '-');
        return $"mapnet-{resultado.Inicio:yyyyMMdd-HHmm}-{rede}-{resultado.SubRedeVarrida.Prefixo}.html";
    }

    public static async Task<string> SalvarAsync(ResultadoVarredura resultado, string caminho, CancellationToken cancelamento = default)
    {
        var pasta = Path.GetDirectoryName(Path.GetFullPath(caminho));
        if (!string.IsNullOrEmpty(pasta))
        {
            Directory.CreateDirectory(pasta);
        }

        await File.WriteAllTextAsync(caminho, Gerar(resultado), new UTF8Encoding(false), cancelamento).ConfigureAwait(false);
        return Path.GetFullPath(caminho);
    }

    public static string Gerar(ResultadoVarredura r)
    {
        var hosts = r.Hosts.OrderBy(h => h.IpNumero).ToList();
        var comNome = hosts.Count(h => h.Nome.Length > 0);
        var comFabricante = hosts.Count(h => h.Mac != null && h.Fabricante != TabelaOui.TextoNaoIdentificado && h.Fabricante != TabelaOui.TextoMacAleatorio);
        var aleatorios = hosts.Count(h => h.MacAleatorio);
        var fabricantes = hosts
            .Where(h => h.Fabricante.Length > 0)
            .GroupBy(h => h.Fabricante)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var i = r.Interface;

        var html = new StringBuilder(64 * 1024);
        html.Append($$"""
            <!DOCTYPE html>
            <html lang="pt-BR">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="generator" content="MapNet - MT {{C(ResultadoVarredura.VersaoPrograma)}}">
            <title>Mapa de rede {{C(r.SubRedeVarrida.ToString())}} - {{C(Data(r.Inicio))}}</title>
            <style>{{Estilo}}</style>
            </head>
            <body>
            <header>
              <div class="marca">MapNet - MT</div>
              <h1>Inventário da rede {{C(r.SubRedeVarrida.ToString())}}</h1>
              <p class="sub">Varredura de {{C(Data(r.Inicio))}}, feita a partir de {{C(r.NomeComputador)}}</p>
            </header>
            <main>
            """);

        if (r.Avisos.Count > 0)
        {
            html.Append("<section class=\"avisos\"><h2>Avisos</h2><ul>");
            foreach (var aviso in r.Avisos)
            {
                html.Append($"<li>{C(aviso)}</li>");
            }

            html.Append("</ul></section>");
        }

        if (r.Maquina is { } maquina)
        {
            AcrescentarMaquina(html, maquina.Grupos(r.Fim, r.IpPublico));
        }

        html.Append($$"""
            <section class="cartoes">
              <div class="cartao"><span class="numero">{{hosts.Count}}</span><span class="rotulo">hosts encontrados</span></div>
              <div class="cartao"><span class="numero">{{comNome}}</span><span class="rotulo">com nome identificado</span></div>
              <div class="cartao"><span class="numero">{{comFabricante}}</span><span class="rotulo">com fabricante identificado</span></div>
              <div class="cartao"><span class="numero">{{aleatorios}}</span><span class="rotulo">com MAC aleatório</span></div>
            </section>
            <section class="resumo">
              <h2>Resumo da varredura</h2>
              <table class="dados">
                <tr><th>Interface</th><td>{{C(i.Nome)}} ({{C(i.TipoTexto)}}){{(i.Descricao.Length > 0 ? " - " + C(i.Descricao) : "")}}</td></tr>
                <tr><th>IP deste computador</th><td>{{C(i.Ip.ToString())}}</td></tr>
                <tr><th>Máscara</th><td>{{C(i.Mascara.ToString())}} (/{{i.Prefixo}})</td></tr>
                <tr><th>Sub-rede varrida</th><td>{{C(r.SubRedeVarrida.ToString())}}, de {{C(r.SubRedeVarrida.PrimeiroHost.ToString())}} a {{C(r.SubRedeVarrida.UltimoHost.ToString())}} ({{r.SubRedeVarrida.QuantidadeHosts}} endereços)</td></tr>
                <tr><th>Gateway</th><td>{{C(i.Gateway?.ToString() ?? "não configurado")}}</td></tr>
                <tr><th>DNS</th><td>{{C(i.Dns.Count > 0 ? string.Join(", ", i.Dns) : "não configurado")}}</td></tr>
                <tr><th>Início</th><td>{{C(Data(r.Inicio))}}</td></tr>
                <tr><th>Portas verificadas</th><td>{{C(r.PortasVerificadas is { } portas ? $"{portas.Count} porta(s) TCP, só abrindo e fechando a conexão: {ListaPortas.Texto(portas)}" : "não verificadas nesta varredura")}}</td></tr>
                <tr><th>Duração</th><td>{{C(Duracao(r.Duracao))}}{{(r.Cancelada ? " (interrompida)" : "")}}</td></tr>
                <tr><th>Computador</th><td>{{C(r.NomeComputador)}}</td></tr>
              </table>
            </section>
            <section class="hosts">
              <h2>Hosts</h2>
              <div class="filtro">
                <label for="filtro">Filtrar</label>
                <input id="filtro" type="search" placeholder="IP, nome, MAC ou fabricante" autocomplete="off">
                <span id="contagem"></span>
              </div>
              <p class="dica">Clique no título de uma coluna para ordenar e numa linha para ver o detalhe do host.</p>
              <table id="tabela" class="lista">
                <thead><tr>
                  <th data-tipo="numero" class="ordenada">IP</th>
                  <th data-tipo="texto">Nome</th>
                  <th data-tipo="texto">MAC</th>
                  <th data-tipo="texto">Fabricante</th>
                  <th data-tipo="numero">Ping (ms)</th>
                  <th data-tipo="texto">Portas</th>
                  <th data-tipo="texto">Observação</th>
                </tr></thead>
                <tbody>
            """);

        foreach (var h in hosts)
        {
            AcrescentarHost(html, h);
        }

        html.Append("""
                </tbody>
              </table>
            </section>
            """);

        html.Append("<section class=\"fabricantes\"><h2>Fabricantes</h2><table class=\"lista curta\"><thead><tr><th>Fabricante</th><th>Hosts</th></tr></thead><tbody>");
        foreach (var grupo in fabricantes)
        {
            html.Append($"<tr><td>{C(grupo.Key)}</td><td class=\"num\">{grupo.Count()}</td></tr>");
        }

        html.Append("</tbody></table></section>");

        html.Append($$"""
            </main>
            <footer>
              Relatório gerado pelo <a href="https://mapnet.manfred.com.br">MapNet - MT</a> {{C(ResultadoVarredura.VersaoPrograma)}}, da MT - Manfred Tecnologia.
              Levantamento de inventário por ping, ARP e consultas de nome{{(r.PortasVerificadas is null ? "" : ", com verificação de portas por conexão TCP")}}. O programa não testa senha nem explora falha.
            </footer>
            <script>{{Script}}</script>
            </body>
            </html>
            """);
        return html.ToString();
    }

    /// <summary>Seção "Minha máquina": o computador de onde a varredura foi feita, em blocos.</summary>
    private static void AcrescentarMaquina(StringBuilder html, IReadOnlyList<GrupoMinhaMaquina> grupos)
    {
        html.Append("<section class=\"maquina\"><h2>Minha máquina</h2><div class=\"grupos\">");
        foreach (var grupo in grupos)
        {
            html.Append($"<div class=\"grupo\"><h3>{C(grupo.Titulo)}</h3><table class=\"dados\">");
            foreach (var item in grupo.Itens)
            {
                html.Append($"<tr><th>{C(item.Rotulo)}</th><td>{C(item.Valor)}</td></tr>");
            }

            html.Append("</table></div>");
        }

        html.Append("</div></section>");
    }

    private static void AcrescentarHost(StringBuilder html, HostEncontrado h)
    {
        var marcas = h.Marcas;
        var busca = string.Join(' ', h.Ip, h.Nome, h.NomeDns, h.NomeNetBios, h.NomeMdns, h.GrupoNetBios, h.MacTexto, h.Fabricante, string.Join(' ', marcas), h.PortasTexto)
            .ToLowerInvariant();
        var classe = h.EhGateway ? " class=\"host gateway\"" : h.EhEsteComputador ? " class=\"host proprio\"" : " class=\"host\"";

        html.Append($"<tr{classe} data-busca=\"{C(busca)}\" tabindex=\"0\">");
        html.Append($"<td data-valor=\"{h.IpNumero}\">{C(h.Ip.ToString())}</td>");
        html.Append($"<td>{C(h.Nome)}</td>");
        html.Append($"<td class=\"mono\">{C(h.MacTexto)}</td>");
        html.Append($"<td>{C(h.Fabricante)}</td>");
        html.Append($"<td class=\"num\" data-valor=\"{(h.TempoPingMs ?? -1)}\">{(h.TempoPingMs.HasValue ? h.TempoPingMs.Value.ToString(_ptBr) : "-")}</td>");
        html.Append($"<td class=\"mono\" title=\"{C(h.PortasAbertas.Count > 0 ? ListaPortas.Texto(h.PortasAbertas) : null)}\">{C(h.PortasTexto)}</td>");
        html.Append($"<td>{string.Concat(marcas.Select(m => $"<span class=\"etiqueta\">{C(m)}</span>"))}</td>");
        html.Append("</tr>");

        html.Append("<tr class=\"detalhe\" hidden><td colspan=\"7\"><dl>");
        Item(html, "IP", h.Ip.ToString());
        Item(html, "MAC", h.MacTexto.Length > 0 ? h.MacTexto : "não obtido");
        Item(html, "Fabricante", h.Fabricante.Length > 0 ? h.Fabricante : "não identificado");
        Item(html, "Nome pelo DNS reverso", h.NomeDns ?? "sem resposta");
        Item(html, "Nome NetBIOS", h.NomeNetBios ?? "sem resposta");
        Item(html, "Grupo de trabalho ou domínio (NetBIOS)", h.GrupoNetBios ?? "sem resposta");
        Item(html, "Nome mDNS", h.NomeMdns ?? "sem resposta");
        Item(html, "Nome exibido vem de", h.OrigemNome.Length > 0 ? h.OrigemNome : "nenhuma fonte respondeu");
        Item(html, "Ping", h.RespondeuPing ? $"respondeu em {h.TempoPingMs} ms" + (h.Ttl.HasValue ? $", TTL {h.Ttl}" : "") : "não respondeu");
        Item(html, "ARP", h.RespondeuArp ? "respondeu" : h.EhEsteComputador ? "não se aplica (este computador)" : "não respondeu");
        Item(html, "Portas abertas", DetalheHost.TextoPortas(h));
        html.Append("</dl></td></tr>");
    }

    private static void Item(StringBuilder html, string rotulo, string valor) =>
        html.Append($"<div><dt>{C(rotulo)}</dt><dd>{C(valor)}</dd></div>");

    /// <summary>Codifica para HTML. Serve para texto e para valor de atributo entre aspas.</summary>
    private static string C(string? texto) => WebUtility.HtmlEncode(texto ?? string.Empty);

    private static string Data(DateTimeOffset data) => data.ToString("dd/MM/yyyy HH:mm:ss", _ptBr);

    public static string Duracao(TimeSpan duracao)
    {
        if (duracao.TotalSeconds < 60)
        {
            return $"{Math.Max(0, (int)Math.Round(duracao.TotalSeconds))} s";
        }

        return $"{(int)duracao.TotalMinutes} min {duracao.Seconds} s";
    }

    private const string Estilo = """
        :root { --texto: #1f2933; --suave: #52606d; --linha: #dfe4dc; --fundo: #f5f7f2; --destaque: #0b4d24; --verde: #0f8f2f; --verde-claro: #9ad52b; --gateway: #fff4d6; --proprio: #e6f4ea; }
        * { box-sizing: border-box; }
        body { margin: 0; font-family: "Segoe UI", Arial, sans-serif; color: var(--texto); background: var(--fundo); font-size: 14px; }
        header { background: linear-gradient(90deg, var(--destaque), var(--verde)); color: #fff; padding: 20px 24px; border-bottom: 4px solid var(--verde-claro); }
        header h1 { margin: 4px 0; font-size: 22px; }
        .marca { font-size: 12px; letter-spacing: 1px; text-transform: uppercase; opacity: .85; }
        .sub { margin: 0; opacity: .9; }
        main { max-width: 1200px; margin: 0 auto; padding: 16px; }
        section { background: #fff; border: 1px solid var(--linha); border-radius: 6px; padding: 16px; margin-bottom: 16px; }
        h2 { font-size: 17px; margin: 0 0 12px; color: var(--destaque); }
        .cartoes { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 12px; background: none; border: none; padding: 0; }
        .cartao { background: #fff; border: 1px solid var(--linha); border-radius: 6px; padding: 14px; }
        .numero { display: block; font-size: 28px; font-weight: 600; color: var(--destaque); }
        .rotulo { color: var(--suave); }
        .avisos { background: #fff8e1; border-color: #f0c36d; }
        .avisos h2 { color: #8a5a00; }
        table { border-collapse: collapse; width: 100%; }
        .dados th { text-align: left; width: 200px; color: var(--suave); font-weight: 600; padding: 4px 8px 4px 0; vertical-align: top; }
        .dados td { padding: 4px 0; word-break: break-word; }
        .grupos { display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 12px 24px; }
        .grupo h3 { font-size: 13px; margin: 0 0 4px; color: var(--suave); text-transform: uppercase; letter-spacing: .5px; }
        .grupo .dados th { width: 45%; }
        .lista th, .lista td { text-align: left; padding: 7px 8px; border-bottom: 1px solid var(--linha); }
        .lista thead th { background: var(--fundo); position: sticky; top: 0; cursor: pointer; user-select: none; white-space: nowrap; }
        .lista thead th.ordenada { color: var(--destaque); text-decoration: underline; }
        .lista.curta { width: auto; min-width: 50%; }
        .lista.curta thead th { cursor: default; }
        tr.host { cursor: pointer; }
        tr.host:hover { background: #eef7e8; }
        tr.gateway { background: var(--gateway); }
        tr.proprio { background: var(--proprio); }
        tr.detalhe td { background: #fbfdf9; }
        dl { display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 6px 16px; margin: 4px 0; }
        dt { font-size: 12px; color: var(--suave); }
        dd { margin: 0; word-break: break-word; }
        .num { text-align: right; }
        .mono { font-family: Consolas, "Courier New", monospace; }
        .etiqueta { display: inline-block; font-size: 12px; background: #e5f3dc; color: var(--destaque); border-radius: 10px; padding: 1px 8px; margin: 1px 4px 1px 0; white-space: nowrap; }
        .filtro { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; margin-bottom: 8px; }
        .filtro input { flex: 1; min-width: 220px; padding: 7px 10px; border: 1px solid var(--linha); border-radius: 4px; font: inherit; }
        #contagem, .dica { color: var(--suave); font-size: 12px; }
        .dica { margin: 0 0 8px; }
        .hosts { overflow-x: auto; }
        footer { text-align: center; color: var(--suave); font-size: 12px; padding: 8px 16px 24px; }
        footer a { color: inherit; }
        @media (max-width: 600px) { .dados th { width: 42%; } header h1 { font-size: 19px; } }
        @media print {
          body { background: #fff; }
          header { color: #000; background: none; border-bottom: 2px solid var(--destaque); }
          section { border: none; padding: 0; }
          .filtro, .dica { display: none; }
          tr.detalhe { display: none; }
        }
        """;

    private const string Script = """
        (function () {
          var tabela = document.getElementById('tabela');
          var corpo = tabela.tBodies[0];
          var filtro = document.getElementById('filtro');
          var contagem = document.getElementById('contagem');

          function pares() {
            var linhas = [];
            for (var i = 0; i < corpo.rows.length; i++) {
              var linha = corpo.rows[i];
              if (linha.classList.contains('host')) { linhas.push([linha, linha.nextElementSibling]); }
            }
            return linhas;
          }

          function atualizarContagem() {
            var todos = pares();
            var visiveis = todos.filter(function (p) { return !p[0].hidden; }).length;
            contagem.textContent = 'Mostrando ' + visiveis + ' de ' + todos.length + ' hosts';
          }

          filtro.addEventListener('input', function () {
            var termo = filtro.value.trim().toLowerCase();
            pares().forEach(function (p) {
              var mostra = termo === '' || p[0].getAttribute('data-busca').indexOf(termo) >= 0;
              p[0].hidden = !mostra;
              if (!mostra) { p[1].hidden = true; }
            });
            atualizarContagem();
          });

          corpo.addEventListener('click', function (e) {
            var linha = e.target.closest('tr.host');
            if (linha) { linha.nextElementSibling.hidden = !linha.nextElementSibling.hidden; }
          });
          corpo.addEventListener('keydown', function (e) {
            var linha = e.target.closest('tr.host');
            if (linha && (e.key === 'Enter' || e.key === ' ')) { e.preventDefault(); linha.click(); }
          });

          var cabecalhos = tabela.tHead.rows[0].cells;
          var coluna = 0, crescente = true;
          Array.prototype.forEach.call(cabecalhos, function (th, indice) {
            th.addEventListener('click', function () {
              crescente = coluna === indice ? !crescente : true;
              coluna = indice;
              Array.prototype.forEach.call(cabecalhos, function (c) { c.classList.remove('ordenada'); });
              th.classList.add('ordenada');
              var numero = th.getAttribute('data-tipo') === 'numero';
              var lista = pares();
              lista.sort(function (a, b) {
                var ca = a[0].cells[indice], cb = b[0].cells[indice];
                var r;
                if (numero) {
                  r = parseFloat(ca.getAttribute('data-valor')) - parseFloat(cb.getAttribute('data-valor'));
                } else {
                  r = ca.textContent.localeCompare(cb.textContent, 'pt-BR', { sensitivity: 'base' });
                }
                return crescente ? r : -r;
              });
              lista.forEach(function (p) { corpo.appendChild(p[0]); corpo.appendChild(p[1]); });
            });
          });

          atualizarContagem();
        })();
        """;
}

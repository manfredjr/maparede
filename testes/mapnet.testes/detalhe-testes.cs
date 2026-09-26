using System.Net;
using MapNet.Nucleo;

namespace MapNet.Testes;

/// <summary>
/// Fatia 6: painel de detalhe do host. O destino das ações sai só do IP; nome que vem da rede
/// nunca entra na linha de comando. Nenhum teste abre programa de verdade.
/// </summary>
public class DetalheTestes
{
    private static readonly IPAddress _ip = IPAddress.Parse("192.0.2.57");

    [Theory]
    [InlineData(AcaoHost.AbrirHttp, "http://192.0.2.57/", null)]
    [InlineData(AcaoHost.AbrirHttps, "https://192.0.2.57/", null)]
    [InlineData(AcaoHost.AreaDeTrabalhoRemota, "mstsc.exe", "/v:192.0.2.57")]
    [InlineData(AcaoHost.PastaCompartilhada, @"\\192.0.2.57", null)]
    public void Destino_de_cada_acao_sai_do_ip(AcaoHost acao, string arquivo, string? argumentos)
    {
        Assert.Equal((arquivo, argumentos), DetalheHost.Destino(acao, _ip));
    }

    [Fact]
    public void Destino_so_aceita_ipv4()
    {
        Assert.Throws<ArgumentException>(() => DetalheHost.Destino(AcaoHost.AbrirHttp, IPAddress.Parse("2001:db8::1")));
    }

    [Theory]
    [InlineData(255, "equipamento de rede (TTL inicial 255)")]
    [InlineData(250, "equipamento de rede (TTL inicial 255)")]
    [InlineData(128, "provavelmente Windows (TTL inicial 128)")]
    [InlineData(64, "provavelmente Linux, Android, macOS ou iOS (TTL inicial 64)")]
    [InlineData(null, null)]
    public void Pista_do_sistema_pelo_ttl(int? ttl, string? esperado)
    {
        Assert.Equal(esperado, DetalheHost.PistaDoTtl(ttl));
    }

    [Fact]
    public void Itens_trazem_mac_fabricante_nomes_grupo_ttl_e_marcas()
    {
        var itens = DetalheHost.Itens(Host()).ToDictionary(i => i.Rotulo, i => i.Valor);

        Assert.Equal("192.0.2.57", itens["IP"]);
        Assert.Equal("00:0C:29:12:34:57", itens["MAC"]);
        Assert.Equal("VMware, Inc.", itens["Fabricante"]);
        Assert.Equal("servidor.example", itens["Nome pelo DNS reverso"]);
        Assert.Equal("ESCRITORIO", itens["Grupo de trabalho ou domínio"]);
        Assert.Equal("respondeu em 1 ms, TTL 128", itens["Ping"]);
        Assert.Equal("provavelmente Windows (TTL inicial 128)", itens["Sistema"]);
        Assert.Equal("Gateway", itens["Observação"]);
    }

    [Fact]
    public void Texto_para_copiar_tem_uma_linha_por_item()
    {
        var texto = DetalheHost.Texto(Host());

        Assert.StartsWith("IP: 192.0.2.57", texto);
        Assert.Contains("Fabricante: VMware, Inc.", texto);
    }

    [Fact]
    public void Escolher_o_host_abre_o_detalhe_e_liga_as_acoes()
    {
        var (painel, _, _) = Painel();

        Assert.False(painel.TemHostSelecionado);
        Assert.False(painel.ComandoAbrirHttp.CanExecute(null));

        painel.HostSelecionado = new LinhaHost(Host());

        Assert.True(painel.TemHostSelecionado);
        Assert.Equal("servidor.example", painel.TituloDetalhe);
        Assert.Contains(painel.DetalheItens, i => i.Rotulo == "MAC");
        Assert.True(painel.ComandoAbrirHttp.CanExecute(null));

        painel.ComandoFecharDetalhe.Execute(null);

        Assert.False(painel.TemHostSelecionado);
        Assert.Empty(painel.DetalheItens);
    }

    [Fact]
    public void Nome_malicioso_vindo_da_rede_nao_entra_no_comando()
    {
        var (painel, abertos, _) = Painel();
        var host = Host();
        host.NomeDns = "x.example & calc.exe";
        host.NomeNetBios = "\"; del /q *";
        painel.HostSelecionado = new LinhaHost(host);

        painel.ComandoAreaRemota.Execute(null);
        painel.ComandoPastaCompartilhada.Execute(null);

        Assert.Equal([("mstsc.exe", "/v:192.0.2.57"), (@"\\192.0.2.57", (string?)null)], abertos);
    }

    [Fact]
    public void Falha_ao_abrir_vai_ao_console()
    {
        var painel = new PainelVarredura(Dependencias(abrir: (_, _) => throw new InvalidOperationException("sem navegador")));
        painel.HostSelecionado = new LinhaHost(Host());

        painel.AcaoNoHost(AcaoHost.AbrirHttps);

        Assert.EndsWith("Não foi possível abrir https://192.0.2.57/: sem navegador", painel.Console.Linhas[^1]);
    }

    [Fact]
    public void Copiar_dados_leva_o_texto_do_detalhe()
    {
        var (painel, _, copiados) = Painel();
        painel.HostSelecionado = new LinhaHost(Host());

        painel.ComandoCopiarHost.Execute(null);

        Assert.Single(copiados);
        Assert.StartsWith("IP: 192.0.2.57", copiados[0]);
    }

    [Fact]
    public async Task Ping_continuo_leva_o_ip_para_a_aba_ping()
    {
        ParametrosFerramenta? recebido = null;
        var painel = new PainelVarredura(Dependencias(ferramentas: [new FerramentaQueGuarda("Ping", p => recebido = p)]));
        painel.HostSelecionado = new LinhaHost(Host());

        await painel.FerramentaNoHostAsync("Ping", continuo: true);

        Assert.Equal("Ping", painel.AbaSelecionada.Titulo);
        Assert.Equal("192.0.2.57", recebido!.Alvo);
        Assert.True(recebido.Continuo);
    }

    [Fact]
    public void Dados_novos_do_host_chegam_ao_detalhe()
    {
        var (painel, _, _) = Painel();
        var linha = new LinhaHost(Host());
        painel.HostSelecionado = linha;
        var avisos = new List<string?>();
        painel.PropertyChanged += (_, e) => avisos.Add(e.PropertyName);

        var novo = Host();
        novo.NomeMdns = "servidor.local";
        linha.Atualizar(novo);

        Assert.Contains(nameof(PainelVarredura.DetalheItens), avisos);
        Assert.Equal("servidor.local", painel.DetalheItens.Single(i => i.Rotulo == "Nome mDNS").Valor);
    }

    [Fact]
    public async Task Nova_varredura_fecha_o_detalhe()
    {
        var painel = new PainelVarredura(Dependencias());
        painel.CarregarInterfaces();
        painel.HostSelecionado = new LinhaHost(Host());

        var varredura = painel.VarrerAsync();

        Assert.False(painel.TemHostSelecionado);
        await varredura;
    }

    [Fact]
    public void Textos_do_detalhe_nao_tem_caractere_proibido()
    {
        var textos = DetalheHost.Itens(Host()).Select(i => i.Rotulo + i.Valor)
            .Concat([DetalheHost.PistaDoTtl(64)!, DetalheHost.PistaDoTtl(128)!, DetalheHost.PistaDoTtl(255)!]);

        Assert.Empty(textos.SelectMany(CaracteresProibidosTestes.Proibidos));
    }

    private static HostEncontrado Host() => new()
    {
        Ip = _ip,
        Mac = [0x00, 0x0C, 0x29, 0x12, 0x34, 0x57],
        Fabricante = "VMware, Inc.",
        RespondeuPing = true,
        RespondeuArp = true,
        TempoPingMs = 1,
        Ttl = 128,
        NomeDns = "servidor.example",
        GrupoNetBios = "ESCRITORIO",
        EhGateway = true,
    };

    private static (PainelVarredura Painel, List<(string, string?)> Abertos, List<string> Copiados) Painel()
    {
        var abertos = new List<(string, string?)>();
        var copiados = new List<string>();
        var painel = new PainelVarredura(Dependencias(abrir: (a, b) => abertos.Add((a, b)), copiar: copiados.Add));
        return (painel, abertos, copiados);
    }

    private static DependenciasPainel Dependencias(
        Action<string, string?>? abrir = null,
        Action<string>? copiar = null,
        IReadOnlyList<IFerramenta>? ferramentas = null) => new()
        {
            ListarInterfaces = () => [MaquinaTestes.Interface(TipoInterface.Cabo)],
            Varrer = (i, _, _) => Task.FromResult(new ResultadoVarredura { Interface = i, SubRedeVarrida = i.SubRede }),
            SalvarEm = (_, c) => Task.FromResult(c),
            PastaRelatorios = Path.Combine(Path.GetTempPath(), "mapnet-testes-detalhe"),
            Abrir = abrir ?? ((_, _) => { }),
            Copiar = copiar ?? (_ => { }),
            Ferramentas = ferramentas is null ? null : () => ferramentas,
        };

    private sealed class FerramentaQueGuarda(string titulo, Action<ParametrosFerramenta> guardar) : IFerramenta
    {
        public string Titulo => titulo;

        public string Descricao => "Teste.";

        public bool PedeAlvo => true;

        public bool PedeServidor => false;

        public bool PodeContinuo => true;

        public Task ExecutarAsync(ParametrosFerramenta parametros, SaidaFerramenta saida, CancellationToken cancelamento)
        {
            guardar(parametros);
            return Task.CompletedTask;
        }
    }
}

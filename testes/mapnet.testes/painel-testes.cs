using System.Net;
using MapNet.Nucleo;

namespace MapNet.Testes;

/// <summary>
/// Lógica da tela principal, sem janela: estado dos botões, tabela, filtro, console e barra de
/// estado. Os dois ajustes do teste de campo de 25/09/2026 estão aqui: aviso sem janela e
/// botões certos no fim da varredura.
/// </summary>
public class PainelTestes
{
    [Fact]
    public void Parado_com_interface_pode_iniciar_e_trocar_interface()
    {
        var painel = Painel(new Simulador());

        Assert.Equal(EstadoPainel.Parado, painel.Estado);
        Assert.True(painel.PodeAcionarPrincipal);
        Assert.True(painel.PodeTrocarInterface);
        Assert.Equal("Iniciar varredura", painel.TextoBotaoPrincipal);
        Assert.False(painel.PodeAbrirRelatorio);
        Assert.False(painel.PodeSalvarComo);
    }

    [Fact]
    public void Sem_interface_nao_pode_iniciar()
    {
        var painel = new PainelVarredura(Dependencias(new Simulador(), interfaces: []));
        painel.CarregarInterfaces();

        Assert.Null(painel.InterfaceSelecionada);
        Assert.False(painel.PodeAcionarPrincipal);
        Assert.Contains("Nenhuma interface", painel.TextoAndamento);
    }

    [Fact]
    public async Task Durante_a_varredura_o_botao_principal_vira_cancelar()
    {
        var simulador = new Simulador();
        var painel = Painel(simulador);

        var varredura = painel.VarrerAsync();

        Assert.Equal(EstadoPainel.Varrendo, painel.Estado);
        Assert.Equal("Cancelar", painel.TextoBotaoPrincipal);
        Assert.True(painel.PodeAcionarPrincipal);
        Assert.False(painel.PodeTrocarInterface);
        Assert.False(painel.ComandoAtualizar.CanExecute(null));
        Assert.False(painel.PodeSalvarComo);

        simulador.Terminar(Resultado());
        await varredura;
    }

    [Fact]
    public async Task No_fim_os_botoes_voltam_ao_normal()
    {
        var simulador = new Simulador();
        var painel = Painel(simulador);

        var varredura = painel.VarrerAsync();
        simulador.Terminar(Resultado());
        await varredura;

        Assert.Equal(EstadoPainel.Parado, painel.Estado);
        Assert.Equal("Iniciar varredura", painel.TextoBotaoPrincipal);
        Assert.True(painel.PodeTrocarInterface);
        Assert.True(painel.PodeAbrirRelatorio);
        Assert.True(painel.PodeSalvarComo);
        Assert.Equal(100, painel.Progresso);
    }

    [Fact]
    public async Task Estado_volta_a_parado_antes_de_gravar_o_relatorio()
    {
        var simulador = new Simulador();
        EstadoPainel? estadoAoGravar = null;
        PainelVarredura? painel = null;
        var dependencias = Dependencias(simulador, salvar: (_, caminho) =>
        {
            estadoAoGravar = painel!.Estado;
            return Task.FromResult(caminho);
        });
        painel = new PainelVarredura(dependencias);
        painel.CarregarInterfaces();

        var varredura = painel.VarrerAsync();
        simulador.Terminar(Resultado());
        await varredura;

        Assert.Equal(EstadoPainel.Parado, estadoAoGravar);
    }

    [Fact]
    public async Task Erro_chega_com_o_estado_ja_parado()
    {
        var simulador = new Simulador();
        var painel = Painel(simulador);
        EstadoPainel? estadoNoErro = null;
        string? mensagem = null;
        painel.Falhou += m =>
        {
            estadoNoErro = painel.Estado;
            mensagem = m;
        };

        var varredura = painel.VarrerAsync();
        simulador.Falhar(new InvalidOperationException("placa desligada"));
        await varredura;

        Assert.Equal(EstadoPainel.Parado, estadoNoErro);
        Assert.Contains("placa desligada", mensagem);
        Assert.True(painel.PodeAcionarPrincipal);
        Assert.Contains(painel.Console.Linhas, l => l.Contains("A varredura falhou: placa desligada"));
    }

    [Fact]
    public async Task Cancelar_passa_por_cancelando_e_volta_a_parado()
    {
        var simulador = new Simulador();
        var painel = Painel(simulador);

        var varredura = painel.VarrerAsync();
        painel.ComandoPrincipal.Execute(null);

        Assert.Equal(EstadoPainel.Cancelando, painel.Estado);
        Assert.Equal("Cancelando...", painel.TextoBotaoPrincipal);
        Assert.False(painel.PodeAcionarPrincipal);
        Assert.True(simulador.Cancelamento.IsCancellationRequested);

        var resultado = Resultado();
        resultado.Cancelada = true;
        simulador.Terminar(resultado);
        await varredura;

        Assert.Equal(EstadoPainel.Parado, painel.Estado);
        Assert.Equal("Varredura interrompida.", painel.TextoAndamento);
    }

    [Fact]
    public async Task Aviso_da_sub_rede_grande_vai_ao_console_uma_vez_e_a_barra_de_estado()
    {
        var simulador = new Simulador();
        var interfaceGrande = Interface("192.0.2.123", 21);
        var painel = new PainelVarredura(Dependencias(simulador, interfaces: [interfaceGrande]));
        painel.CarregarInterfaces();

        var varredura = painel.VarrerAsync();
        var resultado = Resultado(interfaceGrande);
        Varredor.SubRedeAVarrer(interfaceGrande, 22, out var aviso);
        resultado.Avisos.Add(aviso!);
        simulador.Terminar(resultado);
        await varredura;

        Assert.Single(painel.Console.Linhas, l => l.Contains(aviso!));
        Assert.Equal("Só o bloco 192.0.0.0/22 foi varrido. Detalhes no console.", painel.TextoAviso);
    }

    [Fact]
    public async Task Hosts_entram_na_ordem_numerica_do_ip()
    {
        var simulador = new Simulador();
        var painel = Painel(simulador);

        var varredura = painel.VarrerAsync();
        simulador.Relatar(Host("192.0.2.10"));
        simulador.Relatar(Host("192.0.2.9"));
        simulador.Relatar(Host("192.0.2.100"));
        simulador.Relatar(Host("192.0.2.9", "impressora.local"));

        Assert.Equal(["192.0.2.9", "192.0.2.10", "192.0.2.100"], painel.Hosts.Select(h => h.Ip));
        Assert.Equal("impressora.local", painel.Hosts[0].Nome);
        Assert.Equal("Hosts da rede: 3", painel.TituloHosts);

        simulador.Terminar(Resultado());
        await varredura;
    }

    [Theory]
    [InlineData("192.0.2.1", true)]
    [InlineData("roteador", true)]
    [InlineData("00:00:0c", true)]
    [InlineData("00000C010203", true)]
    [InlineData("00-00-0C", true)]
    [InlineData("cisco", true)]
    [InlineData("gateway", true)]
    [InlineData("brother", false)]
    [InlineData("", true)]
    [InlineData("-", false)]
    public void Filtro_procura_ip_nome_mac_fabricante_e_observacao(string texto, bool esperado)
    {
        var painel = Painel(new Simulador());
        var host = Host("192.0.2.1", "roteador");
        host.Mac = FabricantesTestes.Mac("00:00:0C:01:02:03");
        host.Fabricante = "Cisco Systems, Inc";
        host.EhGateway = true;
        var linha = new LinhaHost(host);

        painel.Filtro = texto;

        Assert.Equal(esperado, painel.Aceita(linha));
    }

    [Fact]
    public void Ping_sem_resposta_vai_para_o_fim_da_ordem()
    {
        var comPing = new LinhaHost(new HostEncontrado { Ip = IPAddress.Parse("192.0.2.1"), TempoPingMs = 3 });
        var semPing = new LinhaHost(new HostEncontrado { Ip = IPAddress.Parse("192.0.2.2") });

        Assert.True(comPing.PingOrdem < semPing.PingOrdem);
        Assert.Equal("-", semPing.Ping);
    }

    [Fact]
    public void Minha_maquina_mostra_a_placa_escolhida()
    {
        var painel = Painel(new Simulador());

        var itens = painel.MinhaMaquina.ToDictionary(i => i.Rotulo, i => i.Valor);

        Assert.Equal("192.0.2.10/24", itens["IPv4"]);
        Assert.Equal("255.255.255.0", itens["Máscara"]);
        Assert.Equal("192.0.2.1", itens["Gateway"]);
        Assert.Equal("192.0.2.0/24 (254 endereços)", itens["Sub-rede a varrer"]);
    }

    [Fact]
    public async Task Textos_da_tela_nao_tem_caractere_proibido()
    {
        var simulador = new Simulador();
        var painel = Painel(simulador);
        var varredura = painel.VarrerAsync();
        painel.Cancelar();
        var resultado = Resultado();
        resultado.Cancelada = true;
        simulador.Terminar(resultado);
        await varredura;

        var textos = painel.Console.Linhas
            .Concat(painel.MinhaMaquina.Select(i => i.Rotulo + i.Valor))
            .Append(painel.TextoAndamento)
            .Append(painel.TextoResumo)
            .Append(painel.TextoRelatorio)
            .Append(PainelVarredura.Titulo);

        Assert.Empty(textos.SelectMany(CaracteresProibidosTestes.Proibidos));
    }

    [Fact]
    public void Console_guarda_no_maximo_o_limite_de_linhas()
    {
        var registro = new RegistroConsole(() => new DateTimeOffset(2026, 9, 25, 14, 2, 11, TimeSpan.Zero));

        for (var i = 0; i < RegistroConsole.LimiteLinhas + 5; i++)
        {
            registro.Escrever($"linha {i}");
        }

        Assert.Equal(RegistroConsole.LimiteLinhas, registro.Linhas.Count);
        Assert.Equal("[14:02:11] linha 5", registro.Linhas[0]);
    }

    private static PainelVarredura Painel(Simulador simulador)
    {
        var painel = new PainelVarredura(Dependencias(simulador));
        painel.CarregarInterfaces();
        return painel;
    }

    private static DependenciasPainel Dependencias(
        Simulador simulador,
        IReadOnlyList<InterfaceRede>? interfaces = null,
        Func<ResultadoVarredura, string, Task<string>>? salvar = null) => new()
        {
            ListarInterfaces = () => interfaces ?? [Interface("192.0.2.10", 24)],
            Varrer = simulador.Varrer,
            SalvarEm = salvar ?? ((_, caminho) => Task.FromResult(caminho)),
            CriarProgresso = acao => new ProgressoImediato(acao),
            PastaRelatorios = Path.Combine(Path.GetTempPath(), "mapnet-testes-painel"),
        };

    private static InterfaceRede Interface(string ip, int prefixo) => new()
    {
        Id = "teste",
        Nome = "Ethernet",
        Descricao = "Placa de teste",
        Tipo = TipoInterface.Cabo,
        Ip = IPAddress.Parse(ip),
        Prefixo = prefixo,
        Gateway = IPAddress.Parse("192.0.2.1"),
        Dns = [IPAddress.Parse("192.0.2.1")],
    };

    private static HostEncontrado Host(string ip, string? nome = null) => new()
    {
        Ip = IPAddress.Parse(ip),
        RespondeuPing = true,
        TempoPingMs = 2,
        NomeMdns = nome,
    };

    private static ResultadoVarredura Resultado(InterfaceRede? interfaceRede = null)
    {
        var i = interfaceRede ?? Interface("192.0.2.10", 24);
        var inicio = new DateTimeOffset(2026, 9, 25, 14, 30, 0, TimeSpan.Zero);
        var resultado = new ResultadoVarredura
        {
            Interface = i,
            SubRedeVarrida = Varredor.SubRedeAVarrer(i, 22, out _),
            Inicio = inicio,
            Fim = inicio.AddSeconds(79),
        };
        resultado.Hosts.Add(Host("192.0.2.1", "roteador"));
        resultado.Hosts.Add(Host("192.0.2.20"));
        return resultado;
    }

    /// <summary>Varredura simulada: o teste decide quando ela relata, termina ou falha.</summary>
    private sealed class Simulador
    {
        private readonly TaskCompletionSource<ResultadoVarredura> _fim = new();
        private IProgress<ProgressoVarredura>? _progresso;

        public CancellationToken Cancelamento { get; private set; }

        public Task<ResultadoVarredura> Varrer(InterfaceRede i, IProgress<ProgressoVarredura> progresso, CancellationToken cancelamento)
        {
            _progresso = progresso;
            Cancelamento = cancelamento;
            return _fim.Task;
        }

        public void Relatar(HostEncontrado host) =>
            _progresso!.Report(new ProgressoVarredura(Varredor.EtapaDescoberta, 1, 254, host));

        public void Terminar(ResultadoVarredura resultado) => _fim.SetResult(resultado);

        public void Falhar(Exception erro) => _fim.SetException(erro);
    }

    /// <summary>Entrega o andamento na hora, sem fila, como a janela faz na linha dela.</summary>
    private sealed class ProgressoImediato(Action<ProgressoVarredura> acao) : IProgress<ProgressoVarredura>
    {
        public void Report(ProgressoVarredura value) => acao(value);
    }
}

using MapNet.Nucleo;

namespace MapNet.Testes;

/// <summary>
/// Fatia 5: manutenção pelos comandos oficiais do Windows. A elevação só roda comandos de uma
/// lista fechada, e nenhum texto vindo da tela vira comando. Nenhum teste roda comando de verdade.
/// </summary>
public class ManutencaoTestes
{
    [Theory]
    [InlineData(AcaoManutencao.LimparCacheDns, "ipconfig flushdns", false)]
    [InlineData(AcaoManutencao.RenovarIp, "ipconfig release e ipconfig renew", true)]
    [InlineData(AcaoManutencao.LimparArp, "netsh interface ip delete arpcache", true)]
    [InlineData(AcaoManutencao.ResetarRede, "netsh winsock reset e netsh int ip reset", true)]
    public void Cada_acao_tem_comandos_fixos(AcaoManutencao acao, string esperado, bool administrador)
    {
        Assert.Equal(esperado, Manutencao.TextoComandos(acao).Replace("/", string.Empty));
        Assert.Equal(administrador, Manutencao.PedeAdministrador(acao));
        Assert.All(Manutencao.Comandos(acao), c => Assert.Contains(c.Programa, new[] { "ipconfig.exe", "netsh.exe" }));
    }

    [Fact]
    public void Lista_de_acoes_e_fechada()
    {
        Assert.Equal(4, Enum.GetValues<AcaoManutencao>().Length);
    }

    [Theory]
    [InlineData(AcaoManutencao.RenovarIp, true)]
    [InlineData(AcaoManutencao.ResetarRede, true)]
    [InlineData(AcaoManutencao.LimparCacheDns, false)]
    [InlineData(AcaoManutencao.LimparArp, false)]
    public void So_as_acoes_que_derrubam_a_rede_pedem_confirmacao(AcaoManutencao acao, bool pede)
    {
        Assert.Equal(pede, Manutencao.Confirmacao(acao) != null);
    }

    [Fact]
    public void Auxiliar_aceita_acao_da_lista_e_arquivo_no_formato()
    {
        var arquivo = Auxiliar.NovoArquivo();

        Assert.True(Auxiliar.Interpretar(["--auxiliar", "LimparArp", arquivo], out var acao, out var lido));
        Assert.Equal(AcaoManutencao.LimparArp, acao);
        Assert.Equal(arquivo, lido);
    }

    [Theory]
    [InlineData("--auxiliar", "Formatar", true)]
    [InlineData("--auxiliar", "1", true)]
    [InlineData("--auxiliar", "limparArp", true)]
    [InlineData("--auxiliar", "LimparArp; del", true)]
    [InlineData("--outro", "LimparArp", true)]
    [InlineData("--auxiliar", "LimparArp", false)]
    public void Auxiliar_recusa_acao_fora_da_lista(string primeiro, string acao, bool arquivoBom)
    {
        var arquivo = arquivoBom ? Auxiliar.NovoArquivo() : Path.Combine(Path.GetTempPath(), "saida.txt");

        Assert.False(Auxiliar.Interpretar([primeiro, acao, arquivo], out _, out _));
    }

    [Fact]
    public void Auxiliar_recusa_caminho_com_volta_de_pasta_ou_relativo()
    {
        var nome = Path.GetFileName(Auxiliar.NovoArquivo());

        Assert.False(Auxiliar.Interpretar(["--auxiliar", "LimparArp", Path.Combine(Path.GetTempPath(), "..", nome)], out _, out _));
        Assert.False(Auxiliar.Interpretar(["--auxiliar", "LimparArp", nome], out _, out _));
        Assert.False(Auxiliar.Interpretar(["--auxiliar", "LimparArp"], out _, out _));
    }

    [Fact]
    public void Auxiliar_com_argumento_errado_nao_roda_nada()
    {
        Assert.Equal(2, Auxiliar.Executar(["--auxiliar", "Formatar", "C:\\x.txt"]));
    }

    [Fact]
    public async Task Acao_sem_confirmacao_do_tecnico_nao_roda()
    {
        var executor = new ExecutorFixo(new ResultadoManutencao(false, 0, "ok"));
        var painel = Painel(executor, confirmar: false);

        await painel.ManutencaoAsync(AcaoManutencao.ResetarRede);

        Assert.Equal(0, executor.Chamadas);
        Assert.Equal("Manutenção", painel.AbaSelecionada.Titulo);
        Assert.EndsWith("Resetar Winsock e TCP/IP: cancelado antes de rodar.", painel.AbaSelecionada.Registro.Linhas[^1]);
    }

    [Fact]
    public async Task Acao_confirmada_roda_e_mostra_a_saida()
    {
        var executor = new ExecutorFixo(new ResultadoManutencao(false, 0, "> netsh winsock reset\r\nRedefinido.\r\n"));
        var painel = Painel(executor, confirmar: true);

        await painel.ManutencaoAsync(AcaoManutencao.ResetarRede);

        var linhas = painel.AbaSelecionada.Registro.Linhas;
        Assert.Equal(1, executor.Chamadas);
        Assert.Contains(linhas, l => l.Contains("O Windows vai pedir permissão de administrador."));
        Assert.Contains("Redefinido.", linhas);
        Assert.EndsWith("concluído. Reinicie o computador para valer.", linhas[^1]);
    }

    [Fact]
    public async Task Uac_negado_vira_aviso()
    {
        var painel = Painel(new ExecutorFixo(new ResultadoManutencao(true, 0, "")), confirmar: true);

        await painel.ManutencaoAsync(AcaoManutencao.LimparArp);

        Assert.EndsWith("a permissão de administrador foi negada, nada foi feito.", painel.AbaSelecionada.Registro.Linhas[^1]);
    }

    [Fact]
    public async Task Codigo_de_erro_do_windows_aparece()
    {
        var painel = Painel(new ExecutorFixo(new ResultadoManutencao(false, 1, "A operação requer elevação.")), confirmar: true);

        await painel.ManutencaoAsync(AcaoManutencao.LimparCacheDns);

        Assert.Contains("A operação requer elevação.", painel.AbaSelecionada.Registro.Linhas);
        Assert.EndsWith("o Windows devolveu o código 1. Veja a saída acima.", painel.AbaSelecionada.Registro.Linhas[^1]);
    }

    [Fact]
    public async Task Uma_acao_por_vez()
    {
        var liberar = new TaskCompletionSource<ResultadoManutencao>();
        var executor = new ExecutorFixo(null, liberar.Task);
        var painel = Painel(executor, confirmar: true);

        var primeira = painel.ManutencaoAsync(AcaoManutencao.LimparArp);
        Assert.False(painel.ComandoLimparCacheDns.CanExecute(null));
        await painel.ManutencaoAsync(AcaoManutencao.LimparCacheDns);
        liberar.SetResult(new ResultadoManutencao(false, 0, "Ok."));
        await primeira;

        Assert.Equal(1, executor.Chamadas);
        Assert.True(painel.ComandoLimparCacheDns.CanExecute(null));
    }

    [Fact]
    public void Sem_executor_nao_ha_aba_manutencao()
    {
        var painel = new PainelVarredura(new DependenciasPainel
        {
            ListarInterfaces = () => [],
            Varrer = (_, _, _) => Task.FromResult<ResultadoVarredura>(null!),
            SalvarEm = (_, c) => Task.FromResult(c),
        });

        Assert.DoesNotContain(painel.Abas, a => a.EhManutencao);
        Assert.False(painel.ComandoRenovarIp.CanExecute(null));
    }

    [Fact]
    public void Textos_da_manutencao_nao_tem_caractere_proibido()
    {
        var textos = Enum.GetValues<AcaoManutencao>()
            .SelectMany(a => new[] { Manutencao.Titulo(a), Manutencao.TextoComandos(a), Manutencao.Confirmacao(a) ?? "" });

        Assert.Empty(textos.SelectMany(CaracteresProibidosTestes.Proibidos));
    }

    private static PainelVarredura Painel(IExecutorManutencao executor, bool confirmar) => new(new DependenciasPainel
    {
        ListarInterfaces = () => [MaquinaTestes.Interface(TipoInterface.Cabo)],
        Varrer = (_, _, _) => Task.FromResult<ResultadoVarredura>(null!),
        SalvarEm = (_, c) => Task.FromResult(c),
        Manutencao = executor,
        Confirmar = _ => confirmar,
    });

    private sealed class ExecutorFixo(ResultadoManutencao? resultado, Task<ResultadoManutencao>? tarefa = null) : IExecutorManutencao
    {
        public int Chamadas { get; private set; }

        public Task<ResultadoManutencao> ExecutarAsync(AcaoManutencao acao, CancellationToken cancelamento)
        {
            Chamadas++;
            return tarefa ?? Task.FromResult(resultado!);
        }
    }
}

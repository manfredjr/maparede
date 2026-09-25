using System.Net;
using MapaRedeMt.Nucleo;

namespace MapaRedeMt.Testes;

public class ArgumentosTestes
{
    [Fact]
    public void Sem_argumentos_abre_a_janela()
    {
        var a = ArgumentosCli.Interpretar([]);

        Assert.Equal(ComandoCli.Janela, a.Comando);
        Assert.True(a.Valido);
    }

    [Fact]
    public void Varrer_com_opcoes()
    {
        var a = ArgumentosCli.Interpretar(["--varrer", "--interface", "Wi-Fi", "--saida", @"C:\Relatorios", "--abrir", "--tempo-ping", "500", "--paralelo", "32", "--sem-arp"]);

        Assert.True(a.Valido);
        Assert.Equal(ComandoCli.Varrer, a.Comando);
        Assert.Equal("Wi-Fi", a.Interface);
        Assert.Equal(@"C:\Relatorios", a.Saida);
        Assert.True(a.Abrir);
        Assert.Equal(500, a.Opcoes.TempoPingMs);
        Assert.Equal(32, a.Opcoes.Paralelismo);
        Assert.False(a.Opcoes.UsarArp);
    }

    [Fact]
    public void Aceita_barra_no_estilo_do_windows()
    {
        Assert.Equal(ComandoCli.Ajuda, ArgumentosCli.Interpretar(["/?"]).Comando);
        Assert.Equal(ComandoCli.ListarInterfaces, ArgumentosCli.Interpretar(["/interfaces"]).Comando);
    }

    [Theory]
    [InlineData("--nada")]
    [InlineData("--varrer", "--tempo-ping", "50")]
    [InlineData("--varrer", "--paralelo", "abc")]
    [InlineData("--varrer", "--interface")]
    [InlineData("--varrer", "--interfaces")]
    [InlineData("--saida", "x")]
    public void Aponta_erro_de_uso(params string[] args)
    {
        Assert.False(ArgumentosCli.Interpretar(args).Valido);
    }

    [Fact]
    public void Escolhe_interface_por_numero_nome_ou_ip()
    {
        var lista = new[] { Interface("Ethernet", "Intel I219", "192.168.0.10"), Interface("Wi-Fi", "Intel AX201", "10.0.0.5") };

        Assert.Same(lista[0], ArgumentosCli.EscolherInterface(lista, null, out _));
        Assert.Same(lista[1], ArgumentosCli.EscolherInterface(lista, "2", out _));
        Assert.Same(lista[1], ArgumentosCli.EscolherInterface(lista, "wi-fi", out _));
        Assert.Same(lista[0], ArgumentosCli.EscolherInterface(lista, "192.168.0.10", out _));
        Assert.Same(lista[1], ArgumentosCli.EscolherInterface(lista, "AX201", out _));
        Assert.Null(ArgumentosCli.EscolherInterface(lista, "Intel", out var ambiguo));
        Assert.NotNull(ambiguo);
        Assert.Null(ArgumentosCli.EscolherInterface(lista, "3", out var foraDaFaixa));
        Assert.NotNull(foraDaFaixa);
    }

    private static InterfaceRede Interface(string nome, string descricao, string ip) =>
        new() { Id = nome, Nome = nome, Descricao = descricao, Ip = IPAddress.Parse(ip), Prefixo = 24 };
}

public class VarredorTestes
{
    [Fact]
    public void Rede_grande_e_reduzida_ao_bloco_do_computador()
    {
        var i = new InterfaceRede { Id = "x", Nome = "Ethernet", Ip = IPAddress.Parse("10.20.130.7"), Prefixo = 16 };

        var s = Varredor.SubRedeAVarrer(i, 22, out var aviso);

        Assert.Equal("10.20.128.0/22", s.ToString());
        Assert.NotNull(aviso);
    }

    [Fact]
    public void Rede_pequena_e_varrida_inteira()
    {
        var i = new InterfaceRede { Id = "x", Nome = "Ethernet", Ip = IPAddress.Parse("192.168.1.7"), Prefixo = 24 };

        var s = Varredor.SubRedeAVarrer(i, 22, out var aviso);

        Assert.Equal("192.168.1.0/24", s.ToString());
        Assert.Null(aviso);
    }

    [Fact]
    public async Task Varredura_usa_o_arp_e_marca_gateway_e_o_proprio_computador()
    {
        var i = new InterfaceRede
        {
            Id = "x",
            Nome = "Teste",
            Ip = IPAddress.Parse("198.18.0.2"),
            Prefixo = 29,
            Gateway = IPAddress.Parse("198.18.0.1"),
            Mac = FabricantesTestes.Mac("02:00:00:00:00:02"),
        };
        var arp = new ArpFalso(new() { ["198.18.0.1"] = FabricantesTestes.Mac("00:00:0C:00:00:01"), ["198.18.0.5"] = FabricantesTestes.Mac("B8:27:EB:00:00:05") });
        var opcoes = new OpcoesVarredura { TempoPingMs = 200, TempoNomeMs = 200 };

        var r = await new Varredor(opcoes, TabelaOui.Embutida, arp).VarrerAsync(i);

        Assert.Equal(new[] { "198.18.0.1", "198.18.0.2", "198.18.0.5" }, r.Hosts.Select(h => h.Ip.ToString()));
        Assert.True(r.Hosts[0].EhGateway);
        Assert.Equal("Cisco Systems, Inc", r.Hosts[0].Fabricante);
        Assert.True(r.Hosts[1].EhEsteComputador);
        Assert.Equal(TabelaOui.TextoMacAleatorio, r.Hosts[1].Fabricante);
        Assert.Equal("Raspberry Pi Foundation", r.Hosts[2].Fabricante);
        Assert.False(r.Cancelada);
    }

    private sealed class ArpFalso(Dictionary<string, byte[]> tabela) : ISondaArp
    {
        public byte[]? Resolver(IPAddress destino, IPAddress origem) => tabela.GetValueOrDefault(destino.ToString());
    }
}

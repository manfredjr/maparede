using System.Net;
using MapNet.Nucleo;

namespace MapNet.Testes;

/// <summary>
/// Classificação por heurística: cada tipo a partir dos sinais que as etapas levantam, o mínimo
/// para arriscar um palpite, os motivos à vista e onde o tipo aparece.
/// </summary>
public class ClassificacaoTestes
{
    [Fact]
    public void Gateway_e_roteador()
    {
        var h = Host(gateway: true, portas: [53, 80, 443]);

        var c = h.Classificacao;

        Assert.Equal(TipoEquipamento.Roteador, c.Tipo);
        Assert.Contains("é o gateway da rede", c.Motivos);
    }

    [Fact]
    public void Porta_de_impressao_e_fabricante_dao_impressora()
    {
        var h = Host(fabricante: "Brother industries, LTD.", portas: [80, 443, 515, 631, 9100]);

        var c = h.Classificacao;

        Assert.Equal(TipoEquipamento.Impressora, c.Tipo);
        Assert.Contains(c.Motivos, m => m.Contains("9100"));
        Assert.Contains(c.Motivos, m => m.Contains("Brother"));
        Assert.StartsWith("Impressora (", c.Texto);
    }

    [Fact]
    public void Porta_de_camera_da_camera_mesmo_sem_fabricante_conhecido()
    {
        var h = Host(portas: [80, 554, 37777]);

        Assert.Equal(TipoEquipamento.Camera, h.Classificacao.Tipo);
    }

    [Fact]
    public void Portas_do_windows_dao_computador_windows()
    {
        var h = Host(fabricante: "Intel Corporate", portas: [80, 135, 139, 445, 3389]);

        Assert.Equal(TipoEquipamento.ComputadorWindows, h.Classificacao.Tipo);
    }

    [Fact]
    public void Ttl_128_com_netbios_da_windows_sem_portas()
    {
        var h = Host(ttl: 128);
        h.NomeNetBios = "ESTACAO-01";

        Assert.Equal(TipoEquipamento.Desconhecido, h.Classificacao.Tipo);

        h.Fabricante = "Dell Inc.";
        h.PortasAbertas = [445];
        Assert.Equal(TipoEquipamento.ComputadorWindows, h.Classificacao.Tipo);
    }

    [Theory]
    [InlineData("Tuya Smart Inc.", TipoEquipamento.Automacao)]
    [InlineData("Philips Lighting BV", TipoEquipamento.Automacao)]
    [InlineData("Synology Incorporated", TipoEquipamento.Nas)]
    [InlineData("Sony Interactive Entertainment Inc.", TipoEquipamento.Console)]
    [InlineData("Ubiquiti Inc", TipoEquipamento.AccessPoint)]
    [InlineData("Hikvision Digital Technology", TipoEquipamento.Camera)]
    public void Fabricante_forte_basta(string fabricante, TipoEquipamento esperado)
    {
        Assert.Equal(esperado, Host(fabricante: fabricante).Classificacao.Tipo);
    }

    [Fact]
    public void Nome_do_aparelho_ajuda()
    {
        var ps5 = Host();
        ps5.NomeMdns = "PS5-09BD4A.local";
        var tv = Host();
        tv.Servicos = [new ServicoIdentificado { Porta = 0, Protocolo = "UPnP", Modelo = "[LG] webOS TV UR8750PSA (LG Electronics. LG TV)" }];

        Assert.Equal(TipoEquipamento.Console, ps5.Classificacao.Tipo);
        Assert.Equal(TipoEquipamento.TvMidia, tv.Classificacao.Tipo);
    }

    [Fact]
    public void Palavra_so_conta_inteira()
    {
        var h = Host();
        h.NomeDns = "tvbox-nascimento.example";

        Assert.Equal(TipoEquipamento.Desconhecido, h.Classificacao.Tipo);
    }

    [Fact]
    public void Mac_aleatorio_da_celular()
    {
        var h = Host(mac: [0x02, 0x1A, 0x2B, 0x3C, 0x4D, 0x31]);

        Assert.Equal(TipoEquipamento.Celular, h.Classificacao.Tipo);
        Assert.Contains("MAC aleatório", h.Classificacao.Motivos);
    }

    [Fact]
    public void Este_computador_e_windows_mesmo_com_mac_aleatorio()
    {
        var h = Host(mac: [0x02, 0x00, 0x00, 0x00, 0x00, 0x23]);
        h.EhEsteComputador = true;

        Assert.Equal(TipoEquipamento.ComputadorWindows, h.Classificacao.Tipo);
        Assert.DoesNotContain("MAC aleatório", h.Classificacao.Motivos);
    }

    [Fact]
    public void Sinal_fraco_sozinho_fica_desconhecido()
    {
        var h = Host(fabricante: "Canon Inc.");

        Assert.Equal(TipoEquipamento.Desconhecido, h.Classificacao.Tipo);
        Assert.Equal("não deu para saber", h.Classificacao.Texto);
    }

    [Fact]
    public void Sinal_mais_forte_ganha()
    {
        // Gateway com cara de impressora na página: o gateway pesa mais.
        var h = Host(gateway: true, portas: [80]);
        h.Servicos = [new ServicoIdentificado { Porta = 80, Protocolo = "HTTP", Titulo = "Printer" }];

        Assert.Equal(TipoEquipamento.Roteador, h.Classificacao.Tipo);
    }

    [Fact]
    public void Tipo_aparece_na_tabela_no_detalhe_e_no_relatorio()
    {
        var h = Host(fabricante: "Brother industries, LTD.", portas: [9100]);
        var linha = new LinhaHost(h);

        Assert.Equal("Impressora", linha.Tipo);
        Assert.StartsWith("Tipo provável: Impressora (", linha.TipoDica);
        Assert.True(linha.Contem("impressora"));
        Assert.StartsWith("Impressora (", DetalheHost.Itens(h).Single(i => i.Rotulo == "Tipo provável").Valor);

        var html = WebUtility.HtmlDecode(RelatorioHtml.Gerar(Resultado(h, Host(ip: "192.0.2.9"))));
        Assert.Contains("<th data-tipo=\"texto\">Tipo</th>", html);
        Assert.Contains("<h2>Tipos prováveis</h2>", html);
        Assert.Contains("<td>Impressora</td><td class=\"num\">1</td>", html);
        Assert.Contains("<td>Desconhecido</td><td class=\"num\">1</td>", html);
    }

    [Fact]
    public void Desconhecido_fica_vazio_na_tabela()
    {
        var linha = new LinhaHost(Host());

        Assert.Equal(string.Empty, linha.Tipo);
        Assert.Null(linha.TipoDica);
    }

    [Fact]
    public void Nome_traz_a_origem_na_dica()
    {
        var h = Host();
        h.NomeNetBios = "IMPRESSORA-RECEPCAO";

        Assert.Equal("IMPRESSORA-RECEPCAO (pelo NetBIOS)", new LinhaHost(h).NomeDica);
    }

    [Fact]
    public void Demonstracao_tem_varios_tipos()
    {
        var dependencias = Demonstracao.Dependencias();
        var i = dependencias.ListarInterfaces()[0];

        var tipos = Demonstracao.Hosts(i).Select(h => h.Classificacao.Tipo).Distinct().ToList();

        Assert.Contains(TipoEquipamento.Roteador, tipos);
        Assert.Contains(TipoEquipamento.Celular, tipos);
    }

    private static HostEncontrado Host(
        string ip = "192.0.2.5",
        string fabricante = "",
        int[]? portas = null,
        bool gateway = false,
        int? ttl = 64,
        byte[]? mac = null) => new()
        {
            Ip = IPAddress.Parse(ip),
            Mac = mac ?? [0x00, 0x11, 0x22, 0x33, 0x44, 0x55],
            Fabricante = fabricante,
            RespondeuPing = true,
            TempoPingMs = 1,
            Ttl = ttl,
            EhGateway = gateway,
            PortasAbertas = portas ?? [],
            PortasTestadas = ListaPortas.Padrao,
            PortasVerificadas = portas != null,
        };

    private static ResultadoVarredura Resultado(params HostEncontrado[] hosts)
    {
        var i = new InterfaceRede
        {
            Id = "teste",
            Nome = "Ethernet",
            Descricao = "Placa de teste",
            Tipo = TipoInterface.Cabo,
            Ip = IPAddress.Parse("192.0.2.10"),
            Prefixo = 24,
            Gateway = IPAddress.Parse("192.0.2.1"),
            Dns = [IPAddress.Parse("192.0.2.1")],
        };
        var inicio = new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);
        var r = new ResultadoVarredura { Interface = i, SubRedeVarrida = i.SubRede, Inicio = inicio, Fim = inicio.AddSeconds(30) };
        r.Hosts.AddRange(hosts);
        return r;
    }
}

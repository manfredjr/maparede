using System.Net;
using MapNet.Nucleo;

namespace MapNet.Testes;

public class RelatorioTestes
{
    [Fact]
    public void Relatorio_traz_resumo_e_hosts()
    {
        var html = RelatorioHtml.Gerar(Exemplo());

        Assert.Contains("Inventário da rede 192.168.0.0/24", html);
        Assert.Contains("192.168.0.1", html);
        Assert.Contains("Gateway", html);
        Assert.Contains("Raspberry Pi Foundation", html);
        Assert.Contains("id=\"filtro\"", html);
        Assert.Contains("class=\"detalhe\"", html);
    }

    [Fact]
    public void Nome_vindo_da_rede_e_codificado()
    {
        var resultado = Exemplo();
        resultado.Hosts[1].NomeMdns = "<script>alert(1)</script>\"";

        var html = RelatorioHtml.Gerar(resultado);

        Assert.DoesNotContain("<script>alert(1)", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;&quot;", html);
    }

    [Fact]
    public void Nome_do_arquivo_e_minusculo_e_sem_espaco()
    {
        var nome = RelatorioHtml.NomeArquivo(Exemplo());

        Assert.Equal("mapnet-20260925-1430-192-168-0-0-24.html", nome);
        Assert.Equal(nome.ToLowerInvariant(), nome);
    }

    [Fact]
    public void Texto_fixo_do_relatorio_nao_tem_caractere_proibido()
    {
        var html = RelatorioHtml.Gerar(Exemplo());

        Assert.Empty(CaracteresProibidosTestes.Proibidos(html));
    }

    [Fact]
    public void Secao_minha_maquina_vem_antes_dos_hosts()
    {
        var resultado = Exemplo();
        resultado.Maquina = LeitorMaquina.Ler(MaquinaTestes.Interface(TipoInterface.Cabo), MaquinaTestes.Fontes(), 22);

        var html = Texto(resultado);

        Assert.Contains("<h2>Minha máquina</h2>", html);
        Assert.Contains("<h3>Computador</h3>", html);
        Assert.Contains("<h3>DHCP</h3>", html);
        Assert.Contains("escritorio.example", html);
        Assert.True(html.IndexOf("Minha máquina", StringComparison.Ordinal) < html.IndexOf("<h2>Hosts</h2>", StringComparison.Ordinal));
    }

    [Fact]
    public void Sem_dados_da_maquina_a_secao_nao_aparece()
    {
        var html = RelatorioHtml.Gerar(Exemplo());

        Assert.DoesNotContain("<h2>Minha máquina</h2>", html);
    }

    [Fact]
    public void Ip_publico_so_aparece_quando_foi_consultado()
    {
        var resultado = Exemplo();
        resultado.Maquina = LeitorMaquina.Ler(MaquinaTestes.Interface(TipoInterface.Cabo), MaquinaTestes.Fontes(), 22);

        Assert.DoesNotContain("IP público", Texto(resultado));

        resultado.IpPublico = "203.0.113.7";
        var html = Texto(resultado);

        Assert.Contains("<h3>IP público</h3>", html);
        Assert.Contains("203.0.113.7", html);
    }

    [Fact]
    public void Nome_da_rede_wifi_e_codificado()
    {
        var resultado = Exemplo();
        var wifi = new FontesMaquina
        {
            Computador = () => null,
            Placa = _ => null,
            Wifi = new WifiFixo(new DadosWifi { Ssid = "<img src=x onerror=alert(1)>" }),
        };
        resultado.Maquina = LeitorMaquina.Ler(MaquinaTestes.Interface(TipoInterface.WiFi), wifi, 22);

        var html = RelatorioHtml.Gerar(resultado);

        Assert.DoesNotContain("<img src=x", html);
        Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", html);
    }

    /// <summary>HTML com as entidades decodificadas: o relatório grava "á" como "&amp;#225;".</summary>
    private static string Texto(ResultadoVarredura resultado) => WebUtility.HtmlDecode(RelatorioHtml.Gerar(resultado));

    private sealed class WifiFixo(DadosWifi dados) : IFonteWifi
    {
        public DadosWifi? Ler(string idInterface) => dados;
    }

    [Theory]
    [InlineData(12, "12 s")]
    [InlineData(83, "1 min 23 s")]
    public void Duracao_legivel(int segundos, string esperado)
    {
        Assert.Equal(esperado, RelatorioHtml.Duracao(TimeSpan.FromSeconds(segundos)));
    }

    internal static ResultadoVarredura Exemplo()
    {
        var interfaceRede = new InterfaceRede
        {
            Id = "teste",
            Nome = "Ethernet",
            Descricao = "Placa de teste",
            Tipo = TipoInterface.Cabo,
            Ip = IPAddress.Parse("192.168.0.10"),
            Prefixo = 24,
            Gateway = IPAddress.Parse("192.168.0.1"),
            Dns = [IPAddress.Parse("192.168.0.1")],
        };
        var inicio = new DateTimeOffset(2026, 9, 25, 14, 30, 0, TimeSpan.FromHours(-3));
        var resultado = new ResultadoVarredura
        {
            Interface = interfaceRede,
            SubRedeVarrida = interfaceRede.SubRede,
            Inicio = inicio,
            Fim = inicio.AddSeconds(42),
            NomeComputador = "NOTEBOOK-MT",
        };
        resultado.Hosts.Add(new HostEncontrado
        {
            Ip = IPAddress.Parse("192.168.0.1"),
            Mac = FabricantesTestes.Mac("00:00:0C:01:02:03"),
            Fabricante = "Cisco Systems, Inc",
            RespondeuPing = true,
            RespondeuArp = true,
            TempoPingMs = 1,
            Ttl = 255,
            EhGateway = true,
        });
        resultado.Hosts.Add(new HostEncontrado
        {
            Ip = IPAddress.Parse("192.168.0.20"),
            Mac = FabricantesTestes.Mac("B8:27:EB:00:00:01"),
            Fabricante = "Raspberry Pi Foundation",
            RespondeuArp = true,
            NomeMdns = "raspberrypi.local",
        });
        return resultado;
    }
}

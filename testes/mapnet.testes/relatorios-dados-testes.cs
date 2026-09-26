using System.Net;
using System.Text;
using System.Xml.Linq;
using MapNet.Nucleo;

namespace MapNet.Testes;

/// <summary>Relatório em XML e CSV: conteúdo, codificação do texto vindo da rede e escolha do formato.</summary>
public class RelatoriosDadosTestes
{
    [Fact]
    public void Xml_e_valido_e_traz_tudo_do_host()
    {
        var xml = RelatorioXml.Gerar(Resultado());
        var doc = XDocument.Parse(xml);
        var host = doc.Root!.Element("hosts")!.Elements("host").First();

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", xml);
        Assert.Equal("1", doc.Root.Attribute("formato")!.Value);
        Assert.Equal("192.0.2.0/24", doc.Root.Element("varredura")!.Attribute("subRede")!.Value);
        Assert.Equal("192.0.2.5", host.Attribute("ip")!.Value);
        Assert.Equal("Impressora", host.Element("tipo")!.Attribute("nome")!.Value);
        Assert.Contains(host.Element("portas")!.Elements("porta"), p => p.Attribute("numero")!.Value == "9100" && p.Attribute("servico")!.Value == "Impressão direta");
        Assert.Equal("Impressora <P-200> & \"cia\"", host.Element("servicos")!.Element("servico")!.Attribute("titulo")!.Value);
        Assert.Equal("IMPRESSORA", host.Element("nomes")!.Attribute("netbios")!.Value);
    }

    [Fact]
    public void Xml_tira_caractere_que_o_xml_recusa()
    {
        var r = Resultado();
        r.Hosts[0].NomeMdns = "nome\u0001com\u0008controle.local";

        var doc = XDocument.Parse(RelatorioXml.Gerar(r));

        Assert.Equal("nomecomcontrole.local", doc.Root!.Element("hosts")!.Element("host")!.Element("nomes")!.Attribute("mdns")!.Value);
    }

    [Fact]
    public void Csv_tem_cabecalho_e_uma_linha_por_host()
    {
        var linhas = RelatorioCsv.Gerar(Resultado()).TrimEnd().Split(Environment.NewLine);

        Assert.Equal(3, linhas.Length);
        Assert.StartsWith("IP;Nome;Origem do nome;MAC;Fabricante;Tipo provável", linhas[0]);
        Assert.StartsWith("192.0.2.5;IMPRESSORA;NetBIOS;00:80:77:12:34:05;", linhas[1]);
        Assert.Contains(";Impressora;", linhas[1]);
    }

    [Theory]
    [InlineData("simples", "simples")]
    [InlineData("a;b", "\"a;b\"")]
    [InlineData("diz \"oi\"", "\"diz \"\"oi\"\"\"")]
    [InlineData("=HYPERLINK(\"x\")", "\"'=HYPERLINK(\"\"x\"\")\"")]
    [InlineData("+1", "'+1")]
    [InlineData("-cmd", "'-cmd")]
    [InlineData("@soma", "'@soma")]
    [InlineData("duas\nlinhas", "duas linhas")]
    [InlineData(null, "")]
    public void Campo_do_csv_e_protegido(string? valor, string esperado)
    {
        Assert.Equal(esperado, RelatorioCsv.Campo(valor));
    }

    [Theory]
    [InlineData("r.html", FormatoRelatorio.Html)]
    [InlineData("r.HTM", FormatoRelatorio.Html)]
    [InlineData("r.csv", FormatoRelatorio.Csv)]
    [InlineData("r.XML", FormatoRelatorio.Xml)]
    [InlineData("r", FormatoRelatorio.Html)]
    public void Formato_pela_extensao(string caminho, FormatoRelatorio esperado)
    {
        Assert.Equal(esperado, Relatorios.FormatoDe(caminho));
    }

    [Fact]
    public async Task Salvar_grava_cada_formato_com_a_codificacao_certa()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "mapnet-testes-formatos-" + Guid.NewGuid().ToString("N"));
        try
        {
            var r = Resultado();
            var csv = await Relatorios.SalvarAsync(r, Path.Combine(pasta, "a.csv"));
            var xml = await Relatorios.SalvarAsync(r, Path.Combine(pasta, "a.xml"));
            var html = await Relatorios.SalvarAsync(r, Path.Combine(pasta, "a.html"));

            var bytesCsv = await File.ReadAllBytesAsync(csv);
            Assert.Equal(Encoding.UTF8.GetPreamble(), bytesCsv[..3]);
            Assert.NotEqual(0xEF, (await File.ReadAllBytesAsync(xml))[0]);
            XDocument.Load(xml);
            Assert.StartsWith("<!DOCTYPE html>", await File.ReadAllTextAsync(html));
        }
        finally
        {
            if (Directory.Exists(pasta))
            {
                Directory.Delete(pasta, recursive: true);
            }
        }
    }

    [Fact]
    public void Nome_sugerido_por_formato()
    {
        var r = Resultado();

        Assert.EndsWith(".csv", Relatorios.NomeArquivo(r, FormatoRelatorio.Csv));
        Assert.Equal(Path.GetFileNameWithoutExtension(RelatorioHtml.NomeArquivo(r)), Path.GetFileNameWithoutExtension(Relatorios.NomeArquivo(r, FormatoRelatorio.Xml)));
    }

    [Fact]
    public void Linha_de_comando_grava_html_por_padrao()
    {
        var a = ArgumentosCli.Interpretar(["--varrer"]);

        var caminhos = a.Caminhos(Resultado(), @"C:\atual");

        Assert.Single(caminhos);
        Assert.StartsWith(@"C:\atual", caminhos[0]);
        Assert.EndsWith(".html", caminhos[0]);
    }

    [Fact]
    public void Linha_de_comando_todos_os_formatos_numa_pasta()
    {
        var a = ArgumentosCli.Interpretar(["--varrer", "--formato", "todos", "--saida", "relatorios"]);

        var caminhos = a.Caminhos(Resultado(), "atual");

        Assert.True(a.Valido);
        Assert.Equal([".html", ".csv", ".xml"], caminhos.Select(Path.GetExtension));
        Assert.All(caminhos, c => Assert.StartsWith("relatorios", c));
    }

    [Fact]
    public void Linha_de_comando_arquivo_define_o_formato()
    {
        var a = ArgumentosCli.Interpretar(["--varrer", "--saida", "inventario.csv"]);

        Assert.Equal([FormatoRelatorio.Csv], a.Formatos);
        Assert.Equal(["inventario.csv"], a.Caminhos(Resultado(), "atual"));
    }

    [Theory]
    [InlineData(new[] { "--varrer", "--formato", "pdf" }, "Formato desconhecido")]
    [InlineData(new[] { "--varrer", "--formato", "todos", "--saida", "a.html" }, "tem que ser uma pasta")]
    [InlineData(new[] { "--formato", "csv" }, "pedem o comando --varrer")]
    public void Linha_de_comando_recusa_formato_errado(string[] args, string trecho)
    {
        var a = ArgumentosCli.Interpretar(args);

        Assert.False(a.Valido);
        Assert.Contains(a.Erros, e => e.Contains(trecho));
    }

    private static ResultadoVarredura Resultado()
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
        var r = new ResultadoVarredura { Interface = i, SubRedeVarrida = i.SubRede, Inicio = inicio, Fim = inicio.AddSeconds(30), PortasVerificadas = ListaPortas.Padrao, IdentificacaoFeita = true };
        r.Hosts.Add(new HostEncontrado
        {
            Ip = IPAddress.Parse("192.0.2.5"),
            Mac = [0x00, 0x80, 0x77, 0x12, 0x34, 0x05],
            Fabricante = "Brother industries, LTD.",
            RespondeuPing = true,
            RespondeuArp = true,
            TempoPingMs = 3,
            Ttl = 64,
            NomeNetBios = "IMPRESSORA",
            PortasAbertas = [80, 9100],
            PortasTestadas = ListaPortas.Padrao,
            PortasVerificadas = true,
            Servicos = [new ServicoIdentificado { Porta = 80, Protocolo = "HTTP", CodigoHttp = 200, Titulo = "Impressora <P-200> & \"cia\"" }],
            ServicosIdentificados = true,
        });
        r.Hosts.Add(new HostEncontrado { Ip = IPAddress.Parse("192.0.2.10"), EhEsteComputador = true });
        return r;
    }
}

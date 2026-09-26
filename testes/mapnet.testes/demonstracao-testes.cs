using System.Net;
using System.Text.RegularExpressions;
using MapNet.Nucleo;

namespace MapNet.Testes;

/// <summary>
/// Modo de demonstração: as imagens do repositório saem dele, e o repositório é público. Nada
/// ali pode parecer rede de verdade.
/// </summary>
public partial class DemonstracaoTestes
{
    private static readonly SubRede[] _documentacao =
    [
        SubRede.Calcular(IPAddress.Parse("192.0.2.0"), 24),
        SubRede.Calcular(IPAddress.Parse("198.51.100.0"), 24),
        SubRede.Calcular(IPAddress.Parse("203.0.113.0"), 24),
    ];

    [Fact]
    public void Interfaces_e_hosts_usam_so_enderecos_de_documentacao()
    {
        var dependencias = Demonstracao.Dependencias();
        var interfaces = dependencias.ListarInterfaces();

        foreach (var i in interfaces)
        {
            Assert.True(DeDocumentacao(i.Ip), $"{i.Ip} não é endereço de documentação");
            Assert.True(i.Gateway is null || DeDocumentacao(i.Gateway));
            Assert.All(i.Dns, d => Assert.True(DeDocumentacao(d)));
            Assert.All(Demonstracao.Hosts(i), h => Assert.True(DeDocumentacao(h.Ip), $"{h.Ip} não é endereço de documentação"));
        }
    }

    [Fact]
    public async Task Com_portas_ligadas_a_demonstracao_preenche_portas_e_deixa_o_celular_de_fora()
    {
        var dependencias = Demonstracao.Dependencias();
        dependencias.Opcoes.OlharPortas = true;
        var i = dependencias.ListarInterfaces()[0];

        var r = await dependencias.Varrer(i, new Progress<ProgressoVarredura>(), CancellationToken.None);

        Assert.NotNull(r.PortasVerificadas);
        Assert.Contains(r.Hosts, h => h.PortasAbertas.Contains(9100));
        var celular = r.Hosts.Single(h => h.NomeMdns == "celular-exemplo.local");
        Assert.False(celular.PortasVerificadas);
        Assert.Equal(EtapaPortas.MotivoMacAleatorio, celular.MotivoSemPortas);
    }

    [Fact]
    public void Ipv6_de_exemplo_e_de_documentacao_ou_link_local()
    {
        var i = Demonstracao.Dependencias().ListarInterfaces()[0];
        var placa = Demonstracao.Fontes().Placa(i)!;

        Assert.All(placa.Ipv6, a => Assert.True(a.IsIPv6LinkLocal || a.ToString().StartsWith("2001:db8:", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Ip_publico_de_exemplo_nao_sai_para_a_internet()
    {
        var consulta = Demonstracao.Dependencias().IpPublico!;

        Assert.IsNotType<IpPublicoCloudflare>(consulta);
        Assert.True(DeDocumentacao(await consulta.ConsultarAsync(CancellationToken.None)));
    }

    [Fact]
    public void Textos_de_exemplo_nao_tem_caractere_proibido()
    {
        var dependencias = Demonstracao.Dependencias();
        var i = dependencias.ListarInterfaces()[0];
        var maquina = LeitorMaquina.Ler(i, Demonstracao.Fontes(), 22);
        var textos = Demonstracao.Hosts(i).SelectMany(h => new[] { h.Nome, h.Fabricante, string.Join(", ", h.Marcas) })
            .Concat(maquina.Grupos(DateTimeOffset.Now, "203.0.113.45").SelectMany(g => g.Itens.Select(x => x.Rotulo + x.Valor)))
            .Append(dependencias.IpPublico!.Endereco);

        Assert.Empty(textos.SelectMany(CaracteresProibidosTestes.Proibidos));
    }

    /// <summary>Toda ligação da janela aponta para uma propriedade que existe num dos modelos.</summary>
    [Fact]
    public void Ligacoes_da_janela_existem_nos_modelos()
    {
        var janela = File.ReadAllText(Path.Combine(CaracteresProibidosTestes.RaizDoRepositorio(), "src", "mapnet", "janela-principal.xaml"));
        var propriedades = new[] { typeof(PainelVarredura), typeof(GrupoMinhaMaquina), typeof(ItemMinhaMaquina), typeof(LinhaHost), typeof(AbaConsole) }
            .SelectMany(t => t.GetProperties())
            .Select(p => p.Name)
            .ToHashSet();

        var ligacoes = Ligacao().Matches(janela).Select(m => m.Groups[1].Value).Distinct().ToList();

        Assert.Contains("GruposMaquina", ligacoes);
        Assert.Contains("ComandoIpPublico", ligacoes);
        Assert.All(ligacoes, l => Assert.Contains(l, propriedades));
    }

    private static bool DeDocumentacao(IPAddress ip) => _documentacao.Any(s => s.Contem(ip));

    [GeneratedRegex(@"\{Binding\s+(?:Path=)?(\w+)")]
    private static partial Regex Ligacao();
}

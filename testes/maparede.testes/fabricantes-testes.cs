using MapaRede.Nucleo;

namespace MapaRede.Testes;

public class FabricantesTestes
{
    [Fact]
    public void Tabela_embutida_carrega_os_prefixos_do_ieee()
    {
        Assert.True(TabelaOui.Embutida.Quantidade > 30000);
    }

    [Theory]
    [InlineData("00:00:0C:12:34:56", "Cisco Systems, Inc")]
    [InlineData("B8:27:EB:00:00:01", "Raspberry Pi Foundation")]
    [InlineData("00:0C:29:AA:BB:CC", "VMware, Inc.")]
    public void Encontra_o_fabricante_pelo_mac(string mac, string fabricante)
    {
        Assert.Equal(fabricante, TabelaOui.Embutida.Buscar(Mac(mac)));
    }

    [Fact]
    public void Mac_aleatorio_de_celular_nao_tem_fabricante()
    {
        var mac = Mac("DA:A1:19:00:00:01");

        Assert.True(EnderecoMac.AdministradoLocalmente(mac));
        Assert.Equal(TabelaOui.TextoMacAleatorio, TabelaOui.Embutida.DescreverFabricante(mac));
    }

    [Fact]
    public void Le_o_formato_original_do_ieee_e_o_compacto()
    {
        var texto = """
            OUI/MA-L                                                    Organization
            00-00-0C   (hex)		Cisco Systems, Inc
            00000C     (base 16)		Cisco Systems, Inc
            				170 WEST TASMAN DRIVE
            AABBCC	Fabricante Compacto
            # comentario
            """;

        var tabela = TabelaOui.Ler(new StringReader(texto));

        Assert.Equal(2, tabela.Quantidade);
        Assert.Equal("Cisco Systems, Inc", tabela.Buscar(Mac("00:00:0C:00:00:00")));
        Assert.Equal("Fabricante Compacto", tabela.Buscar(Mac("AA:BB:CC:00:00:00")));
    }

    [Fact]
    public void Formata_e_valida_mac()
    {
        Assert.Equal("00:1B:63:0A:FF:10", EnderecoMac.Formatar(Mac("00:1B:63:0A:FF:10")));
        Assert.False(EnderecoMac.Valido(new byte[6]));
        Assert.False(EnderecoMac.Valido(Enumerable.Repeat((byte)0xFF, 6).ToArray()));
        Assert.False(EnderecoMac.Valido(new byte[] { 1, 2, 3 }));
    }

    internal static byte[] Mac(string texto) => texto.Split(':').Select(p => Convert.ToByte(p, 16)).ToArray();
}

using System.Net;
using MapaRede.Nucleo;

namespace MapaRede.Testes;

public class SubRedeTestes
{
    [Fact]
    public void Calcula_a_rede_classica_de_escritorio()
    {
        var s = SubRede.Calcular(IPAddress.Parse("192.168.0.37"), IPAddress.Parse("255.255.255.0"));

        Assert.Equal("192.168.0.0/24", s.ToString());
        Assert.Equal(IPAddress.Parse("192.168.0.1"), s.PrimeiroHost);
        Assert.Equal(IPAddress.Parse("192.168.0.254"), s.UltimoHost);
        Assert.Equal(IPAddress.Parse("192.168.0.255"), s.Broadcast);
        Assert.Equal(254, s.QuantidadeHosts);
        Assert.Equal(254, s.Hosts().Count());
    }

    [Theory]
    [InlineData("255.255.255.0", 24)]
    [InlineData("255.255.252.0", 22)]
    [InlineData("255.255.0.0", 16)]
    [InlineData("255.255.255.252", 30)]
    [InlineData("0.0.0.0", 0)]
    [InlineData("255.255.255.255", 32)]
    public void Converte_mascara_em_prefixo(string mascara, int prefixo)
    {
        Assert.Equal(prefixo, SubRede.PrefixoDaMascara(IPAddress.Parse(mascara)));
    }

    [Fact]
    public void Recusa_mascara_com_bits_fora_de_sequencia()
    {
        Assert.Throws<ArgumentException>(() => SubRede.PrefixoDaMascara(IPAddress.Parse("255.0.255.0")));
    }

    [Fact]
    public void Barra_22_tem_1022_hosts()
    {
        var s = SubRede.Calcular(IPAddress.Parse("10.1.5.20"), 22);

        Assert.Equal("10.1.4.0/22", s.ToString());
        Assert.Equal(1022, s.QuantidadeHosts);
        Assert.Equal(IPAddress.Parse("10.1.7.254"), s.UltimoHost);
    }

    [Fact]
    public void Barra_31_usa_os_dois_enderecos()
    {
        var s = SubRede.Calcular(IPAddress.Parse("10.0.0.1"), 31);

        Assert.Equal(new[] { "10.0.0.0", "10.0.0.1" }, s.Hosts().Select(h => h.ToString()));
    }

    [Fact]
    public void Sabe_se_um_ip_pertence_a_sub_rede()
    {
        var s = SubRede.Calcular(IPAddress.Parse("172.16.10.5"), 24);

        Assert.True(s.Contem(IPAddress.Parse("172.16.10.200")));
        Assert.False(s.Contem(IPAddress.Parse("172.16.11.1")));
    }

    [Fact]
    public void Numero_e_endereco_vao_e_voltam()
    {
        var ip = IPAddress.Parse("200.1.2.3");

        Assert.Equal(0xC8010203u, SubRede.ParaNumero(ip));
        Assert.Equal(ip, SubRede.ParaEndereco(0xC8010203u));
    }
}

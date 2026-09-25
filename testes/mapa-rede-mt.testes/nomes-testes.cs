using System.Buffers.Binary;
using System.Net;
using System.Text;
using MapaRedeMt.Nucleo;

namespace MapaRedeMt.Testes;

public class NetBiosTestes
{
    [Fact]
    public void Consulta_de_status_segue_a_rfc_1002()
    {
        var pacote = NetBios.MontarConsultaStatus(0x1234);

        Assert.Equal(50, pacote.Length);
        Assert.Equal(0x12, pacote[0]);
        Assert.Equal(0x34, pacote[1]);
        Assert.Equal(32, pacote[12]);
        Assert.Equal("CKAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", Encoding.ASCII.GetString(pacote, 13, 32));
        Assert.Equal(new byte[] { 0x00, 0x00, 0x21, 0x00, 0x01 }, pacote[45..]);
    }

    [Fact]
    public void Le_nome_do_computador_grupo_e_mac()
    {
        var resposta = MontarResposta(
            0x1234,
            [("ESTACAO-01", 0x00, false), ("MTGRUPO", 0x00, true), ("ESTACAO-01", 0x20, false)],
            [0x00, 0x1B, 0x63, 0x01, 0x02, 0x03]);

        var status = NetBios.InterpretarResposta(resposta, 0x1234);

        Assert.NotNull(status);
        Assert.Equal("ESTACAO-01", status.NomeComputador);
        Assert.Equal("MTGRUPO", status.Grupo);
        Assert.Equal("00:1B:63:01:02:03", EnderecoMac.Formatar(status.Mac));
        Assert.Equal(3, status.Nomes.Count);
    }

    [Fact]
    public void Samba_manda_mac_zerado_e_ele_e_ignorado()
    {
        var resposta = MontarResposta(7, [("NAS", 0x20, false)], new byte[6]);

        var status = NetBios.InterpretarResposta(resposta, 7);

        Assert.NotNull(status);
        Assert.Equal("NAS", status.NomeComputador);
        Assert.Null(status.Mac);
    }

    [Fact]
    public void Recusa_resposta_de_outra_consulta_e_pacote_cortado()
    {
        var resposta = MontarResposta(1, [("PC", 0x00, false)], new byte[6]);

        Assert.Null(NetBios.InterpretarResposta(resposta, 2));
        Assert.Null(NetBios.InterpretarResposta(resposta.AsSpan(0, 60), 1));
        Assert.Null(NetBios.InterpretarResposta(new byte[5]));
    }

    private static byte[] MontarResposta(ushort id, (string Nome, byte Sufixo, bool Grupo)[] nomes, byte[] mac)
    {
        var p = new List<byte>();
        var cabecalho = new byte[12];
        BinaryPrimitives.WriteUInt16BigEndian(cabecalho, id);
        BinaryPrimitives.WriteUInt16BigEndian(cabecalho.AsSpan(2), 0x8400);
        BinaryPrimitives.WriteUInt16BigEndian(cabecalho.AsSpan(6), 1);
        p.AddRange(cabecalho);
        p.AddRange(NetBios.MontarConsultaStatus(id).AsSpan(12, 34).ToArray());
        p.AddRange([0x00, 0x21, 0x00, 0x01, 0, 0, 0, 0]);
        var dados = new List<byte> { (byte)nomes.Length };
        foreach (var (nome, sufixo, grupo) in nomes)
        {
            dados.AddRange(Encoding.ASCII.GetBytes(nome.PadRight(15)));
            dados.Add(sufixo);
            dados.AddRange(grupo ? [0x84, 0x00] : [0x04, 0x00]);
        }

        dados.AddRange(mac);
        dados.AddRange(new byte[40]);
        p.Add((byte)(dados.Count >> 8));
        p.Add((byte)dados.Count);
        p.AddRange(dados);
        return p.ToArray();
    }
}

public class MdnsTestes
{
    [Fact]
    public void Monta_o_nome_reverso()
    {
        Assert.Equal("10.0.168.192.in-addr.arpa", Mdns.NomeReverso(IPAddress.Parse("192.168.0.10")));
    }

    [Fact]
    public void Consulta_ptr_tem_rotulos_e_tipo_certos()
    {
        var pacote = Mdns.MontarConsultaPtr(0xABCD, "10.0.168.192.in-addr.arpa");
        var pos = 12;

        Assert.Equal("10.0.168.192.in-addr.arpa", Mdns.LerNome(pacote, ref pos));
        Assert.Equal(new byte[] { 0, 12, 0, 1 }, pacote[pos..]);
    }

    [Fact]
    public void Le_resposta_com_nome_comprimido()
    {
        var pergunta = "10.0.168.192.in-addr.arpa";
        var resposta = MontarResposta(pergunta, "impressora-sala.local", comprimirDono: true);

        Assert.Equal("impressora-sala.local", Mdns.InterpretarRespostaPtr(resposta, pergunta));
    }

    [Fact]
    public void Ignora_ptr_de_outro_nome()
    {
        var resposta = MontarResposta("_http._tcp.local", "servico.local", comprimirDono: false);

        Assert.Null(Mdns.InterpretarRespostaPtr(resposta, "10.0.168.192.in-addr.arpa"));
    }

    [Fact]
    public void Nao_entra_em_laco_com_ponteiro_circular()
    {
        var pacote = new byte[] { 0, 0, 0x84, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0xC0, 12, 0, 12, 0, 1 };
        var pos = 12;

        Assert.Null(Mdns.LerNome(pacote, ref pos));
        Assert.Null(Mdns.InterpretarRespostaPtr(pacote, "x"));
    }

    private static byte[] MontarResposta(string dono, string alvo, bool comprimirDono)
    {
        var p = new List<byte>();
        var cabecalho = new byte[12];
        BinaryPrimitives.WriteUInt16BigEndian(cabecalho.AsSpan(2), 0x8400);
        BinaryPrimitives.WriteUInt16BigEndian(cabecalho.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt16BigEndian(cabecalho.AsSpan(6), 1);
        p.AddRange(cabecalho);
        p.AddRange(Mdns.MontarConsultaPtr(0, dono).AsSpan(12).ToArray());
        if (comprimirDono)
        {
            p.AddRange([0xC0, 12]);
        }
        else
        {
            p.AddRange(Mdns.MontarConsultaPtr(0, dono).AsSpan(12, dono.Length + 2).ToArray());
        }

        p.AddRange([0, 12, 0x80, 1, 0, 0, 0, 120]);
        var rotulos = Mdns.MontarConsultaPtr(0, alvo).AsSpan(12, alvo.Length + 2).ToArray();
        p.Add(0);
        p.Add((byte)rotulos.Length);
        p.AddRange(rotulos);
        return p.ToArray();
    }
}

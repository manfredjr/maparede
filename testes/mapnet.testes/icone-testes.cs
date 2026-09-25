using System.Buffers.Binary;

namespace MapNet.Testes;

/// <summary>
/// O ícone do .exe precisa estar no formato DIB clássico em todos os tamanhos. Com imagem
/// comprimida em PNG dentro do .ico, o Smart App Control do Windows chegou a recusar o
/// executável de outro programa da MT (erro 0x800711C7).
/// </summary>
public class IconeTestes
{
    [Fact]
    public void Icone_do_executavel_usa_dib_em_todos_os_tamanhos()
    {
        var caminho = Path.Combine(CaracteresProibidosTestes.RaizDoRepositorio(), "src", "mapnet", "recursos", "mapnet.ico");
        var dados = File.ReadAllBytes(caminho);
        var quantidade = BinaryPrimitives.ReadUInt16LittleEndian(dados.AsSpan(4));

        Assert.True(quantidade >= 4, "O ícone precisa ter vários tamanhos.");
        for (var i = 0; i < quantidade; i++)
        {
            var inicio = BinaryPrimitives.ReadInt32LittleEndian(dados.AsSpan(6 + (16 * i) + 12));
            var png = dados[inicio] == 0x89 && dados[inicio + 1] == (byte)'P';
            Assert.False(png, $"A imagem {i + 1} do ícone está em PNG.");
        }
    }
}

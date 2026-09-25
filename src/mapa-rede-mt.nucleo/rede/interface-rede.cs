using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MapaRedeMt.Nucleo;

public enum TipoInterface
{
    Cabo,
    WiFi,
    Outro,
}

/// <summary>Uma interface de rede ativa com endereço IPv4, do jeito que o técnico escolhe na tela.</summary>
public sealed class InterfaceRede
{
    public required string Id { get; init; }

    public required string Nome { get; init; }

    public string Descricao { get; init; } = string.Empty;

    public TipoInterface Tipo { get; init; }

    /// <summary>Adaptador de máquina virtual, VPN ou similar. Fica no fim da lista.</summary>
    public bool Virtual { get; init; }

    public byte[] Mac { get; init; } = [];

    public required IPAddress Ip { get; init; }

    public required int Prefixo { get; init; }

    public IPAddress? Gateway { get; init; }

    public IReadOnlyList<IPAddress> Dns { get; init; } = [];

    public long VelocidadeBps { get; init; }

    public SubRede SubRede => SubRede.Calcular(Ip, Prefixo);

    public IPAddress Mascara => SubRede.Mascara;

    public string TipoTexto => Tipo switch
    {
        TipoInterface.Cabo => "Cabo",
        TipoInterface.WiFi => "Wi-Fi",
        _ => "Outro",
    };

    public string Resumo => $"{Nome} ({TipoTexto}) - {Ip}/{Prefixo}";

    public override string ToString() => Resumo;
}

/// <summary>Lê as interfaces de rede do computador.</summary>
public static class LeitorInterfaces
{
    private static readonly string[] _palavrasVirtual =
    [
        "virtual", "hyper-v", "vmware", "virtualbox", "vpn", "tap-", "wintun", "wireguard",
        "tailscale", "zerotier", "loopback", "bluetooth", "docker", "wsl",
    ];

    /// <summary>
    /// Interfaces ligadas, com IPv4, fora a de loopback e os túneis. A ordem põe primeiro a
    /// que tem gateway, depois cabo, depois Wi-Fi, e os adaptadores virtuais por último.
    /// </summary>
    public static IReadOnlyList<InterfaceRede> Listar()
    {
        var lista = new List<InterfaceRede>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up
                || ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            IPInterfaceProperties propriedades;
            try
            {
                propriedades = ni.GetIPProperties();
            }
            catch (NetworkInformationException)
            {
                continue;
            }

            var gateway = propriedades.GatewayAddresses
                .Select(g => g.Address)
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !a.Equals(IPAddress.Any));
            var dns = propriedades.DnsAddresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork).ToList();
            var texto = (ni.Name + " " + ni.Description).ToLowerInvariant();

            foreach (var endereco in propriedades.UnicastAddresses)
            {
                if (endereco.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(endereco.Address))
                {
                    continue;
                }

                var prefixo = LerPrefixo(endereco);
                if (prefixo is null or 0 or 32)
                {
                    continue;
                }

                lista.Add(new InterfaceRede
                {
                    Id = ni.Id,
                    Nome = ni.Name,
                    Descricao = ni.Description,
                    Tipo = ni.NetworkInterfaceType switch
                    {
                        NetworkInterfaceType.Wireless80211 => TipoInterface.WiFi,
                        NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet
                            or NetworkInterfaceType.FastEthernetT or NetworkInterfaceType.FastEthernetFx
                            or NetworkInterfaceType.Ethernet3Megabit => TipoInterface.Cabo,
                        _ => TipoInterface.Outro,
                    },
                    Virtual = _palavrasVirtual.Any(texto.Contains),
                    Mac = ni.GetPhysicalAddress().GetAddressBytes(),
                    Ip = endereco.Address,
                    Prefixo = prefixo.Value,
                    Gateway = gateway,
                    Dns = dns,
                    VelocidadeBps = LerVelocidade(ni),
                });
            }
        }

        return lista
            .OrderBy(i => i.Virtual)
            .ThenBy(i => i.Gateway is null)
            .ThenBy(i => i.Tipo)
            .ThenBy(i => i.Nome, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static int? LerPrefixo(UnicastIPAddressInformation endereco)
    {
        try
        {
            if (endereco.PrefixLength is > 0 and <= 32)
            {
                return endereco.PrefixLength;
            }
        }
        catch (PlatformNotSupportedException)
        {
            // Segue para a máscara.
        }

        try
        {
            var mascara = endereco.IPv4Mask;
            return mascara is null ? null : SubRede.PrefixoDaMascara(mascara);
        }
        catch (Exception e) when (e is ArgumentException or PlatformNotSupportedException)
        {
            return null;
        }
    }

    private static long LerVelocidade(NetworkInterface ni)
    {
        try
        {
            return ni.Speed;
        }
        catch (Exception e) when (e is PlatformNotSupportedException or NetworkInformationException)
        {
            return 0;
        }
    }
}

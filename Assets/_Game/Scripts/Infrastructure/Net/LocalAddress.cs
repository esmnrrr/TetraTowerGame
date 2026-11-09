using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Game.Infrastructure.Net
{
    /// <summary>Bu bilgisayarın yerel ağdaki IPv4 adresi ve "ip:port" ayrıştırma.</summary>
    public static class LocalAddress
    {
        public static string GetLocalIPv4()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                        if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                            return ua.Address.ToString();
                }
            }
            catch { /* ağ bilgisi okunamazsa loopback'e düş */ }
            return "127.0.0.1";
        }

        /// <summary>"192.168.1.5" veya "192.168.1.5:7777" kabul eder.</summary>
        public static bool TryParse(string input, ushort defaultPort, out string ip, out ushort port)
        {
            ip = null; port = defaultPort;
            if (string.IsNullOrWhiteSpace(input)) return false;

            string host = input.Trim();
            int colon = host.LastIndexOf(':');
            if (colon > 0)
            {
                if (!ushort.TryParse(host.Substring(colon + 1), out port)) return false;
                host = host.Substring(0, colon);
            }
            if (!IPAddress.TryParse(host, out _)) return false;
            ip = host;
            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using elaphureLink.Wpf.ViewModel;

namespace elaphureLink.Wpf.Core.Services
{
    public static class UdpDiscovery
    {
        private const int DiscoveryPort = 50000;
        private static readonly byte[] DiscoveryRequest =
            Encoding.ASCII.GetBytes("ELAPHURE_DISCOVERY?");

        public static async Task<List<DiscoveredDevice>> ScanDevicesAsync(int timeoutMs)
        {
            var results = new Dictionary<string, DiscoveredDevice>();
            var targets = GetBroadcastEndpoints(DiscoveryPort).ToList();
            if (targets.Count == 0)
                return new List<DiscoveredDevice>();

            var udp = new UdpClient(AddressFamily.InterNetwork);
            try
            {
                udp.EnableBroadcast = true;
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

                // 发送 discovery
                foreach (var ep in targets)
                {
                    await udp.SendAsync(DiscoveryRequest, DiscoveryRequest.Length, ep);
                }

                int startTick = Environment.TickCount;

                while (true)
                {
                    int elapsed = unchecked(Environment.TickCount - startTick);
                    if (elapsed > timeoutMs)
                        break;

                    var receiveTask = udp.ReceiveAsync();
                    var delayTask = Task.Delay(120);

                    var completed = await Task.WhenAny(receiveTask, delayTask);
                    if (completed != receiveTask)
                        continue;

                    UdpReceiveResult recv;
                    try
                    {
                        recv = receiveTask.Result;
                    }
                    catch
                    {
                        continue;
                    }

                    string ip = recv.RemoteEndPoint.Address.ToString();
                    if (results.ContainsKey(ip))
                        continue;

                    string payload;
                    try
                    {
                        payload = Encoding.UTF8.GetString(recv.Buffer);
                    }
                    catch
                    {
                        payload = BitConverter.ToString(recv.Buffer);
                    }

                    var dev = new DiscoveredDevice
                    {
                        Ip = ip,
                        Port = recv.RemoteEndPoint.Port,
                        Name = "elaphureLink"
                    };

                    results[ip] = dev;
                }
            }
            finally
            {
                udp.Close();
            }

            return results.Values.OrderBy(d => d.Ip).ToList();
        }

        private static IEnumerable<IPEndPoint> GetBroadcastEndpoints(int port)
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                    continue;

                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    continue;

                IPInterfaceProperties props;
                try
                {
                    props = ni.GetIPProperties();
                }
                catch
                {
                    continue;
                }

                foreach (var ua in props.UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    if (ua.IPv4Mask == null)
                        continue;

                    var broadcast = CalcBroadcast(ua.Address, ua.IPv4Mask);
                    yield return new IPEndPoint(broadcast, port);
                }
            }
        }

        private static IPAddress CalcBroadcast(IPAddress ip, IPAddress mask)
        {
            byte[] ipb = ip.GetAddressBytes();
            byte[] mb = mask.GetAddressBytes();
            byte[] bb = new byte[4];

            for (int i = 0; i < 4; i++)
                bb[i] = (byte)(ipb[i] | (byte)~mb[i]);

            return new IPAddress(bb);
        }
    }
}

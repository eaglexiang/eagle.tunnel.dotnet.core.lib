using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace eagle.tunnel.dotnet.core
{
    public class EagleTunnelSender
    {
        private static ConcurrentDictionary<string, DnsCache> dnsCaches =
            new ConcurrentDictionary<string, DnsCache>();

        public static ConcurrentDictionary<string, bool> insideCache;

        public static void FlushDnsCaches()
        {
            dnsCaches = new ConcurrentDictionary<string, DnsCache>();
        }

        private static bool IsRunning { get; set; } = false;
        private static ConcurrentQueue<Tunnel> tunnels2Allot =
            new ConcurrentQueue<Tunnel>();
        private const int maxCountOfTunnels2Allot = 20;

        private static Tunnel NewTunnel2Remote()
        {
            Tunnel result;
            if (tunnels2Allot.TryDequeue(out Tunnel tunnel))
            {
                result = tunnel;
            }
            else
            {
                result = CreateTunnel();
            }
            return result;
        }

        private static void KeepTunnelPool()
        {
            while (IsRunning)
            {
                if (tunnels2Allot.Count > maxCountOfTunnels2Allot)
                {
                    System.Threading.Thread.Sleep(100);
                }
                else
                {
                    Tunnel tunnel = CreateTunnel();
                    if (tunnel != null)
                    {
                        tunnels2Allot.Enqueue(tunnel);
                    }
                }
            }
        }

        public static void OpenTunnelPool()
        {
            if (IsRunning == false)
            {
                Thread thread2KeepTunnelPool = new Thread(KeepTunnelPool)
                {
                    IsBackground = true
                };
                IsRunning = true;
                thread2KeepTunnelPool.Start();
            }
        }

        public static void CloseTunnelPool()
        {
            IsRunning = false;
        }

        private static Tunnel CreateTunnel()
        {
            Tunnel result = null;
            Socket socket2Server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ipeOfServer = Conf.GetRemoteIPEndPoint();
            if (ipeOfServer != null)
            {
                try
                {
                    socket2Server.Connect(ipeOfServer);
                }
                catch { socket2Server = null; }
                if (socket2Server != null)
                {
                    Tunnel tunnel = CheckVersion(socket2Server);
                    if (CheckUser(tunnel))
                    {
                        result = tunnel;
                    }
                    else
                    {
                        try
                        {
                            socket2Server.Shutdown(SocketShutdown.Both);
                            System.Threading.Thread.Sleep(10);
                            socket2Server.Close();
                        }
                        catch (SocketException) {; }
                    }
                }
            }
            return result;
        }

        public static Tunnel Handle(EagleTunnelHandler.EagleTunnelRequestType type, EagleTunnelArgs e)
        {
            Tunnel result = null;
            if (e != null)
            {
                switch (type)
                {
                    case EagleTunnelHandler.EagleTunnelRequestType.DNS:
                        SendDNSReq(e);
                        break;
                    case EagleTunnelHandler.EagleTunnelRequestType.TCP:
                        SendTCPReq(out result, e);
                        break;
                    case EagleTunnelHandler.EagleTunnelRequestType.LOCATION:
                        SendLOCATIONReq(e);
                        break;
                    case EagleTunnelHandler.EagleTunnelRequestType.Unknown:
                    default:
                        break;
                }
            }
            return result;
        }

        private static void SendLOCATIONReq(EagleTunnelArgs e)
        {
            string ip2Resolv = e.IP.ToString();
            // local cache resolv firstly
            if (EagleTunnelHandler.insideCache.ContainsKey(ip2Resolv))
            {
                e.EnableProxy = !EagleTunnelHandler.insideCache[ip2Resolv];
                e.Success = true;
            }
            else
            {
                // req remote
                if (CheckIfInsideByRemote(ip2Resolv, out bool inside))
                {
                    e.EnableProxy = !inside;
                    e.Success = true;
                }
                else
                {
                    EagleTunnelHandler.ips2Resolv.Enqueue(ip2Resolv);
                }
            }
        }

        private static bool CheckIfInsideByRemote(string ip2Resolv, out bool inside)
        {
            bool result = false;
            inside = false;
            // Tunnel tunnel2Remote = NewTunnel2Remote ();
            Tunnel tunnel2Remote = CreateTunnel();
            if (tunnel2Remote != null)
            {
                if (tunnel2Remote.WriteR("LOCATION " + ip2Resolv))
                {
                    string reply = tunnel2Remote.ReadStringR();
                    if (!string.IsNullOrEmpty(reply))
                    {
                        if (bool.TryParse(reply, out inside))
                        {
                            result = true;
                        }
                    }
                }
                tunnel2Remote.Close();
            }
            return result;
        }

        private static Tunnel CheckVersion(Socket socket2Server)
        {
            Tunnel result = null;
            if (socket2Server != null)
            {
                string req = "eagle_tunnel " + Server.ProtocolVersion + " simple";
                byte[] buffer = Encoding.ASCII.GetBytes(req);
                int written;
                try
                {
                    written = socket2Server.Send(buffer);
                }
                catch { written = 0; }
                if (written > 0)
                {
                    buffer = new byte[100];
                    int read;
                    try
                    {
                        read = socket2Server.Receive(buffer);
                    }
                    catch { read = 0; }
                    if (read > 0)
                    {
                        string reply = Encoding.UTF8.GetString(buffer, 0, read);
                        if (reply == "valid valid valid")
                        {
                            result = new Tunnel(null, socket2Server);
                            result.EncryptR = true;
                        }
                    }
                }
            }
            return result;
        }

        private static bool CheckUser(Tunnel tunnel)
        {
            bool result = false;
            if (tunnel != null)
            {
                if (Conf.LocalUser != null)
                {
                    bool done = tunnel.WriteR(Conf.LocalUser.ToString());
                    if (done)
                    {
                        string reply = tunnel.ReadStringR();
                        if (!string.IsNullOrEmpty(reply))
                        {
                            result = reply == "valid";
                        }
                    }
                }
                else
                {
                    result = true;
                }
            }
            return result;
        }

        private static void SendDNSReq(EagleTunnelArgs e)
        {
            if (e != null)
            {
                if (e.Domain != null)
                {
                    if (Conf.hosts.ContainsKey(e.Domain))
                    {
                        e.IP = Conf.hosts[e.Domain];
                        e.Success = true;
                    }
                    else
                    {
                        if (dnsCaches.ContainsKey(e.Domain))
                        {
                            if (!dnsCaches[e.Domain].IsDead)
                            {
                                e.IP = dnsCaches[e.Domain].IP;
                                e.Success = true;
                            }
                            else
                            {
                                e.IP = ResolvDomain(e);
                                if (e.IP != null)
                                {
                                    dnsCaches[e.Domain].IP = e.IP;
                                    e.Success = true;
                                }
                            }
                        }
                        else
                        {
                            e.IP = ResolvDomain(e);
                            if (e.IP != null)
                            {
                                DnsCache cache = new DnsCache(e.Domain, e.IP, Conf.DnsCacheTtl);
                                dnsCaches.TryAdd(e.Domain, cache);
                                e.Success = true;
                            }
                        }
                    }
                }
            }
        }

        private static IPAddress ResolvDomain(EagleTunnelArgs e)
        {
            IPAddress result = null;
            if (e.EnableProxy)
            {
                result = ResolvByProxy(e.Domain);
            }
            else
            {
                result = ResolvByLocal(e.Domain);
                if (result == null)
                {
                    result = ResolvByProxy(e.Domain);
                }
            }
            return result;
        }

        private static IPAddress ResolvByProxy(string domain)
        {
            IPAddress result = null;
            // Tunnel tunnel = NewTunnel2Remote ();
            Tunnel tunnel = CreateTunnel();
            if (tunnel != null)
            {
                string req = EagleTunnelHandler.EagleTunnelRequestType.DNS.ToString();
                req += " " + domain;
                bool done = tunnel.WriteR(req);
                if (done)
                {
                    string reply = tunnel.ReadStringR();
                    if (!string.IsNullOrEmpty(reply) && reply != "nok")
                    {
                        if (IPAddress.TryParse(reply, out IPAddress ip))
                        {
                            result = ip;
                        }
                    }
                }
                tunnel.Close();
            }
            return result;
        }

        private static IPAddress ResolvByLocal(string domain)
        {
            IPAddress result = null;
            IPHostEntry iphe;
            try
            {
                iphe = Dns.GetHostEntry(domain);
            }
            catch { iphe = null; }
            if (iphe != null)
            {
                foreach (IPAddress tmp in iphe.AddressList)
                {
                    if (tmp.AddressFamily == AddressFamily.InterNetwork)
                    {
                        result = tmp;
                        break;
                    }
                }
            }
            return result;
        }

        private static void SendTCPReq(out Tunnel tunnel, EagleTunnelArgs e)
        {
            tunnel = null;
            if (e != null && e.EndPoint != null)
            {
                if (e.EnableProxy)
                {
                    ConnectByProxy(out tunnel, e);
                }
                else
                {
                    DirectConnect(out tunnel, e);
                }
            }
        }

        private static void ConnectByProxy(out Tunnel tunnel, EagleTunnelArgs e)
        {
            // tunnel = NewTunnel2Remote ();
            tunnel = CreateTunnel();
            if (tunnel != null)
            {
                string req = EagleTunnelHandler.EagleTunnelRequestType.TCP.ToString();
                req += ' ' + e.EndPoint.Address.ToString();
                req += ' ' + e.EndPoint.Port.ToString();
                bool done = tunnel.WriteR(req);
                if (done)
                {
                    string reply = tunnel.ReadStringR();
                    if (reply != "ok")
                    {
                        tunnel.Close();
                        tunnel = null;
                    }
                }
            }
        }

        private static void DirectConnect(out Tunnel tunnel, EagleTunnelArgs e)
        {
            tunnel = null;
            Socket socket2Server = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp);
            try
            {
                socket2Server.Connect(e.EndPoint);
            }
            catch (SocketException)
            {
                socket2Server.Close();
            }
            if (socket2Server.Connected)
            {
                tunnel = new Tunnel(null, socket2Server);
            }
        }
    }
}
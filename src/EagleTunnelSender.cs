using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace eagle.tunnel.dotnet.core
{
    public class EagleTunnelSender
    {
        private static bool CreateTunnel (out Tunnel tunnel)
        {
            bool succeed = false;
            Tunnel result = new Tunnel (null, null, Conf.encryptionKey);
            succeed = Connect2Relayer (result);
            if (succeed)
            {
                tunnel = result;
            }
            else
            {
                result.Close ();
                tunnel = null;
            }
            return succeed;
        }

        public static bool Connect2Relayer (Tunnel tunnel)
        {
            bool result = false;
            if (tunnel != null)
            {
                Socket socket2Server = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPEndPoint ipeOfServer = Conf.GetRemoteIPEndPoint ();
                try
                {
                    socket2Server.Connect (ipeOfServer);
                }
                catch { socket2Server = null; }
                tunnel.SocketR = socket2Server;
                tunnel.EncryptR = false;
                if (CheckVersion (tunnel))
                {
                    tunnel.EncryptR = true;
                    result = CheckUser (tunnel);
                }
            }
            return result;
        }

        public static bool Handle (EagleTunnelHandler.EagleTunnelRequestType type, EagleTunnelArgs e)
        {
            bool result = false;
            if (e != null)
            {
                switch (type)
                {
                    case EagleTunnelHandler.EagleTunnelRequestType.DNS:
                        SendDNSReq (e);
                        break;
                    case EagleTunnelHandler.EagleTunnelRequestType.TCP:
                        result = SendTCPReq (e);
                        break;
                    case EagleTunnelHandler.EagleTunnelRequestType.LOCATION:
                        SendLOCATIONReq (e);
                        break;
                    case EagleTunnelHandler.EagleTunnelRequestType.Unknown:
                    default:
                        break;
                }
            }
            return result;
        }

        private static void SendLOCATIONReq (EagleTunnelArgs e)
        {
            string ip2Resolv = e.IP.ToString ();
            // local cache resolv firstly
            if (EagleTunnelHandler.insideCache.ContainsKey (ip2Resolv))
            {
                e.EnableProxy = !EagleTunnelHandler.insideCache[ip2Resolv];
                e.Success = true;
            }
            else
            {
                // req remote
                if (CheckIfInsideByRemote (ip2Resolv, out bool inside))
                {
                    e.EnableProxy = !inside;
                    EagleTunnelHandler.insideCache.TryAdd (ip2Resolv, inside);
                    e.Success = true;
                }
                else
                {
                    EagleTunnelHandler.ips2Resolv.Enqueue (ip2Resolv);
                }
            }
        }

        private static bool CheckIfInsideByRemote (string ip2Resolv, out bool inside)
        {
            bool result = false;
            inside = false;
            // Tunnel tunnel2Remote = NewTunnel2Remote ();
            if (CreateTunnel (out Tunnel tunnel2Remote))
            {
                if (tunnel2Remote.WriteR ("LOCATION " + ip2Resolv))
                {
                    string reply = tunnel2Remote.ReadStringR ();
                    if (!string.IsNullOrEmpty (reply))
                    {
                        if (bool.TryParse (reply, out inside))
                        {
                            result = true;
                        }
                    }
                }
                tunnel2Remote.Close ();
            }
            return result;
        }

        private static bool CheckVersion (Tunnel tunnel)
        {
            bool isValid = false;
            string req = "eagle_tunnel " + Server.ProtocolVersion + " simple";
            if (tunnel.WriteR (req, Encoding.ASCII))
            {
                string reply = tunnel.ReadStringR ();
                if (!string.IsNullOrEmpty (reply))
                {
                    isValid = reply == "valid valid valid";
                }
            }
            return isValid;
        }

        private static bool CheckUser (Tunnel tunnel)
        {
            bool result = false;
            if (tunnel != null)
            {
                if (Conf.LocalUser != null)
                {
                    bool done = tunnel.WriteR (Conf.LocalUser.ToString ());
                    if (done)
                    {
                        string reply = tunnel.ReadStringR ();
                        if (!string.IsNullOrEmpty (reply))
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

        private static void SendDNSReq (EagleTunnelArgs e)
        {
            if (e != null)
            {
                if (e.Domain != null)
                {
                    if (Conf.hosts.ContainsKey (e.Domain))
                    {
                        e.IP = Conf.hosts[e.Domain];
                        e.Success = true;
                    }
                    else
                    {
                        if (EagleTunnelHandler.dnsCaches.ContainsKey (e.Domain))
                        {
                            if (!EagleTunnelHandler.dnsCaches[e.Domain].IsDead)
                            {
                                e.IP = EagleTunnelHandler.dnsCaches[e.Domain].IP;
                                e.Success = true;
                            }
                            else
                            {
                                if (ResolvDomain (e))
                                {
                                    EagleTunnelHandler.dnsCaches[e.Domain].IP = e.IP;
                                    e.Success = true;
                                }
                            }
                        }
                        else
                        {
                            if (ResolvDomain (e))
                            {
                                DnsCache cache = new DnsCache (e.Domain, e.IP, Conf.DnsCacheTtl);
                                EagleTunnelHandler.dnsCaches.TryAdd (e.Domain, cache);
                                e.Success = true;
                            }
                        }
                    }
                }
            }
        }

        private static bool ResolvDomain (EagleTunnelArgs e)
        {
            bool result;
            if (e.EnableProxy)
            {
                result = ResolvByProxy (e);
            }
            else
            {
                result = ResolvByLocal (e);
                if (!result)
                {
                    result = ResolvByProxy (e);
                }
            }
            return result;
        }

        private static bool ResolvByProxy (EagleTunnelArgs e)
        {
            bool result = false;
            if (CreateTunnel (out Tunnel tunnel))
            {
                string req = EagleTunnelHandler.EagleTunnelRequestType.DNS.ToString ();
                req += " " + e.Domain;
                bool done = tunnel.WriteR (req);
                if (done)
                {
                    string reply = tunnel.ReadStringR ();
                    if (!string.IsNullOrEmpty (reply) && reply != "nok")
                    {
                        if (IPAddress.TryParse (reply, out IPAddress ip))
                        {
                            e.IP = ip;
                            result = true;
                        }
                    }
                }
                tunnel.Close ();
            }
            return result;
        }

        private static bool ResolvByLocal (EagleTunnelArgs e)
        {
            bool result = false;
            IPHostEntry iphe;
            try
            {
                iphe = Dns.GetHostEntry (e.Domain);
            }
            catch { iphe = null; }
            if (iphe != null)
            {
                foreach (IPAddress tmp in iphe.AddressList)
                {
                    if (tmp.AddressFamily == AddressFamily.InterNetwork)
                    {
                        e.IP = tmp;
                        result = true;
                        break;
                    }
                }
            }
            return result;
        }

        private static bool SendTCPReq (EagleTunnelArgs e)
        {
            bool result = false;
            if (e != null && e.EndPoint != null)
            {
                if (e.EnableProxy)
                {
                    result = ConnectByProxy (e);
                }
                else
                {
                    result = DirectConnect (e);
                }
            }
            return result;
        }

        private static bool ConnectByProxy (EagleTunnelArgs e)
        {
            bool succeed = false;
            if (e.tunnel != null)
            {
                Tunnel tunnel = e.tunnel;
                if (Connect2Relayer (tunnel))
                {
                    string req = EagleTunnelHandler.EagleTunnelRequestType.TCP.ToString ();
                    req += ' ' + e.EndPoint.Address.ToString ();
                    req += ' ' + e.EndPoint.Port.ToString ();
                    bool done = tunnel.WriteR (req);
                    if (done)
                    {
                        string reply = tunnel.ReadStringR ();
                        succeed = reply == "ok";
                    }
                }
            }
            return succeed;
        }

        private static bool DirectConnect (EagleTunnelArgs e)
        {
            bool result = false;
            Socket socket2Server = new Socket (AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp);
            try
            {
                socket2Server.Connect (e.EndPoint);
            }
            catch (SocketException)
            {
                socket2Server.Close ();
            }
            if (socket2Server.Connected)
            {
                e.tunnel.SocketR = socket2Server;
                result = true;
            }
            return result;
        }
    }
}
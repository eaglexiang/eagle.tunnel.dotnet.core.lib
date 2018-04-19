using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace eagle.tunnel.dotnet.core {
    public class EagleTunnelSender {
        private static ConcurrentDictionary<string, DnsCache> dnsCaches =
            new ConcurrentDictionary<string, DnsCache> ();

        private static Tunnel CreateTunnel (int retryTimes = 3) {
            Tunnel result = null;
            if (retryTimes > 0) {
                Socket socket2Server = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPEndPoint ipeOfServer = Conf.GetRemoteIPEndPoint ();
                try {
                    socket2Server.Connect (ipeOfServer);
                } catch { socket2Server = null; }
                if (socket2Server != null) {
                    Tunnel tunnel = CheckVersion (socket2Server);
                    if (CheckUser (tunnel)) {
                        result = tunnel;
                    } else {
                        try {
                            socket2Server.Shutdown (SocketShutdown.Both);
                            System.Threading.Thread.Sleep (10);
                            socket2Server.Close ();
                        } catch (SocketException) {; }
                    }
                }
                if (result == null) {
                    result = CreateTunnel (--retryTimes);
                }
            }
            return result;
        }

        public static Tunnel Handle (EagleTunnelHandler.EagleTunnelRequestType type, EagleTunnelArgs e) {
            Tunnel result = null;
            if (type != EagleTunnelHandler.EagleTunnelRequestType.Unknown &&
                e != null) {
                switch (type) {
                    case EagleTunnelHandler.EagleTunnelRequestType.DNS:
                        SendDNSReq (e);
                        break;
                    case EagleTunnelHandler.EagleTunnelRequestType.TCP:
                        SendTCPReq (out result, e);
                        break;
                }
            }
            return result;
        }

        private static Tunnel CheckVersion (Socket socket2Server) {
            Tunnel result = null;
            if (socket2Server != null) {
                string req = "eagle_tunnel 1.0 simple";
                byte[] buffer = Encoding.ASCII.GetBytes (req);
                int written;
                try {
                    written = socket2Server.Send (buffer);
                } catch { written = 0; }
                if (written > 0) {
                    buffer = new byte[100];
                    int read;
                    try {
                        read = socket2Server.Receive (buffer);
                    } catch { read = 0; }
                    if (read > 0) {
                        string reply = Encoding.UTF8.GetString (buffer, 0, read);
                        if (reply == "valid valid valid") {
                            result = new Tunnel (null, socket2Server);
                            result.EncryptR = true;
                        }
                    }
                }
            }
            return result;
        }

        private static bool CheckUser (Tunnel tunnel) {
            bool result = false;
            if (Conf.LocalUser != null) {
                bool done = tunnel.WriteR (Conf.LocalUser.ToString ());
                if (done) {
                    string reply = tunnel.ReadStringR ();
                    if (!string.IsNullOrEmpty (reply)) {
                        result = reply == "valid";
                    }
                }
            } else {
                result = true;
            }
            if (result) {
                Conf.LocalUser.AddTunnel (tunnel);
            }
            return result;
        }

        private static void SendDNSReq (EagleTunnelArgs e) {
            if (e != null) {
                e.IP = null;
                if (e.Domain != null) {
                    if (dnsCaches.ContainsKey (e.Domain)) {
                        if (!dnsCaches[e.Domain].IsDead) {
                            e.IP = dnsCaches[e.Domain].IP;
                        } else {
                            e.IP = ResolvDomain (e.Domain);
                            if (e.IP != null) {
                                dnsCaches[e.Domain].IP = e.IP;
                            }
                        }
                    } else {
                        e.IP = ResolvDomain (e.Domain);
                        if (e.IP != null) {
                            DnsCache cache = new DnsCache (e.Domain, e.IP, Conf.DnsCacheTti);
                            dnsCaches.TryAdd (e.Domain, cache);
                        }
                    }
                }
            }
        }

        private static IPAddress ResolvDomain (string domain) {
            IPAddress result = null;
            Tunnel tunnel = CreateTunnel ();
            if (tunnel != null) {
                string req = EagleTunnelHandler.EagleTunnelRequestType.DNS.ToString ();
                req += " " + domain;
                bool done = tunnel.WriteR (req);
                if (done) {
                    string reply = tunnel.ReadStringR ();
                    if (!string.IsNullOrEmpty (reply) && reply != "nok") {
                        if (IPAddress.TryParse (reply, out IPAddress ip)) {
                            result = ip;
                        }
                    }
                }
                tunnel.Close();
            }
            return result;
        }

        private static void SendTCPReq (out Tunnel tunnel, EagleTunnelArgs e) {
            tunnel = null;
            if (e != null && e.EndPoint != null) {
                tunnel = CreateTunnel ();
                if (tunnel != null) {
                    string req = EagleTunnelHandler.EagleTunnelRequestType.TCP.ToString ();
                    req += ' ' + e.EndPoint.Address.ToString ();
                    req += ' ' + e.EndPoint.Port.ToString ();
                    bool done = tunnel.WriteR (req);
                    if (done) {
                        string reply = tunnel.ReadStringR ();
                        if (reply != "ok") {
                            tunnel.Close ();
                            tunnel = null;
                        }
                    }
                }
            }
        }
    }
}
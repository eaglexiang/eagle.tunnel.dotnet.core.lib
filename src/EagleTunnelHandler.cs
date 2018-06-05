using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace eagle.tunnel.dotnet.core {
    public class EagleTunnelHandler {
        public enum EagleTunnelRequestType {
            TCP,
            UDP,
            DNS,
            Unknown
        }

        private static ConcurrentDictionary<string, DnsCache> dnsCaches =
            new ConcurrentDictionary<string, DnsCache> ();

        public static bool Handle (string firstMsg, Tunnel tunnel) {
            bool result;
            if (!string.IsNullOrEmpty (firstMsg) &&
                tunnel != null) {
                result = CheckVersion (firstMsg, tunnel);
                if (result) {
                    EagleTunnelUser user = CheckAuthen (tunnel);
                    if (user != null) {
                        string req = tunnel.ReadStringL ();
                        if (!string.IsNullOrEmpty (req)) {
                            EagleTunnelRequestType type = GetType (req);
                            switch (type) {
                                case EagleTunnelRequestType.DNS:
                                    HandleDNSReq (req, tunnel);
                                    // no need to continue;
                                    break;
                                case EagleTunnelRequestType.TCP:
                                    result = TCPReqHandle (req, tunnel);
                                    if(result){
                                        user.AddTunnel(tunnel);
                                    }
                                    break;
                            }
                        }
                    }
                }
            } else {
                result = false;
            }
            return result;
        }

        private static bool CheckVersion (string firstMsg, Tunnel tunnel) {
            bool result;
            string[] args = firstMsg.Split (' ');
            if (args.Length >= 3) {
                string reply = "";
                result = args[0] == "eagle_tunnel";
                reply = result ? "valid" : "invalid";
                bool valid1 = args[1] == "1.0";
                result &= valid1;
                reply += valid1 ? " valid" : " invalid";
                valid1 = args[2] == "simple";
                result &= valid1;
                reply += valid1 ? " valid" : " invalid";
                if (result) {
                    result = tunnel.WriteL (reply);
                    if (result) {
                        tunnel.EncryptL = true;
                    }
                }
            } else {
                result = false;
            }
            return result;
        }

        private static EagleTunnelUser CheckAuthen (Tunnel tunnel) {
            EagleTunnelUser result = null;
            if (Conf.allConf.ContainsKey ("user-check") && Conf.allConf["user-check"][0] == "on") {
                byte[] buffer = new byte[100];
                string req = tunnel.ReadStringL ();
                if (!string.IsNullOrEmpty (req)) {
                    if (EagleTunnelUser.TryParse (req, out EagleTunnelUser user, false)) {
                        result = EagleTunnelUser.Check(user.ID, user.Password);
                    }
                }
                string reply = result != null ? "valid" : "invalid";
                result = tunnel.WriteL (reply) ? result : null;
            } else {
                result = EagleTunnelUser.users["anonymous"];
            }
            return result;
        }

        private static EagleTunnelRequestType GetType (string msg) {
            EagleTunnelRequestType result = EagleTunnelRequestType.Unknown;
            string[] args = msg.Split (' ');
            if (!System.Enum.TryParse (args[0], out result)) {
                result = EagleTunnelRequestType.Unknown;
            }
            return result;
        }

        private static void HandleDNSReq (string msg, Tunnel tunnel) {
            if (!string.IsNullOrEmpty (msg) && tunnel != null) {
                string[] args = msg.Split (' ');
                if (args.Length >= 2) {
                    string domain = args[1];
                    IPAddress ip;
                    if (dnsCaches.ContainsKey (domain)) {
                        if (!dnsCaches[domain].IsDead) {
                            ip = dnsCaches[domain].IP;
                        } else {
                            ip = ResolvDNS (domain);
                            if (ip != null) {
                                dnsCaches[domain].IP = ip;
                            }
                        }
                    } else {
                        ip = ResolvDNS (domain);
                        if (ip != null) {
                            DnsCache cache = new DnsCache (domain, ip, Conf.DnsCacheTti);
                            dnsCaches.TryAdd (cache.Domain, cache);
                        }
                    }
                    string reply;
                    if (ip == null) {
                        reply = "nok";
                    } else {
                        reply = ip.ToString ();
                    }
                    tunnel.WriteL (reply);
                    tunnel.Close ();
                }
            }
        }

        private static IPAddress ResolvDNS (string url) {
            IPAddress result = null;
            IPHostEntry iphe;
            if (!string.IsNullOrEmpty (url)) {
                try {
                    iphe = Dns.GetHostEntry (url);
                } catch { iphe = null; }
                if (iphe != null) {
                    foreach (IPAddress tmp in iphe.AddressList) {
                        if (tmp.AddressFamily == AddressFamily.InterNetwork) {
                            result = tmp;
                            break;
                        }
                    }
                }
            }
            return result;
        }

        private static bool TCPReqHandle (string msg, Tunnel tunnel) {
            bool result = false;
            if (msg != null && tunnel != null) {
                string[] args = msg.Split (' ');
                if (args.Length >= 3) {
                    string ip = args[1];
                    string _port = args[2];
                    if (int.TryParse (_port, out int port)) {
                        if (IPAddress.TryParse (ip, out IPAddress ipa)) {
                            IPEndPoint ipeReq = new IPEndPoint (ipa, port);
                            Socket socket2Server = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                            try {
                                socket2Server.Connect (ipeReq);
                                result = true;
                            } catch {; }
                            if (result) {
                                tunnel.SocketR = socket2Server;
                                result = tunnel.WriteL ("ok");
                            } else {
                                tunnel.WriteL ("nok");
                            }
                        }
                    }
                }
            }
            return result;
        }
    }
}
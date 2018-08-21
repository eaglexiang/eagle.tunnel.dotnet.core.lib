using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace eagle.tunnel.dotnet.core
{
    public class EagleTunnelHandler
    {
        public enum EagleTunnelRequestType
        {
            TCP,
            UDP,
            DNS,
            LOCATION,
            Unknown
        }

        private static ConcurrentDictionary<string, DnsCache> dnsCaches =
            new ConcurrentDictionary<string, DnsCache>();

        private static bool LOCATIONHandlerIsRunning;
        private static Thread threadHandleLOCATION;
        public static ConcurrentDictionary<string, bool> insideCache =
            new ConcurrentDictionary<string, bool>();

        public static ConcurrentQueue<string> ips2Resolv =
            new ConcurrentQueue<string>();

        public static bool Handle(string firstMsg, Tunnel tunnel)
        {
            bool result;
            if (!string.IsNullOrEmpty(firstMsg) &&
                tunnel != null)
            {
                result = CheckVersion(firstMsg, tunnel);
                if (result)
                {
                    EagleTunnelUser user = CheckAuthen(tunnel);
                    if (user != null)
                    {
                        string req = tunnel.ReadStringL();
                        if (!string.IsNullOrEmpty(req))
                        {
                            EagleTunnelRequestType type = GetType(req);
                            switch (type)
                            {
                                case EagleTunnelRequestType.DNS:
                                    HandleDNSReq(req, tunnel);
                                    // no need to continue;
                                    break;
                                case EagleTunnelRequestType.TCP:
                                    result = TCPReqHandle(req, tunnel);
                                    if (result)
                                    {
                                        user.AddTunnel(tunnel);
                                    }
                                    break;
                                case EagleTunnelRequestType.LOCATION:
                                    HandleLOCATIONReq(req, tunnel);
                                    break;
                                case EagleTunnelRequestType.Unknown:
                                default:
                                    break;
                            }
                        }
                    }
                }
            }
            else
            {
                result = false;
            }
            return result;
        }

        private static void HandleLOCATIONReq(string req, Tunnel tunnel)
        {
            string[] reqs = req.Split(' ');
            if (reqs.Length >= 2)
            {
                string ip2Resolv = reqs[1];
                string result;
                if (insideCache.ContainsKey(ip2Resolv))
                {
                    result = insideCache[ip2Resolv].ToString();
                }
                else
                {
                    ips2Resolv.Enqueue(ip2Resolv);
                    result = "not found";
                }
                tunnel.WriteL(result);
            }
            tunnel.Close();
        }

        public static void StartResolvInside()
        {
            if (!LOCATIONHandlerIsRunning)
            {
                LOCATIONHandlerIsRunning = true;
                threadHandleLOCATION = new Thread(threadHandleLOCATION_Handler);
                threadHandleLOCATION.IsBackground = true;
                threadHandleLOCATION.Start();
            }
        }

        public static void StopResolvInside()
        {
            LOCATIONHandlerIsRunning = false;
        }

        private static void threadHandleLOCATION_Handler()
        {
            insideCache = new ConcurrentDictionary<string, bool>();
            while (LOCATIONHandlerIsRunning)
            {
                if (ips2Resolv.IsEmpty)
                {
                    Thread.Sleep(5000);
                }
                else
                {
                    if (ips2Resolv.TryDequeue(out string ip))
                    {
                        if (!insideCache.ContainsKey(ip)) // reduce repeated resolv
                        {
                            if (CheckIfInsideByLocal(ip, out bool result))
                            {
                                if (!insideCache.TryAdd(ip, result))
                                {
                                    throw new System.Exception();
                                }
                            }
                        }
                    }
                }
            }
        }

        private static bool CheckIfInsideByLocal(string ip, out bool result_Bool)
        {
            string result_Str = CheckLocationByLocal(ip);
            bool succeed;
            switch (result_Str)
            {
                case "in":
                    succeed = true;
                    result_Bool = true;
                    break;
                case "out":
                    succeed = true;
                    result_Bool = false;
                    break;
                case "failed":
                default:
                    succeed = false;
                    result_Bool = false;
                    break;
            }
            return succeed;
        }

        public static string CheckLocationByLocal(string ip)
        {
            string result = "failed";
            string req = @"https://ip2c.org/" + ip;
            string reply;
            try
            {
                using (WebClient client = new WebClient())
                {
                    System.Net.ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 |
                    SecurityProtocolType.Tls11 |
                    SecurityProtocolType.Tls;
                    reply = client.DownloadString(req);
                }
            }
            catch (WebException) { reply = @"WebException"; }

            if (!string.IsNullOrEmpty(reply))
            {
                switch (reply)
                {
                    case @"0;;;WRONG INPUT":
                    case @"WebException":
                        result = @"failed";
                        break;
                    case @"1;ZZ;ZZZ;Reserved":
                    case @"1;CN;CHN;China":
                        result = @"in";
                        break;
                    default:
                        result = @"out";
                        break;
                }
            }
            return result;
        }

        private static bool CheckVersion(string firstMsg, Tunnel tunnel)
        {
            bool result;
            string[] args = firstMsg.Split(' ');
            if (args.Length >= 3)
            {
                string reply = "";
                result = args[0] == "eagle_tunnel";
                reply = result ? "valid" : "invalid";
                bool valid1 = args[1] == Server.ProtocolVersion;
                result &= valid1;
                reply += valid1 ? " valid" : " invalid";
                valid1 = args[2] == "simple";
                result &= valid1;
                reply += valid1 ? " valid" : " invalid";
                if (result)
                {
                    result = tunnel.WriteL(reply);
                    if (result)
                    {
                        tunnel.EncryptL = true;
                    }
                }
            }
            else
            {
                result = false;
            }
            return result;
        }

        private static EagleTunnelUser CheckAuthen(Tunnel tunnel)
        {
            EagleTunnelUser result = null;
            if (Conf.allConf.ContainsKey("user-check") && Conf.allConf["user-check"][0] == "on")
            {
                byte[] buffer = new byte[100];
                string req = tunnel.ReadStringL();
                if (!string.IsNullOrEmpty(req))
                {
                    if (EagleTunnelUser.TryParse(req, out EagleTunnelUser user, false))
                    {
                        result = EagleTunnelUser.Check(user.ID, user.Password);
                    }
                }
                string reply = result != null ? "valid" : "invalid";
                result = tunnel.WriteL(reply) ? result : null;
            }
            else
            {
                if (EagleTunnelUser.users.ContainsKey("anonymous"))
                {
                    result = EagleTunnelUser.users["anonymous"];
                }
            }
            return result;
        }

        private static EagleTunnelRequestType GetType(string msg)
        {
            EagleTunnelRequestType result = EagleTunnelRequestType.Unknown;
            string[] args = msg.Split(' ');
            if (!System.Enum.TryParse(args[0], out result))
            {
                result = EagleTunnelRequestType.Unknown;
            }
            return result;
        }

        private static void HandleDNSReq(string msg, Tunnel tunnel)
        {
            if (!string.IsNullOrEmpty(msg) && tunnel != null)
            {
                string[] args = msg.Split(' ');
                if (args.Length >= 2)
                {
                    string domain = args[1];
                    IPAddress ip;
                    if (dnsCaches.ContainsKey(domain))
                    {
                        if (!dnsCaches[domain].IsDead)
                        {
                            ip = dnsCaches[domain].IP;
                        }
                        else
                        {
                            ip = ResolvDNS(domain);
                            if (ip != null)
                            {
                                dnsCaches[domain].IP = ip;
                            }
                        }
                    }
                    else
                    {
                        ip = ResolvDNS(domain);
                        if (ip != null)
                        {
                            DnsCache cache = new DnsCache(domain, ip, Conf.DnsCacheTtl);
                            dnsCaches.TryAdd(cache.Domain, cache);
                        }
                    }
                    string reply;
                    if (ip == null)
                    {
                        reply = "nok";
                    }
                    else
                    {
                        reply = ip.ToString();
                    }
                    tunnel.WriteL(reply);
                }
                tunnel.Close();
            }
        }

        private static IPAddress ResolvDNS(string url)
        {
            IPAddress result = null;
            IPHostEntry iphe;
            if (!string.IsNullOrEmpty(url))
            {
                try
                {
                    iphe = Dns.GetHostEntry(url);
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
            }
            return result;
        }

        private static bool TCPReqHandle(string msg, Tunnel tunnel)
        {
            bool result = false;
            if (msg != null && tunnel != null)
            {
                string[] args = msg.Split(' ');
                if (args.Length >= 3)
                {
                    string ip = args[1];
                    string _port = args[2];
                    if (int.TryParse(_port, out int port))
                    {
                        if (IPAddress.TryParse(ip, out IPAddress ipa))
                        {
                            IPEndPoint ipeReq = new IPEndPoint(ipa, port);
                            Socket socket2Server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                            try
                            {
                                socket2Server.Connect(ipeReq);
                                result = true;
                            }
                            catch {; }
                            if (result)
                            {
                                tunnel.SocketR = socket2Server;
                                result = tunnel.WriteL("ok");
                            }
                            else
                            {
                                tunnel.WriteL("nok");
                            }
                        }
                    }
                }
            }
            return result;
        }
    }
}
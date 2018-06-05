using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading;

namespace eagle.tunnel.dotnet.core {
    public class EagleTunnelArgs {
        public EagleTunnelUser User { get; set; }
        private string domain;
        public string Domain {
            get {
                return domain;
            }
            set {
                domain = value;
                switch (Conf.Status) {
                    case Conf.ProxyStatus.DISABLE:
                        EnableProxy = false;
                        break;
                    case Conf.ProxyStatus.SMART:
                        EnableProxy = CheckEnableProxy (domain);
                        break;
                    default:
                        EnableProxy = true;
                        break;
                }
            }
        }
        public IPAddress IP { get; set; }
        public bool EnableProxy { get; private set; }
        private IPEndPoint endPoint;
        public IPEndPoint EndPoint {
            get {
                return endPoint;
            }
            set {
                endPoint = value;
                if (endPoint != null) {
                    switch (Conf.Status) {
                        case Conf.ProxyStatus.DISABLE:
                            EnableProxy = false;
                            break;
                        case Conf.ProxyStatus.SMART:
                            EnableProxy = CheckEnableProxy (endPoint.Address);
                            break;
                        default:
                            EnableProxy = true;
                            break;
                    }
                }
            }
        }

        private static bool CheckEnableProxy (string domain) {
            bool result = false;
            if (!string.IsNullOrEmpty (domain)) {
                foreach (string item in Conf.whitelist_domain) {
                    if (domain.IndexOf (item) >= 0) {
                        result = true;
                        break;
                    }
                }
            }
            return result;
        }

        private static bool CheckEnableProxy (IPAddress ip) {
            bool result = true;
            if (ip != null) {
                string _ip = ip.ToString ();
                bool inside = Conf.ContainsBlackIP (_ip);
                bool outside = Conf.ContainsWhiteIP (_ip);
                if (!inside && !outside) {
                    if (insideCache.ContainsKey (_ip)) {
                        inside = insideCache[_ip];
                        outside = !inside;
                        if (inside) {
                            Conf.NewBlackIP = _ip;
                        }
                        if (outside) {
                            Conf.NewWhitelistIP = _ip;
                        }
                    } else {
                        ip2Resolv.Enqueue (_ip);
                        outside = !inside;
                    }
                }
                result = (!inside) || outside;
            }
            return result;
        }

        private static string CheckIfInside (string ip) {
            string result = "failed";
            string req = @"https://ip2c.org/" + ip;
            string reply = "";
            HttpWebRequest httpReq = HttpWebRequest.CreateHttp (req);
            try {
                using (HttpWebResponse httpRes = httpReq.GetResponse () as HttpWebResponse) {
                    using (System.IO.Stream stream = httpRes.GetResponseStream ()) {
                        byte[] buffer = new byte[1024];
                        int bytes = stream.Read (buffer, 0, buffer.Length);
                        reply = Encoding.UTF8.GetString (buffer, 0, bytes);
                    }
                }
            } catch {; }
            if (!string.IsNullOrEmpty (reply)) {
                if (reply == @"1;CN;CHN;China") {
                    result = "in";
                } else if (reply == @"1;ZZ;ZZZ;Reserved") {
                    result = "in";
                } else {
                    result = "out";
                }
            }
            return result;
        }

        public static ConcurrentDictionary<string, bool> insideCache =
            new ConcurrentDictionary<string, bool> ();

        private static ConcurrentQueue<string> ip2Resolv =
            new ConcurrentQueue<string> ();

        private static bool IsRunning;
        private static int time2Wait = 10000;
        private static void HandleIp2Resolve () {
            while (IsRunning) {
                while (ip2Resolv.Count > 0) {
                    if (ip2Resolv.TryDequeue (out string ip)) {
                        if (!insideCache.ContainsKey (ip)) {
                            string result = CheckIfInside (ip);
                            switch (result) {
                                case "in":
                                    insideCache.TryAdd (ip, true);
                                    break;
                                case "out":
                                    insideCache.TryAdd (ip, false);
                                    break;
                                case "failed":
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }
                Thread.Sleep (time2Wait);
            }
        }

        public static void StartResolvInside () {
            IsRunning = true;
            Thread thread_Resolv = new Thread (HandleIp2Resolve);
            thread_Resolv.IsBackground = true;
            thread_Resolv.Start ();
        }

        public static void DisposeAll () {
            IsRunning = false;
            Thread.Sleep (time2Wait * 2 + 100);
            insideCache.Clear ();
        }
    }
}
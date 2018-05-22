using System.Collections.Concurrent;
using System.Net;
using System.Threading;

namespace eagle.tunnel.dotnet.core {
    public class EagleTunnelArgs {
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

        private static bool CheckIfInside (string ip) {
            bool result = false;
            string req = @"https://ip2c.org/" + ip;
            string reply = "";
            using (WebClient client = new WebClient ()) {
                try {
                    reply = client.DownloadString (req);
                } catch (WebException) {; }
                if (!string.IsNullOrEmpty (reply)) {
                    if (reply == @"1;CN;CHN;China") {
                        result = true;
                    }
                }
            };
            return result;
        }

        public static ConcurrentDictionary<string, bool> insideCache =
            new ConcurrentDictionary<string, bool> ();

        private static ConcurrentQueue<string> ip2Resolv =
            new ConcurrentQueue<string> ();

        private static bool IsRunning;
        private static int time2Wait = 1000;
        private static int maxTime2Wait = 10000;
        private static void HandleIp2Resolve () {
            while (IsRunning) {
                while (ip2Resolv.Count > 0) {
                    if (ip2Resolv.TryDequeue (out string ip)) {
                        if (!insideCache.ContainsKey (ip)) {
                            bool result = CheckIfInside (ip);
                            insideCache.TryAdd (ip, result);
                            time2Wait = 1000;
                        }
                    } else {
                        time2Wait += 1000;
                        if (time2Wait > maxTime2Wait) {
                            time2Wait = maxTime2Wait;
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
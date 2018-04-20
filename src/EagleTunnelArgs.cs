using System.Net;

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
            foreach (string item in Conf.whitelist_domain) {
                if (domain.IndexOf (item) >= 0) {
                    result = true;
                    break;
                }
            }
            return result;
        }

        private static bool CheckEnableProxy (IPAddress ip) {
            string _ip = ip.ToString ();
            bool result = Conf.ContainsWhiteIP (_ip);
            if (!result) {
                result = CheckIPLocation (ip);
                if (result) {
                    Conf.NewWhitelistIP = _ip;
                }
            }
            return result;
        }

        private static bool CheckIPLocation (IPAddress ip) {
            bool result = false;
            string req = @"https://ip2c.org/" + ip.ToString ();
            string reply = "";
            WebClient client = new WebClient ();
            try {
                reply = client.DownloadString (req);
            } catch (WebException) {; }
            if (!string.IsNullOrEmpty (reply)) {
                if (reply != @"1;CN;CHN;China" &&
                    reply != @"1;ZZ;ZZZ;Reserved") {
                    result = true;
                }
            }
            return result;
        }
    }
}
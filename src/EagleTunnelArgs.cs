using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading;

namespace eagle.tunnel.dotnet.core
{
    public class EagleTunnelArgs
    {
        public EagleTunnelUser User { get; set; }
        private string domain;
        public string Domain
        {
            get
            {
                return domain;
            }
            set
            {
                domain = value;
                switch (Conf.Status)
                {
                    case Conf.ProxyStatus.DISABLE:
                        EnableProxy = false;
                        break;
                    case Conf.ProxyStatus.SMART:
                        EnableProxy = CheckEnableProxy(domain);
                        break;
                    default:
                        EnableProxy = true;
                        break;
                }
            }
        }
        public IPAddress IP { get; set; }
        public bool Success { get; set; }
        public bool EnableProxy { get; set; }
        private IPEndPoint endPoint;
        public IPEndPoint EndPoint
        {
            get
            {
                return endPoint;
            }
            set
            {
                endPoint = value;
                if (endPoint != null)
                {
                    switch (Conf.Status)
                    {
                        case Conf.ProxyStatus.DISABLE:
                            EnableProxy = false;
                            break;
                        case Conf.ProxyStatus.SMART:
                            EnableProxy = CheckEnableProxy(endPoint.Address);
                            break;
                        default:
                            EnableProxy = true;
                            break;
                    }
                }
            }
        }

        public EagleTunnelArgs()
        {
            Success = false;
        }

        private static bool CheckEnableProxy(string domain)
        {
            bool result = false;
            if (!string.IsNullOrEmpty(domain))
            {
                foreach (string item in Conf.whitelist_domain)
                {
                    int ind = domain.LastIndexOf(item);
                    if (ind >= 0)
                    {
                        if ((ind + item.Length) == domain.Length)
                        {
                            result = true;
                            break;
                        }
                    }
                }
            }
            return result;
        }

        private static bool CheckEnableProxy(IPAddress ip)
        {
            bool result = true;
            if (ip != null)
            {
                EagleTunnelArgs e = new EagleTunnelArgs();
                e.IP = ip;
                EagleTunnelSender.Handle(
                    EagleTunnelHandler.EagleTunnelRequestType.LOCATION,
                    e
                );
                if (e.Success)
                {
                    result = e.EnableProxy;
                }
            }
            return result;
        }
    }
}
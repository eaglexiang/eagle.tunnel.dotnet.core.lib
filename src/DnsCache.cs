using System;
using System.Net;

namespace eagle.tunnel.dotnet.core {
    public class DnsCache {
        public string Domain { get; set; }
        private IPAddress ip;
        public IPAddress IP {
            get {
                return ip;
            }
            set {
                ip = value;
                TimeModified = DateTime.Now;
            }
        }
        public DateTime TimeModified { get; private set; }
        public int TTI { get; set; }
        public bool IsDead {
            get {
                double seconds = (DateTime.Now - TimeModified).TotalSeconds;
                return seconds >= TTI && IP != null;
            }
        }

        public DnsCache (string domain, IPAddress ip, int tti) {
            Domain = domain;
            IP = ip;
            TTI = tti;
        }
    }
}
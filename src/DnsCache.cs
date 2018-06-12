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
        public int TTL { get; set; }
        public bool IsDead {
            get {
                double seconds = (DateTime.Now - TimeModified).TotalSeconds;
                return seconds >= TTL && IP != null;
            }
        }

        public DnsCache (string domain, IPAddress ip, int ttl) {
            Domain = domain;
            IP = ip;
            TTL = ttl;
        }
    }
}
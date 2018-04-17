using System;
using System.Net;

namespace eagle.tunnel.dotnet.core {
    public class DnsCache {
        public string Domain { get; set; }
        public IPAddress IP { get; set; }
        public DateTime TimeCreated { get; private set; }
        public int TTI { get; set; }
        public bool IsDead {
            get {
                double seconds = (DateTime.Now - TimeCreated).TotalSeconds;
                return seconds >= TTI;
            }
        }

        public DnsCache (string domain, IPAddress ip, int tti) {
            Domain = domain;
            IP = ip;
            TimeCreated = DateTime.Now;
            TTI = tti;
        }
    }
}
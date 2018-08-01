using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace eagle.tunnel.dotnet.core {
    public class Conf {
        public enum ProxyStatus {
            DISABLE,
            ENABLE,
            SMART
        }
        public static ArrayList whitelist_domain;
        private static ArrayList whitelist_ip;
        private static ArrayList blacklist_ip;
        public static ConcurrentDictionary<string, IPAddress> hosts;
        private static string confFilePath;
        public static int PipeTimeOut;
        private static EagleTunnelUser localUser;
        public static EagleTunnelUser LocalUser {
            get {
                return localUser;
            }
            set {
                localUser = value;
                allConf.TryAdd ("user", new List<string> ());
                if (allConf["user"].Count == 0) {
                    allConf["user"].Add ("");
                }
                allConf["user"][0] = localUser.ToString ();
            }
        }
        private static bool enableSOCKS;
        public static bool EnableSOCKS {
            get {
                return enableSOCKS;
            }
            set {
                enableSOCKS = value;
                allConf.TryAdd ("socks", new List<string> ());
                if (allConf["socks"].Count == 0) {
                    allConf["socks"].Add ("");
                }
                allConf["socks"][0] = value? "on": "off";
            }
        }
        private static bool enableHTTP;
        public static bool EnableHTTP {
            get {
                return enableHTTP;
            }
            set {
                enableHTTP = value;
                allConf.TryAdd ("http", new List<string> ());
                if (allConf["http"].Count == 0) {
                    allConf["http"].Add ("");
                }
                allConf["http"][0] = value? "on": "off";
            }
        }
        private static bool enableEagleTunnel;
        public static bool EnableEagleTunnel {
            get {
                return enableEagleTunnel;
            }
            set {
                enableEagleTunnel = value;
                allConf.TryAdd ("et", new List<string> ());
                if (allConf["et"].Count == 0) {
                    allConf["et"].Add ("");
                }
                allConf["et"][0] = value? "on": "off";
            }
        }
        private static ProxyStatus proxyStatus;
        public static ProxyStatus Status {
            get {
                return proxyStatus;
            }
            set {
                proxyStatus = value;
                allConf.TryAdd ("proxy-status", new List<string> ());
                if (allConf["proxy-status"].Count == 0) {
                    allConf["proxy-status"].Add ("");
                }
                allConf["proxy-status"][0] = value.ToString ();
            }
        }
        public static ConcurrentDictionary<string, List<string>> allConf;
        public static int maxClientsCount;
        private static IPEndPoint[] localAddresses;
        public static IPEndPoint[] LocalAddresses {
            get {
                return localAddresses;
            }
            set {
                localAddresses = value;
                if (!allConf.TryAdd ("listen", new List<string> ())) {
                    allConf["listen"] = new List<string> ();
                }
                foreach (IPEndPoint item in value) {
                    allConf["listen"].Add (item.ToString ());
                }
            }
        }
        private static IPEndPoint[] remoteAddresses;
        public static IPEndPoint[] RemoteAddresses {
            get {
                return remoteAddresses;
            }
            set {
                remoteAddresses = value;
                if (!allConf.TryAdd ("relayer", new List<string> ())) {
                    allConf["relayer"] = new List<string> ();
                }
                foreach (IPEndPoint item in value) {
                    allConf["relayer"].Add (item.ToString ());
                }
            }
        }
        public static int DnsCacheTti { get; set; } = 60; // default 60s

        private static object lockOfIndex;

        public static bool LocalAddress_Set (string address) {
            bool result = false;
            if (address != null) {
                string[] args = address.Split (':');
                if (args.Length >= 1) {
                    if (IPAddress.TryParse (args[0], out IPAddress ipa)) {
                        int _port = 8080;
                        if (args.Length >= 2) {
                            if (int.TryParse (args[1], out int port)) {
                                _port = port;
                            }
                        }
                        IPEndPoint[] tmpList = new IPEndPoint[1];
                        tmpList[0] = new IPEndPoint (ipa, _port);
                        LocalAddresses = tmpList;
                        result = true;
                    }
                }
            }
            return result;
        }

        public static bool RemoteAddress_Set (string address) {
            bool result = false;
            if (!string.IsNullOrEmpty (address)) {
                string[] args = address.Split (':');
                if (args.Length >= 1) {
                    if (IPAddress.TryParse (args[0], out IPAddress ipa)) {
                        int _port = 8080;
                        if (args.Length >= 2) {
                            if (int.TryParse (args[1], out int port)) {
                                _port = port;
                            }
                        }
                        IPEndPoint[] tmpList = new IPEndPoint[1];
                        tmpList[0] = new IPEndPoint (ipa, _port);
                        RemoteAddresses = tmpList;
                        result = true;
                    }
                }
            }
            return result;
        }

        private static int indexOfRemoteAddresses;
        private static int GetIndexOfRemoteAddresses () {
            int result = 0;
            if (RemoteAddresses != null) {
                if (RemoteAddresses.Length > 1) {
                    lock (lockOfIndex) {
                        indexOfRemoteAddresses += 1;
                        indexOfRemoteAddresses %= RemoteAddresses.Length;
                    }
                    result = indexOfRemoteAddresses;
                }
            }
            return result;
        }

        public static IPEndPoint GetRemoteIPEndPoint () {
            IPEndPoint result = null;
            if (RemoteAddresses != null) {
                result = RemoteAddresses[GetIndexOfRemoteAddresses ()];
            }
            return result;
        }

        public static void Init (string confPath) {
            allConf = new ConcurrentDictionary<string, List<string>> (StringComparer.OrdinalIgnoreCase);
            confFilePath = confPath;
            ReadAll ();

            ImportUsers ();
            Console.WriteLine ("find user(s): {0}", EagleTunnelUser.users.Count);

            if (allConf.ContainsKey ("user")) {
                if (EagleTunnelUser.TryParse (allConf["user"][0], out EagleTunnelUser user, true)) {
                    localUser = user;
                }
            }
            if (LocalUser != null) {
                Console.WriteLine ("User: {0}", LocalUser.ID);
            }

            PipeTimeOut = 0;
            if (Conf.allConf.ContainsKey ("timeout")) {
                if (int.TryParse (Conf.allConf["timeout"][0], out int timeout)) {
                    PipeTimeOut = timeout;
                }
            }
            Console.WriteLine ("TimeOut(ms): {0} (0 means infinite timeout period.)", PipeTimeOut);

            maxClientsCount = 500;
            if (allConf.ContainsKey ("worker")) {
                if (int.TryParse (allConf["worker"][0], out int workerCount)) {
                    maxClientsCount = workerCount;
                }
            }
            Console.WriteLine ("worker: {0}", maxClientsCount);

            try {
                List<string> remoteAddressStrs = Conf.allConf["relayer"];
                remoteAddresses = CreateEndPoints (remoteAddressStrs);
            } catch (KeyNotFoundException) {
                Console.WriteLine ("Warning: Relayer not found.");
            }
            if (RemoteAddresses != null) {
                Console.WriteLine ("Count of Relayer: {0}", RemoteAddresses.Length);
            }

            try {
                List<string> localAddressStrs = Conf.allConf["listen"];
                localAddresses = CreateEndPoints (localAddressStrs);
                lockOfIndex = new object ();

            } catch (KeyNotFoundException) {
                Console.WriteLine ("Warning: Listen not found");
            }

            if (allConf.ContainsKey ("socks")) {
                if (allConf["socks"][0] == "on") {
                    enableSOCKS = true;
                }
            }
            Console.WriteLine ("SOCKS Switch: {0}", EnableSOCKS.ToString ());

            if (allConf.ContainsKey ("http")) {
                if (allConf["http"][0] == "on") {
                    enableHTTP = true;
                }
            }
            Console.WriteLine ("HTTP Switch: {0}", EnableHTTP.ToString ());

            if (allConf.ContainsKey ("et")) {
                if (allConf["et"][0] == "on") {
                    enableEagleTunnel = true;
                }
            }
            Console.WriteLine ("Eagle Tunnel Switch: {0}", EnableEagleTunnel.ToString ());

            if (allConf.ContainsKey ("proxy-status")) {
                proxyStatus = (ProxyStatus) Enum.Parse (typeof (Conf.ProxyStatus),
                    allConf["proxy-status"][0].ToUpper ());
            } else {
                proxyStatus = ProxyStatus.ENABLE; // default enable proxy
            }
            Console.WriteLine ("Proxy Status: {0}", proxyStatus.ToString ());
            ImportList ("whitelist_domain.txt", out whitelist_domain);
            ImportList ("whitelist_ip.txt", out whitelist_ip);
            ImportList ("blacklist_ip.txt", out blacklist_ip);
            ImportHosts ("hosts", out hosts);
        }

        private static bool ImportList (string filename, out ArrayList list, string path = "") {
            bool result = false;
            if (path == "") {
                if (allConf.ContainsKey ("config-dir")) {
                    path = allConf["config-dir"][0] + Path.DirectorySeparatorChar;

                } else {
                    string dir = Path.GetDirectoryName (confFilePath);
                    path = dir + Path.DirectorySeparatorChar;
                }
                path += filename;
            }

            string[] lines;
            if (File.Exists (path)) {
                lines = File.ReadAllLines (path, System.Text.Encoding.UTF8);
                for (int i = 0; i < lines.Length; ++i) {
                    int indexOfSharp = lines[i].IndexOf ('#');
                    if (indexOfSharp >= 0) {
                        lines[i] = lines[i].Substring (0, indexOfSharp); // remote note
                    }
                    lines[i] = lines[i].Trim ();
                }
            } else {
                lines = null;
            }
            list = new ArrayList ();
            if (lines != null) {
                list.AddRange (lines);
                result = true;
            }
            return result;
        }

        private static IPEndPoint[] CreateEndPoints (List<string> addresses) {
            ArrayList list = new ArrayList ();
            foreach (string address in addresses) {
                string[] endpoints = address.Split (':');
                if (endpoints.Length >= 1) {
                    if (IPAddress.TryParse (endpoints[0], out IPAddress ipa)) {
                        int port;
                        if (endpoints.Length >= 2) {
                            port = int.Parse (endpoints[1]);
                        } else {
                            port = 8080;
                        }
                        IPEndPoint ipep = new IPEndPoint (ipa, port);
                        list.Add (ipep);
                    }
                }
            }
            return list.ToArray (typeof (IPEndPoint)) as IPEndPoint[];
        }

        private static void ImportUsers () {
            if (allConf.ContainsKey ("user-check") &&
                allConf["user-check"][0] == "on") {
                ImportList ("users.list", out ArrayList users);
                for (int i = 0; i < users.Count; ++i) {
                    string line = users[i] as string;
                    EagleTunnelUser.TryAdd (line);
                }
            } else {
                EagleTunnelUser.TryAdd ("anoymous:anoymous");
            }
        }

        /// <summary>
        /// Read all configurations from file
        /// </summary>
        /// <param name="confPath">path of conf file</param>
        private static void ReadAll () {
            ImportList ("eagle-tunnel.conf", out ArrayList confs, confFilePath);
            foreach (string line in confs) {
                string[] args = line.Split ('=');
                if (args.Length == 2) {
                    string key = args[0].Trim ();
                    string value = args[1].Trim ();
                    allConf.TryAdd (key, new List<string> ());
                    allConf[key].Add (value);
                }
            }
        }

        private static bool ImportHosts (string filename,
            out ConcurrentDictionary<string, IPAddress> hosts) {
            bool result = false;
            hosts = new ConcurrentDictionary<string, IPAddress> ();
            if (ImportList (filename, out ArrayList list)) {
                for (int i = 0; i < list.Count; ++i) {
                    string line = list[i] as string;
                    string[] arr = line.Trim ().Split (new char[] { ' ' },
                        StringSplitOptions.RemoveEmptyEntries);
                    line = string.Join ("\t", arr);
                    arr = line.Trim ().Split (new char[] { '\t' },
                        StringSplitOptions.RemoveEmptyEntries);
                    if (arr.Length == 2) {
                        if (IPAddress.TryParse (arr[0], out IPAddress ip)) {
                            hosts.TryAdd (arr[1], ip);
                        }
                    }
                }
                result = true;
            }
            return result;
        }

        public static void Save () {
            string allConf = ToString ();
            File.WriteAllText (confFilePath, allConf);
        }

        public static new string ToString () {
            string result = "";
            foreach (string key in allConf.Keys) {
                foreach (string value in allConf[key]) {
                    result += (key + '=' + value + Environment.NewLine);
                }
            }
            return result;
        }
    }
}
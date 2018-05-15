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
        public static bool ContainsWhiteIP (string ip) {
            return whitelist_ip.Contains (ip);
        }
        public static string NewWhitelistIP {
            set {
                if (!whitelist_ip.Contains (value)) {
                    lock (whitelist_ip) {
                        whitelist_ip.Add (value);
                        string path = allConf["config dir"][0] + "whitelist_ip.txt";
                        File.AppendAllText (path, value + '\n');
                    }
                }
            }
        }
        public static bool ContainsBlackIP (string ip) {
            return blacklist_ip.Contains (ip);
        }
        public static string NewBlackIP {
            set {
                if (!blacklist_ip.Contains (value)) {
                    lock (blacklist_ip) {
                        blacklist_ip.Add (value);
                        string path = allConf["config dir"][0] + "blacklist_ip.txt";
                        File.AppendAllText (path, value + '\n');
                    }
                }
            }
        }
        private static string confFilePath;
        private static EagleTunnelUser localUser;
        public static EagleTunnelUser LocalUser {
            get {
                return localUser;
            }
            set {
                localUser = value;
                Dirty = true;
                if (allConf.ContainsKey ("user")) {
                    allConf["user"][0] = localUser.ToString ();
                } else {
                    if (allConf.TryAdd ("user", new List<string> ())) {
                        allConf["user"][0] = localUser.ToString ();
                    }
                }
            }
        }
        private static bool enableSOCKS;
        public static bool EnableSOCKS {
            get {
                return enableSOCKS;
            }
            set {
                enableSOCKS = value;
                Dirty = true;
                if (allConf.ContainsKey ("socks")) {
                    allConf["socks"][0] = value? "on": "off";
                } else {
                    if (allConf.TryAdd ("socks", new List<string> ())) {
                        allConf["socks"][0] = value? "on": "off";
                    }
                }
            }
        }
        private static bool enableHTTP;
        public static bool EnableHTTP {
            get {
                return enableHTTP;
            }
            set {
                enableHTTP = value;
                Dirty = true;
                if (allConf.ContainsKey ("http")) {
                    allConf["http"][0] = value? "on": "off";
                } else {
                    if (allConf.TryAdd ("http", new List<string> ())) {
                        allConf["http"][0] = value? "on": "off";
                    }
                }
            }
        }
        private static bool enableEagleTunnel;
        public static bool EnableEagleTunnel {
            get {
                return enableEagleTunnel;
            }
            set {
                enableEagleTunnel = value;
                Dirty = true;
                if (allConf.ContainsKey ("eagle tunnel")) {
                    allConf["eagle tunnel"][0] = value? "on": "off";
                } else {
                    if (allConf.TryAdd ("eagle tunnel", new List<string> ())) {
                        allConf["eagle tunnel"][0] = value? "on": "off";
                    }
                }
            }
        }
        private static ProxyStatus proxyStatus;
        public static ProxyStatus Status {
            get {
                return proxyStatus;
            }
            set {
                proxyStatus = value;
                Dirty = true;
                if (allConf.ContainsKey ("proxy status")) {
                    allConf["proxy status"][0] = value.ToString ();
                } else {
                    if (allConf.TryAdd ("proxy status", new List<string> ())) {
                        allConf["proxy status"][0] = value.ToString ();
                    }
                }
            }
        }
        public static ConcurrentDictionary<string, List<string>> allConf;
        public static ConcurrentDictionary<string, EagleTunnelUser> Users;
        public static int maxClientsCount;
        private static IPEndPoint[] localAddresses;
        public static IPEndPoint[] LocalAddresses {
            get {
                return localAddresses;
            }
            set {
                localAddresses = value;
                Dirty = true;
                allConf["listen"] = new List<string>();
                foreach (IPEndPoint item in value)
                {
                    allConf["listen"].Add(item.ToString());
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
                Dirty = true;
                allConf["relayer"] = new List<string>();
                foreach (IPEndPoint item in value)
                {
                    allConf["relayer"].Add(item.ToString());
                }
            }
        }
        public static int DnsCacheTti { get; set; } = 600; // default 10 m

        private static object lockOfIndex;
        private static bool Dirty { get; set; }

        public static bool LocalAddress_Set (string address) {
            bool result = false;
            if (address != null) {
                string[] args = address.Split (':');
                if (args.Length == 2) {
                    if (IPAddress.TryParse (args[0], out IPAddress ipa)) {
                        if (int.TryParse (args[1], out int port)) {
                            LocalAddresses = new IPEndPoint[1];
                            LocalAddresses[0] = new IPEndPoint (ipa, port);
                            result = true;
                        }
                    }
                }
            }
            return result;
        }

        public static bool RemoteAddress_Set (string address) {
            bool result = false;
            if (!string.IsNullOrEmpty (address)) {
                string[] args = address.Split (':');
                if (args.Length == 2) {
                    if (IPAddress.TryParse (args[0], out IPAddress ipa)) {
                        if (int.TryParse (args[1], out int port)) {
                            RemoteAddresses = new IPEndPoint[1];
                            RemoteAddresses[0] = new IPEndPoint (ipa, port);
                            result = true;
                        }
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
            Dirty = false;
            allConf = new ConcurrentDictionary<string, List<string>> (StringComparer.OrdinalIgnoreCase);
            confFilePath = confPath;
            ReadAll ();

            ImportUsers ();
            Console.WriteLine ("find user(s): {0}", Users.Count);

            if (allConf.ContainsKey ("user")) {
                if (EagleTunnelUser.TryParse (allConf["user"][0], out EagleTunnelUser user)) {
                    localUser = user;
                }
            }
            if (LocalUser != null) {
                Console.WriteLine ("User: {0}", LocalUser.ID);
            }

            maxClientsCount = 200;
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

            if (allConf.ContainsKey ("eagle tunnel")) {
                if (allConf["eagle tunnel"][0] == "on") {
                    enableEagleTunnel = true;
                }
            }
            Console.WriteLine ("Eagle Tunnel Switch: {0}", EnableEagleTunnel.ToString ());

            if (allConf.ContainsKey ("proxy status")) {
                proxyStatus = (ProxyStatus) Enum.Parse (typeof (Conf.ProxyStatus),
                    allConf["proxy status"][0].ToUpper ());
                if (Status == ProxyStatus.SMART) {
                    ImportList ("whitelist_domain.txt", out whitelist_domain);
                    ImportList ("whitelist_ip.txt", out whitelist_ip);
                    ImportList ("blacklist_ip.txt", out blacklist_ip);
                }
            } else {
                Status = ProxyStatus.ENABLE; // default enable proxy
            }
            Console.WriteLine ("Proxy Status: {0}", Status.ToString ());
        }

        private static void ImportWhiteList () {

        }
        private static void ImportList (string filename, out ArrayList list, string path = "") {
            if (path == "") {
                path = allConf["config dir"][0] + '/';
                path += filename;
            }

            string[] lines = File.ReadAllLines (path, System.Text.Encoding.UTF8);
            for (int i = 0; i < lines.Length; ++i) {
                int indexOfSharp = lines[i].IndexOf ('#');
                if (indexOfSharp >= 0) {
                    lines[i] = lines[i].Substring (0, indexOfSharp); // remote note
                }
                lines[i] = lines[i].Trim ();
            }
            list = new ArrayList ();
            list.AddRange (lines);
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
            Users = new ConcurrentDictionary<string, EagleTunnelUser> ();
            Users.TryAdd ("anonymous", new EagleTunnelUser ("anonymous", "anonymous"));
            if (allConf.ContainsKey ("user-check") &&
                allConf["user-check"][0] == "on") {
                ImportList ("users.list", out ArrayList users);
                for (int i = 0; i < users.Count; ++i) {
                    string line = users[i] as string;
                    if (EagleTunnelUser.TryParse (line, out EagleTunnelUser user)) {
                        Users.TryAdd (user.ID, user);
                    }
                }
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

        public static void Save () {
            if (Dirty) {
                string allConf = ToString ();
                File.WriteAllText (confFilePath, allConf);
            }
        }

        public static new string ToString () {
            string result = "";
            foreach (string key in allConf.Keys) {
                foreach (string value in allConf[key]) {
                    result += (key + '=' + value + '\n');
                }
            }
            return result;
        }
    }
}
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace eagle.tunnel.dotnet.core {
    public class Server {
        public static string Version { get; } = "1.13.4";
        public static string ProtocolVersion { get; } = "1.1";
        private static ConcurrentQueue<Tunnel> clients;
        private static Socket[] servers;
        private static IPEndPoint[] localAddresses;
        private static Thread threadLimitCheck;
        private const int maxReqGotNumber = 100;
        private static ConCurrentCounter reqGotNumbers;
        private static bool IsRunning { get; set; } // Server will keep running.
        // Server has started working.
        public static bool IsWorking {
            get {
                bool result = false;
                if (IsRunning) {
                    if (servers != null) {
                        foreach (Socket server in servers) {
                            if (server == null) {
                                continue;
                            } else {
                                result |= server.IsBound;
                            }
                        }
                    }
                }
                return result;
            }
        }

        public static void StartAsync (IPEndPoint[] localAddresses) {
            Thread thread = new Thread (() => Start (localAddresses));
            thread.IsBackground = true;
            thread.Start ();
        }

        public static void Start (IPEndPoint[] localAddresses) {
            if (!IsRunning) {
                if (localAddresses != null) {
                    if (Conf.allConf.ContainsKey ("proxy-status")) {
                        if (Conf.allConf["proxy-status"][0] == "smart") {
                            EagleTunnelHandler.StartResolvInside ();
                        }
                    }
                    if (Conf.allConf.ContainsKey ("et")) {
                        if (Conf.allConf["et"][0] == "on") {
                            EagleTunnelHandler.StartResolvInside ();
                        }
                    }
                    if (Conf.allConf.ContainsKey ("http")) {
                        if (Conf.allConf["http"][0] == "on") {
                            EagleTunnelSender.OpenTunnelPool ();
                        }
                    }
                    if (Conf.allConf.ContainsKey ("socks")) {
                        if (Conf.allConf["socks"][0] == "on") {
                            EagleTunnelSender.OpenTunnelPool ();
                        }
                    }

                    clients = new ConcurrentQueue<Tunnel> ();
                    servers = new Socket[localAddresses.Length];
                    reqGotNumbers = new ConCurrentCounter ();
                    Server.localAddresses = localAddresses;
                    IsRunning = true;

                    if (threadLimitCheck == null) {
                        threadLimitCheck = new Thread (LimitSpeed);
                        threadLimitCheck.IsBackground = true;
                        threadLimitCheck.Start ();
                    }

                    for (int i = 1; i < localAddresses.Length; ++i) {
                        ListenAsync (i);
                    }
                    Listen (0);
                }
            }
        }

        private static Socket CreateServer (IPEndPoint ipep) {
            Socket server = null;
            if (ipep != null) {
                server = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                bool firstTime = true;
                do {
                    try {
                        server.Bind (ipep);
                    } catch (SocketException se) {
                        if (firstTime) {
                            Console.WriteLine ("warning: bind {0} -> {1}", ipep.ToString (), se.Message);
                            Console.WriteLine ("retrying...");
                            firstTime = false;
                        }
                        Thread.Sleep (20000); // wait for 20s to retry.
                    }
                } while (!server.IsBound && IsRunning);
            }
            return server;
        }

        private static void ListenAsync (int ipepIndex) {
            Thread thread = new Thread (() => Listen (ipepIndex));
            thread.IsBackground = true;
            thread.Start ();
        }

        private static void Listen (int ipepIndex) {
            IPEndPoint ipep = localAddresses[ipepIndex];
            Socket server = CreateServer (ipep);
            if (server != null) {
                // init lists
                servers[ipepIndex] = server;
                // listen
                server.Listen (100);
                Console.WriteLine ("start to Listen: {0}",
                    server.LocalEndPoint.ToString ());
                // socket connections handle
                while (IsRunning) {
                    if (reqGotNumbers.Value >= maxReqGotNumber) {
                        if(clients.TryDequeue(out Tunnel tunnel2Close)){
                            tunnel2Close.Close();
                            reqGotNumbers.Down();
                        }
                        Thread.Sleep (100); // wait until reqGotNumber < maxReqGotNumber
                    } else {
                        try {
                            Socket client = server.Accept ();
                            // set timeout to avoid ddos
                            client.SendTimeout = Conf.PipeTimeOut;
                            client.ReceiveTimeout = Conf.PipeTimeOut;
                            reqGotNumbers.Up ();
                            HandleClientAsync (client, ipepIndex);
                        } catch (SocketException se) {
                            Console.WriteLine ("{0}",
                                se.Message);
                            break;
                        } catch (ObjectDisposedException) {; }
                    }
                }
            } else {
                Console.WriteLine ("error: fail to create server -> {0}", ipep.ToString ());
            }
        }

        private static void HandleClientAsync (Socket socket2Client, int ipepIndex) {
            Thread threadHandleClient = new Thread (_handleClient);
            threadHandleClient.IsBackground = true;
            object[] args = new object[2] { socket2Client, ipepIndex };
            threadHandleClient.Start (args);
        }

        private static void _handleClient (object argsObj) {
            object[] args = argsObj as object[];
            Socket socket2Client = args[0] as Socket;
            int ipepIndex = (int) args[1];
            Tunnel tunnel2Add = new Tunnel (socket2Client);

            while (clients.Count >= Conf.maxClientsCount) {
                if (clients.TryDequeue (out Tunnel tunnel2Close)) {
                    tunnel2Close.Close ();
                }
            }
            clients.Enqueue (tunnel2Add);
            bool result = RequestHandler.Handle (tunnel2Add);
            if (result) {
                tunnel2Add.Flow ();
            } else {
                tunnel2Add.Close ();
            }
            // release sources for dead connections
            if (clients.Count > Conf.maxClientsCount / 3) {
                double closing = 10;
                double closed = closing;
                while ((closed / closing) >= 0.3) {
                    closed = 0;
                    for (int i = 0; i < closing; ++i) {
                        if (clients.TryDequeue (out Tunnel tunnel2Check)) {
                            if (!tunnel2Check.IsOpening) {
                                if (tunnel2Check.IsFlowing) {
                                    clients.Enqueue (tunnel2Check);
                                } else {
                                    tunnel2Check.Close ();
                                    closed += 1;
                                }
                            } else {
                                clients.Enqueue (tunnel2Check);
                            }
                        }
                    }
                }
            }
            reqGotNumbers.Down ();
        }

        public static double Speed () {
            double speed = 0;
            foreach (EagleTunnelUser item in EagleTunnelUser.users.Values) {
                speed += item.Speed;
            }
            if (Conf.LocalUser != null) {
                speed += Conf.LocalUser.Speed;
            }
            return speed;
        }

        private static void LimitSpeed () {
            if (Conf.allConf.ContainsKey ("speed-check")) {
                if (Conf.allConf["speed-check"][0] == "on") {
                    if (Conf.allConf.ContainsKey ("speed-limit")) {
                        if (Conf.allConf["speed-limit"][0] == "on") {
                            while (IsRunning) {
                                foreach (EagleTunnelUser item in EagleTunnelUser.users.Values) {
                                    item.LimitSpeedAsync ();
                                }
                                Thread.Sleep (5000);
                            }
                        }
                    }
                }
            }
        }

        public static void Close () {
            if (IsRunning) {
                IsRunning = false;
                Thread.Sleep (1000);
                // stop listening
                lock (servers) {
                    foreach (Socket item in servers) {
                        if (item != null) {
                            try {
                                item.Close ();
                            } catch {; }
                        }
                    }
                }
                // shut down all connections
                while (clients.Count > 0) {
                    if (clients.TryDequeue (out Tunnel tunnel2Close)) {
                        tunnel2Close.Close ();
                    }
                }
                EagleTunnelHandler.StopResolvInside ();
                EagleTunnelSender.CloseTunnelPool ();
            }
        }

        public static void CloseAsync () {
            Thread thread = new Thread (Close);
            thread.IsBackground = true;
            thread.Start ();
        }
    }
}
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace eagle.tunnel.dotnet.core {
    public class Server {
        private static string version = "1.3.0";
        private static ConcurrentQueue<Tunnel> clients;
        private static Socket[] servers;
        private static IPEndPoint[] localAddresses;
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
                    clients = new ConcurrentQueue<Tunnel> ();
                    servers = new Socket[localAddresses.Length];
                    Server.localAddresses = localAddresses;
                    IsRunning = true;

                    Thread threadLimitCheck = new Thread (LimitSpeed);
                    threadLimitCheck.IsBackground = true;
                    threadLimitCheck.Start ();

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
                lock (servers) {
                    servers[ipepIndex] = server;
                }
                server.Listen (100);
                Console.WriteLine ("start to Listen: {0}",
                    server.LocalEndPoint.ToString ());
                while (IsRunning) {
                    try {
                        Socket client = server.Accept ();
                        HandleClient (client);
                    } catch (SocketException se) {
                        Console.WriteLine ("{0}",
                            se.Message);
                        break;
                    } catch (ObjectDisposedException) {;
                    } catch (Exception e) {
                        Console.WriteLine ("error: unexpected exception: {0}",
                            e.Message);
                    }
                }
            } else {
                Console.WriteLine ("error: server created: {0}", ipep.ToString ());
            }
        }

        private static void HandleClient (Socket socket2Client) {
            bool resultOfDequeue = true;
            while (clients.Count > Conf.maxClientsCount) {
                resultOfDequeue = clients.TryDequeue (out Tunnel tunnel2Close);
                tunnel2Close.Close ();
            }
            if (resultOfDequeue) {
                Thread threadHandleClient = new Thread (_handleClient);
                threadHandleClient.IsBackground = true;
                threadHandleClient.Start (socket2Client);
            } else {
                Console.WriteLine ("error: failed to dequeue before new client handled");
            }
        }

        private static void _handleClient (object socket2ClientObj) {
            Socket socket2Client = socket2ClientObj as Socket;
            byte[] buffer = new byte[1024];
            int read;
            try {
                read = socket2Client.Receive (buffer);
            } catch { read = 0; }
            if (read > 0) {
                byte[] req = new byte[read];
                Array.Copy (buffer, req, read);
                Tunnel tunnel = RequestHandler.Handle (req, socket2Client);
                if (tunnel != null) {
                    tunnel.Flow ();
                    clients.Enqueue (tunnel);
                }
            }
        }

        public static double Speed () {
            double speed = 0;
            if (Conf.Users != null) {
                foreach (EagleTunnelUser item in Conf.Users.Values) {
                    speed += item.Speed ();
                }
            }
            if (Conf.LocalUser != null) {
                speed += Conf.LocalUser.Speed ();
            }
            return speed;
        }

        private static void LimitSpeed () {
            if (Conf.allConf.ContainsKey ("speed-check")) {
                if (Conf.allConf["speed-check"][0] == "on") {
                    while (IsRunning) {
                        foreach (EagleTunnelUser item in Conf.Users.Values) {
                            item.LimitSpeedAsync();
                        }
                        Thread.Sleep (5000);
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
                    bool resultOfDequeue = clients.TryDequeue (out Tunnel tunnel2Close);
                    if (resultOfDequeue && tunnel2Close.IsWorking) {
                        tunnel2Close.Close ();
                    }
                }
            }
        }

        public static void CloseAsync () {
            Thread thread = new Thread (Close);
            thread.IsBackground = true;
            thread.Start ();
        }

        public static string Version () {
            return version;
        }
    }
}
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace eagle.tunnel.dotnet.core {
    public class Server {
        private static string version = "1.1.1";
        private static ConcurrentQueue<Tunnel> clients;
        private static Socket[] servers;
        private static bool IsRunning { get; set; } // Server will keep running.
        public static bool IsWorking { get; private set; } // Server has started working.

        public static void Start (IPEndPoint[] localAddress) {
            if (!IsRunning) {
                if (localAddress != null) {
                    clients = new ConcurrentQueue<Tunnel> ();
                    servers = new Socket[localAddress.Length];
                    IsRunning = true;

                    Thread threadLimitCheck = new Thread (LimitSpeed);
                    threadLimitCheck.IsBackground = true;
                    threadLimitCheck.Start ();

                    Socket server;
                    for (int i = 1; i < localAddress.Length; ++i) {
                        server = CreateServer (localAddress[i]);
                        if (server != null) {
                            servers[i] = server;
                            Thread thread = new Thread (Listen);
                            thread.IsBackground = true;
                            thread.Start (server);
                        } else {
                            Console.WriteLine ("Create Listen failed: {0}",
                                localAddress[i].ToString ());
                        }
                    }
                    server = CreateServer (localAddress[0]);
                    if (server != null) {
                        servers[0] = server;
                        IsWorking = true;
                        Listen (server);
                    } else {
                        Console.WriteLine ("create Listen failed: {0}",
                            localAddress[0]);
                    }
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
                            Console.WriteLine ("bind warning: {0} -> {1}", ipep.ToString (), se.Message);
                            Console.WriteLine ("retrying...");
                            firstTime = false;
                        }
                        Thread.Sleep (20000); // wait for 20s to retry.
                    }
                } while (!server.IsBound);
            }
            return server;
        }

        private static void Listen (object socket2Listen) {
            Socket server = socket2Listen as Socket;
            server.Listen(100);
            Console.WriteLine ("start to Listen: {0}",
                server.LocalEndPoint.ToString ());
            while (IsRunning) {
                try {
                    Socket client = server.Accept ();
                    HandleClient (client);
                } catch (SocketException se) {
                    Console.WriteLine ("{0} -> {1}",
                        server.LocalEndPoint.ToString (), se.Message);
                    break;
                }
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
                Console.WriteLine ("failed to dequeue before new client handled");
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
                            item.LimitSpeed ();
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
                        try {
                            item.Close ();
                        } catch {; }
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

        public static string Version () {
            return version;
        }
    }
}
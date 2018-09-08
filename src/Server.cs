using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace eagle.tunnel.dotnet.core
{
    public class Server
    {
        public static string Version { get; } = "1.17.2";
        public static string ProtocolVersion { get; } = "1.1";
        private static Socket[] servers;
        private static IPEndPoint[] localAddresses;
        private static Thread threadLimitCheck;
        private static int maxReqGotNumber = 100;
        private static ConCurrentCounter reqGotNumbers;
        private static bool IsRunning { get; set; } // Server will keep running.
        // Server has started working.
        public static bool IsWorking
        {
            get
            {
                bool result = false;
                if (IsRunning)
                {
                    if (servers != null)
                    {
                        foreach (Socket server in servers)
                        {
                            if (server == null)
                            {
                                continue;
                            }
                            else
                            {
                                result |= server.IsBound;
                            }
                        }
                    }
                }
                return result;
            }
        }

        public static void StartAsync (IPEndPoint[] localAddresses)
        {
            Task taskStart = new Task (() => Start (localAddresses));
            taskStart.Start ();
        }

        public static void Start (IPEndPoint[] localAddresses)
        {
            if (!IsRunning)
            {
                if (localAddresses != null)
                {
                    if (Conf.Status == Conf.ProxyStatus.SMART ||
                        Conf.EnableEagleTunnel)
                    {
                        EagleTunnelHandler.StartResolvInside ();
                    }

                    servers = new Socket[localAddresses.Length];
                    reqGotNumbers = new ConCurrentCounter (100);
                    Server.localAddresses = localAddresses;
                    IsRunning = true;

                    if (threadLimitCheck == null)
                    {
                        threadLimitCheck = new Thread (LimitSpeed);
                        threadLimitCheck.IsBackground = true;
                        threadLimitCheck.Start ();
                    }

                    for (int i = 1; i < localAddresses.Length; ++i)
                    {
                        ListenAsync (i);
                    }
                    Listen (0);
                }
            }
        }

        private static Socket CreateServer (IPEndPoint ipep)
        {
            Socket server = null;
            if (ipep != null)
            {
                server = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                bool firstTime = true;
                do
                {
                    try
                    {
                        server.Bind (ipep);
                    }
                    catch (SocketException se)
                    {
                        if (firstTime)
                        {
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

        private static void ListenAsync (int ipepIndex)
        {
            Thread thread = new Thread (() => Listen (ipepIndex));
            thread.IsBackground = true;
            thread.Start ();
        }

        private static void Listen (int ipepIndex)
        {
            // import maxReqGotNumber from config file
            if (Conf.allConf.ContainsKey ("listen-max"))
            {
                if (int.TryParse (Conf.allConf["listen-max"][0], out int arg))
                {
                    maxReqGotNumber = arg;
                }
            }
            IPEndPoint ipep = localAddresses[ipepIndex];
            Socket server = CreateServer (ipep);
            if (server != null)
            {
                // init lists
                servers[ipepIndex] = server;
                // listen
                server.Listen (100);
                Console.WriteLine ("start to Listen: {0}",
                    server.LocalEndPoint.ToString ());
                // socket connections handle
                while (IsRunning)
                {
                    if (reqGotNumbers.Value >= maxReqGotNumber)
                    {
                        Thread.Sleep (100); // wait until reqGotNumber < maxReqGotNumber
                    }
                    else
                    {
                        Socket client;
                        try
                        {
                            client = server.Accept ();
                        }
                        catch (SocketException se)
                        {
                            Console.WriteLine ("{0}",
                                se.Message);
                            client = null;
                            Close ();
                        }
                        catch (ObjectDisposedException) { client = null; }

                        if (client != null)
                        {
                            // set timeout to avoid ddos
                            client.SendTimeout = Conf.PipeTimeOut;
                            client.ReceiveTimeout = Conf.PipeTimeOut;
                            reqGotNumbers.Up ();
                            HandleClientAsync (client, ipepIndex);
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine ("error: fail to create server -> {0}", ipep.ToString ());
            }
        }

        private static void HandleClientAsync (Socket socket2Client, int ipepIndex)
        {
            object[] args = new object[2] { socket2Client, ipepIndex };
            Task taskHandleClient = new Task (() => handleClient (socket2Client, ipepIndex),
                TaskCreationOptions.LongRunning);
            taskHandleClient.Start ();
        }

        private static void handleClient (Socket socket2Client, int ipepIndex)
        {
            Tunnel tunnel2Add = new Tunnel(socket2Client,null,Conf.encryptionKey);
            bool result = RequestHandler.Handle (tunnel2Add);
            if (result)
            {
                tunnel2Add.Flow ();
            }
            else
            {
                tunnel2Add.Close ();
            }
            reqGotNumbers.Down ();
        }

        public static double Speed ()
        {
            double speed = 0;
            foreach (EagleTunnelUser item in EagleTunnelUser.users.Values)
            {
                speed += item.Speed;
            }
            if (Conf.LocalUser != null)
            {
                speed += Conf.LocalUser.Speed;
            }
            return speed;
        }

        private static void LimitSpeed ()
        {
            if (Conf.allConf.ContainsKey ("speed-check"))
            {
                if (Conf.allConf["speed-check"][0] == "on")
                {
                    if (Conf.allConf.ContainsKey ("speed-limit"))
                    {
                        if (Conf.allConf["speed-limit"][0] == "on")
                        {
                            while (IsRunning)
                            {
                                foreach (EagleTunnelUser item in EagleTunnelUser.users.Values)
                                {
                                    item.LimitSpeedAsync ();
                                }
                                Thread.Sleep (5000);
                            }
                        }
                    }
                }
            }
        }

        public static void Close ()
        {
            if (IsRunning)
            {
                IsRunning = false;
                Thread.Sleep (1000);
                // stop listening
                lock (servers)
                {
                    foreach (Socket item in servers)
                    {
                        if (item != null)
                        {
                            try
                            {
                                item.Close ();
                            }
                            catch {; }
                        }
                    }
                }
                EagleTunnelHandler.StopResolvInside ();
            }
        }

        public static void CloseAsync ()
        {
            Task taskClose = new Task (() => Close ());
            taskClose.Start ();
        }
    }
}
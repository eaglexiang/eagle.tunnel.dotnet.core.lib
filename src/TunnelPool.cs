using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace eagle.tunnel.dotnet.core
{
    public class TunnelPool
    {
        private static ConcurrentQueue<Tunnel> pool = new ConcurrentQueue<Tunnel>();
        private static ConcurrentQueue<Tunnel> used;
        private static bool IsRunning = false;
        private static object lockObject = new object();

        public static Tunnel Get(Socket left = null, Socket right = null, byte encryptionKey = 0)
        {
            Tunnel result;
            if (pool.TryDequeue(out result))
            {
                result.SocketL = left;
                result.SocketR = right;
                result.EncryptionKey = encryptionKey;
            }
            else
            {
                if (used.Count < Conf.maxClientsCount)
                {
                    result = new Tunnel(left, right, encryptionKey);
                }
                else
                {
                    if (used.TryDequeue(out result))
                    {
                        result.Close();
                        result.SocketL = left;
                        result.SocketR = right;
                        result.EncryptionKey = encryptionKey;
                    }
                    else
                    {
                        throw (new System.Exception("please add value of maxClients"));
                    }
                }
            }
            if (IsRunning)
            {
                used.Enqueue(result);
            }
            return result;
        }

        public static void StartCheck()
        {
            lock (lockObject)
            {
                if (!IsRunning)
                {
                    used = new ConcurrentQueue<Tunnel>();
                    Task.Factory.StartNew(() => CheckTunnels(), TaskCreationOptions.LongRunning);
                    IsRunning = true;
                }
            }
        }

        public static void StopCheck()
        {
            lock (lockObject)
            {
                IsRunning = false;
            }
        }

        private static void CheckTunnels()
        {
            while (IsRunning)
            {
                if (used.TryDequeue(out Tunnel tunnel2Check))
                {
                    if (!(tunnel2Check.IsOpening || tunnel2Check.IsFlowing))
                    {
                        pool.Enqueue(tunnel2Check); // reuse
                    }
                    else
                    {
                        used.Enqueue(tunnel2Check);
                    }
                    Thread.Sleep(100);
                }
                else
                {
                    Thread.Sleep(10000);
                }
            }
        }
    }
}
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace eagle.tunnel.dotnet.core
{
    public class TunnelPool
    {
        public static ConcurrentQueue<Tunnel> pool = new ConcurrentQueue<Tunnel> ();
        public static ConcurrentQueue<Tunnel> used;
        private static bool IsRunning = false;
        private static object lockObject = new object ();

        public static Tunnel Get (Socket left = null, Socket right = null, byte encryptionKey = 0)
        {
            Tunnel result;
            if (pool.TryDequeue (out result))
            {
                result.Restore (left, right, encryptionKey);
                if (IsRunning)
                {
                    used.Enqueue (result);
                }
            }
            else
            {
                if (used.TryDequeue (out result))
                {
                    result.Close ();
                    result.Restore (left, right, encryptionKey);
                }
                else
                {
                    result = new Tunnel (left, right, encryptionKey);
                    System.Console.WriteLine ("new Tunnel!");
                }
            }
            return result;
        }

        public static void StartCheck ()
        {
            lock (lockObject)
            {
                if (!IsRunning)
                {
                    used = new ConcurrentQueue<Tunnel> ();
                    Task.Factory.StartNew (() => CheckTunnels (), TaskCreationOptions.LongRunning);
                    IsRunning = true;
                    if (pool.IsEmpty && used.IsEmpty)
                    {
                        Alloc (1024);
                    }
                }
            }
        }

        public static void Alloc (int count)
        {
            for (int i = 0; i < count; ++i)
            {
                Tunnel tunnel2Add = new Tunnel ();
                pool.Enqueue (tunnel2Add);
            }
        }

        public static void StopCheck ()
        {
            lock (lockObject)
            {
                IsRunning = false;
            }
        }

        private static void CheckTunnels ()
        {
            while (IsRunning)
            {
                if (used.TryDequeue (out Tunnel tunnel2Check))
                {
                    if (!(tunnel2Check.IsOpening || tunnel2Check.IsFlowing))
                    {
                        tunnel2Check.Close ();
                        tunnel2Check.Release ();
                        pool.Enqueue (tunnel2Check); // reuse
                    }
                    else
                    {
                        used.Enqueue (tunnel2Check);
                        Thread.Sleep (100);
                    }
                }
                else
                {
                    Thread.Sleep (100);
                }
            }
        }
    }
}
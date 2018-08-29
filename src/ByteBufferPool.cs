using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace eagle.tunnel.dotnet.core
{
    public class ByteBufferPool
    {
        private static ConcurrentQueue<ByteBuffer> pool =
            new ConcurrentQueue<ByteBuffer> ();
        private static ConcurrentQueue<ByteBuffer> used =
            new ConcurrentQueue<ByteBuffer> ();
        private static bool IsRunning { get; set; }
        private static object lockObject = new object ();
        public static ByteBuffer Get ()
        {
            ByteBuffer result;
            if (pool.TryDequeue (out result))
            {
                result.Using = true;
                if(IsRunning){
                    used.Enqueue (result);
                }
            }
            else
            {
                result = new ByteBuffer ();
                System.Console.WriteLine("new ByteBuffer!");
            }
            // int countOfBufferPool = ByteBufferPool.pool.Count;
            // int countOfBufferUsed = ByteBufferPool.used.Count;
            // int countOfTunnelPool = TunnelPool.pool.Count;
            // int countOfTunnelUsed = TunnelPool.used.Count;
            // System.Console.Write ("bufferPool: {0} + {1} = {2}\t", countOfBufferPool, countOfBufferUsed, countOfBufferPool + countOfBufferUsed);
            // System.Console.WriteLine ("tunnelPool: {0} + {1} = {2}", countOfTunnelPool, countOfTunnelUsed, countOfTunnelPool + countOfTunnelUsed);
            return result;
        }

        public static void Alloc (int count)
        {
            for (int i = 0; i < count; ++i)
            {
                ByteBuffer buffer = new ByteBuffer ();
                pool.Enqueue (buffer);
            }
        }

        public static void StartCheck ()
        {
            lock (lockObject)
            {
                if (!IsRunning)
                {
                    IsRunning = true;
                    Task.Factory.StartNew (() => Check (),
                        TaskCreationOptions.LongRunning);
                    if (pool.IsEmpty && used.IsEmpty)
                    {
                        Alloc (1024);
                    }
                }
            }
        }

        public static void StopCheck ()
        {
            lock (lockObject)
            {
                if (IsRunning)
                {
                    IsRunning = false;
                }
            }
        }

        private static void Check ()
        {
            while (IsRunning)
            {
                if (used.TryDequeue (out ByteBuffer buffer))
                {
                    if (buffer.Using)
                    {
                        used.Enqueue (buffer);
                        Thread.Sleep (100);
                    }
                    else
                    {
                        buffer.Restore ();
                        pool.Enqueue (buffer);
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
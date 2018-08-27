using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace eagle.tunnel.dotnet.core
{
    public class ByteBufferPool
    {
        private static ConcurrentQueue<ByteBuffer> pool =
            new ConcurrentQueue<ByteBuffer>();
        private static ConcurrentQueue<ByteBuffer> used =
            new ConcurrentQueue<ByteBuffer>();
        private static bool IsRunning { get; set; }
        private static object lockObject = new object();
        public static ByteBuffer Get()
        {
            ByteBuffer result;
            if (!pool.TryDequeue(out result))
            {
                result = new ByteBuffer();
            }
            result.Using = true;
            used.Enqueue(result);
            return result;
        }

        public static void Alloc(int count){
            for(int i=0;i<count;++i){
                ByteBuffer buffer = new ByteBuffer();
                pool.Enqueue(buffer);
            }
        }

        public static void StartCheck()
        {
            lock (lockObject)
            {
                if (!IsRunning)
                {
                    IsRunning = true;
                    Task.Factory.StartNew(() => Check(),
                        TaskCreationOptions.LongRunning);
                }
            }
        }

        public static void StopCheck()
        {
            lock (lockObject)
            {
                if (IsRunning)
                {
                    IsRunning = false;
                }
            }
        }

        private static void Check()
        {
            while (IsRunning)
            {
                if (used.TryDequeue(out ByteBuffer buffer))
                {
                    if (buffer.Using)
                    {
                        used.Enqueue(buffer);
                        Thread.Sleep(100);
                    }
                    else
                    {
                        buffer.Restore();
                        pool.Enqueue(buffer);
                    }
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }
    }
}
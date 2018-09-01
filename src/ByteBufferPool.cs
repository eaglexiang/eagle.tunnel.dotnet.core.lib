using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace eagle.tunnel.dotnet.core {
    public class ByteBufferPool {
        private static ConcurrentQueue<ByteBuffer> pool =
            new ConcurrentQueue<ByteBuffer> ();
        public static ByteBuffer Get () {
            ByteBuffer result;
            while (!pool.TryDequeue (out result)) {
                Thread.Sleep (100);
            }
            // int countOfBufferPool = ByteBufferPool.pool.Count;
            // System.Console.Write ("bufferPool: {0}\n", countOfBufferPool);
            return result;
        }

        public static void Release (ByteBuffer buffer) {
            pool.Enqueue (buffer);
        }

        public static void Alloc (int count) {
            for (int i = 0; i < count; ++i) {
                ByteBuffer buffer = new ByteBuffer ();
                pool.Enqueue (buffer);
            }
        }
    }
}
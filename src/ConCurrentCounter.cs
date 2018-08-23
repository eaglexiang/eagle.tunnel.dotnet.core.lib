namespace eagle.tunnel.dotnet.core
{
    public class ConCurrentCounter
    {
        private int[] data;
        private object[] locks;
        private int index;
        private int size;
        public int Size
        {
            get
            {
                return size;
            }
        }

        public ConCurrentCounter(int size = 10)
        {
            this.size = size;
            data = new int[size];
            locks = new object[size];
            index = 0;
            for (int i = 0; i < size; ++i)
            {
                data[i] = 0;
                locks[i] = new object();
            }
        }

        public int Value
        {
            get
            {
                int result = 0;
                int length = data.Length;
                for (int i = 0; i < length; ++i)
                {
                    result += data[i];
                }
                return result;
            }
        }

        public void Add(int i)
        {
            int _index = index++;
            _index %= size;
            lock (locks[_index])
            {
                data[_index] += i;
            }
        }

        public void Minus(int i)
        {
            int _index = index++;
            _index %= size;
            lock (locks[_index])
            {
                data[_index] -= i;
            }
        }

        public void Up()
        {
            Add(1);
        }

        public void Down()
        {
            Minus(1);
        }
    }
}
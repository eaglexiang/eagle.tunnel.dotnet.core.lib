namespace eagle.tunnel.dotnet.core {
    public class ConCurrentCounter {
        public class ConCurrentCounterData {
            int[] data;
            object[] locks;
            int index;

            public ConCurrentCounterData (int sizeOfLocks) {
                if (sizeOfLocks <= 0) {
                    throw new System.ArgumentOutOfRangeException ();
                }
                index = 0;
                data = new int[sizeOfLocks];
                locks = new object[sizeOfLocks];
                for (int i = 0; i < sizeOfLocks; ++i) {
                    data[i] = 0;
                    locks[i] = new object ();
                }
            }

            public int Value {
                get {
                    int result = 0;
                    foreach (int item in data) {
                        result += item;
                    }
                    return result;
                }

                set {
                    for (int i = 0; i < data.Length; ++i) {
                        locks[i] = new object ();
                        data[i] = 0;
                    }
                    index = 0;
                    data[0] = value;
                }
            }

            public int GetIndex () {
                int result = index++;
                result %= data.Length;
                return result;
            }

            public void Add (int indexOfLock, int i) {
                lock (locks[indexOfLock]) {
                    data[indexOfLock] += i;
                }
            }

            public void Minus (int indexOfLock, int i) {
                lock (locks[indexOfLock]) {
                    data[indexOfLock] -= i;
                }
            }
        }

        public ConCurrentCounterData data;
        private int index;
        public ConCurrentCounter (int size = 10) {
            data = new ConCurrentCounterData (size);
            index = data.GetIndex ();
        }

        public ConCurrentCounter (ConCurrentCounter another) {
            data = another.data;
            index = data.GetIndex ();
        }

        public int Value {
            get {
                return data.Value;
            }
            set {
                data.Value = value;
            }
        }

        public void Add (int i) {
            data.Add (index, i);
        }

        public void Minus (int i) {
            data.Minus (index, i);
        }

        public static ConCurrentCounter operator + (ConCurrentCounter i0, int i1) {
            i0.Add (i1);
            return new ConCurrentCounter (i0);
        }

        public static ConCurrentCounter operator - (ConCurrentCounter i0, int i1) {
            i0.Minus (i1);
            return new ConCurrentCounter (i0);
        }
    }
}
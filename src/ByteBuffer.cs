using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;

namespace eagle.tunnel.dotnet.core
{
    public class ByteBuffer
    {
        public byte[] array;
        public bool Using { get; set; }
        public int Length { get; set; }
        public ByteBuffer()
        {
            array = new byte[102400];
            Using = false;
            Length = 0;
        }

        public void Restore()
        {
            Using = false;
            Length = 0;
        }

        public byte this[int index]
        {
            get
            {
                return array[index];
            }
            set
            {
                array[index] = value;
            }
        }

        public void Set(byte[] src)
        {
            Set(src, 0, src.Length);
        }

        public void Set(byte[] src, int index, int count)
        {
            if (count > array.Length)
            {
                array = new byte[count];
            }
            System.Array.Copy(src, index, array, 0, count);
            Length = count;
        }

        public void Set(ByteBuffer src)
        {
            Set(src.array, 0, src.Length);
        }

        public void Set(int length)
        {
            if (length > array.Length)
            {

                byte[] newArray = new byte[length];
                array.CopyTo(newArray, 0);
                array = newArray;
            }
        }

        public void Set(string src, Encoding code)
        {
            Length = code.GetBytes(src, 0, src.Length,
            array, 0);
        }

        public void Set(string src)
        {
            Set(src, Encoding.UTF8);
        }


        public string ToString(Encoding code)
        {
            return code.GetString(array, 0, Length);
        }

        public override string ToString()
        {
            return ToString(Encoding.UTF8);
        }

        public int Receive(Socket socekt2Receive)
        {
            try
            {
                Length = socekt2Receive.Receive(array);
            }
            catch { Length = -1; }
            return Length;
        }

        public int Send(Socket socket2Send)
        {
            int sent;
            try
            {
                sent = socket2Send.Send(array, Length, SocketFlags.None);
            }
            catch { sent = -1; }
            return sent;
        }
    }
}
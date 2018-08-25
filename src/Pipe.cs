using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace eagle.tunnel.dotnet.core
{
    public class Pipe
    {
        public string UserFrom { get; set; }
        public int BytesTransferred { get; private set; }
        private Socket socketfrom;
        public Socket SocketFrom
        {
            get
            {
                return socketfrom;
            }
            set
            {
                socketfrom = value;
                if (socketfrom != null)
                {
                    socketfrom.NoDelay = true;
                }
            }
        }
        private Socket socketto;
        public Socket SocketTo
        {
            get
            {
                return socketto;
            }
            set
            {
                socketto = value;
                if (socketto != null)
                {
                    socketto.NoDelay = true;
                }
            }
        }
        public bool EncryptFrom { get; set; }
        public bool EncryptTo { get; set; }
        public byte EncryptionKey { get; set; }
        private byte[] bufferRead;
        public bool IsRunning { get; private set; }
        public object IsWaiting { get; set; }

        public Pipe(Socket from = null, Socket to = null, string user = null, byte encryptionKey = 0)
        {
            SocketFrom = from;
            SocketTo = to;
            EncryptFrom = false;
            EncryptTo = false;

            UserFrom = user;
            BytesTransferred = 0;
            bufferRead = new byte[2048];
            IsRunning = false;
            EncryptionKey = encryptionKey;
        }

        public int Write(byte[] buffer, int offset, int count)
        {
            int result = -1;
            if (buffer != null)
            {
                byte[] tmpBuffer = new byte[count];
                Array.Copy(buffer, offset, tmpBuffer, 0, count);
                if (EncryptTo)
                {
                    tmpBuffer = Encrypt(tmpBuffer);
                }
                if (SocketTo != null && SocketTo.Connected)
                {
                    try
                    {
                        result = SocketTo.Send(tmpBuffer);
                    }
                    catch { result = -1; }
                }
            }
            return result;
        }

        public int Write(byte[] buffer)
        {
            return Write(buffer, 0, buffer.Length);
        }

        public int Write(string msg)
        {
            int result = -1;
            if (!string.IsNullOrEmpty(msg))
            {
                byte[] buffer = Encoding.UTF8.GetBytes(msg);
                result = Write(buffer);
            }
            return result;
        }

        public string ReadString()
        {
            byte[] tmpBuffer = ReadByte();
            if (tmpBuffer != null)
            {
                try
                {
                    return Encoding.UTF8.GetString(tmpBuffer);
                }
                catch
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        public byte[] ReadByte()
        {
            byte[] result = null;
            if (SocketFrom != null && SocketFrom.Connected)
            {
                int count;
                try
                {
                    count = SocketFrom.Receive(bufferRead);
                }
                catch { count = -1; }
                if (count > 0)
                {
                    byte[] tmpBuffer = new byte[count];
                    Array.Copy(bufferRead, tmpBuffer, count);
                    if (EncryptFrom)
                    {
                        tmpBuffer = Decrypt(tmpBuffer);
                    }
                    result = tmpBuffer;
                    BytesTransferred += count;
                    Wait();
                }
            }
            return result;
        }

        // wait for speed limit
        private void Wait()
        {
            if (IsWaiting != null)
            {
                bool IsWaiting = (bool)this.IsWaiting;
                while (IsWaiting)
                {
                    Thread.Sleep(1000);
                }
            }
        }

        public void Flow()
        {
            if (!IsRunning)
            {
                IsRunning = true;
                Task taskFlow = new Task(() => _Flow());
                taskFlow.Start();
            }
        }

        private void _Flow()
        {
            while (IsRunning)
            {
                byte[] buffer = ReadByte();
                if (buffer != null)
                {
                    int written = Write(buffer);
                    if (written <= 0)
                    {
                        Close();
                    }
                }
                else
                {
                    Close();
                }
            }
        }

        public byte[] Encrypt(byte[] src)
        {
            byte[] des = new byte[src.Length];
            for (int i = 0; i < src.Length; ++i)
            {
                des[i] = (byte)(src[i] ^ EncryptionKey);
            }
            return des;
        }

        public byte[] Decrypt(byte[] src)
        {
            byte[] des = new byte[src.Length];
            for (int i = 0; i < src.Length; ++i)
            {
                des[i] = (byte)(src[i] ^ EncryptionKey);
            }
            return des;
        }

        public void Close()
        {
            IsRunning = false;

            if (SocketFrom != null)
            {
                if (SocketFrom.Connected)
                {
                    try
                    {
                        SocketFrom.Shutdown(SocketShutdown.Both);
                    }
                    catch {; }
                    Thread.Sleep(10);
                    SocketFrom.Close();
                }
            }
            if (SocketTo != null)
            {
                if (SocketTo.Connected)
                {
                    try
                    {
                        SocketTo.Shutdown(SocketShutdown.Both);
                    }
                    catch {; }
                    Thread.Sleep(10);
                    SocketTo.Close();
                }
            }
        }
    }
}
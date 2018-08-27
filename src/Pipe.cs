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
        private ByteBuffer bufferRead;
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
            bufferRead = new ByteBuffer();
            IsRunning = false;
            EncryptionKey = encryptionKey;
        }

        public void Restore(Socket from = null, Socket to = null, string user = null, byte encryptionKey = 0)
        {
            SocketFrom = from;
            SocketTo = to;
            EncryptFrom = false;
            EncryptTo = false;

            UserFrom = user;
            BytesTransferred = 0;
            IsRunning = false;
            EncryptionKey = encryptionKey;
        }

        public int Write(ByteBuffer buffer)
        {
            return Write(buffer.array, 0, buffer.Length);
        }

        public int Write(byte[] buffer, int offset, int count)
        {
            int result = -1;
            if (buffer != null)
            {
                ByteBuffer tmpBuffer = ByteBufferPool.Get();
                tmpBuffer.Set(buffer, offset, count);
                if (EncryptTo)
                {
                    Encrypt(tmpBuffer);
                }
                if (SocketTo != null && SocketTo.Connected)
                {
                    result = tmpBuffer.Send(SocketTo);
                }
                tmpBuffer.Using = false;
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
                ByteBuffer buffer = ByteBufferPool.Get();
                buffer.Set(msg);
                result = Write(buffer);
                buffer.Using = false;
            }
            return result;
        }

        public string ReadString()
        {
            string result = "";
            ByteBuffer buffer = ByteBufferPool.Get();
            ReadByte(buffer);
            if (buffer.Length > 0)
            {
                result = buffer.ToString();
            }
            buffer.Using = false;
            return result;
        }

        public int ReadByte(ByteBuffer buffer)
        {
            if (SocketFrom != null && SocketFrom.Connected)
            {
                int count = bufferRead.Receive(SocketFrom);
                if (count > 0)
                {
                    if (EncryptFrom)
                    {
                        Decrypt(bufferRead);

                    }
                    buffer.Set(bufferRead);
                    BytesTransferred += buffer.Length;
                    Wait();
                }
            }
            return buffer.Length;
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
                ByteBuffer buffer = ByteBufferPool.Get();
                ReadByte(buffer);
                if (buffer.Length > 0)
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
                buffer.Using = false;
            }
        }

        public void Encrypt(ByteBuffer src)
        {
            for (int i = 0; i < src.Length; ++i)
            {
                src[i] = (byte)(src[i] ^ EncryptionKey);
            }
        }

        public void Decrypt(ByteBuffer src)
        {
            for (int i = 0; i < src.Length; ++i)
            {
                src[i] = (byte)(src[i] ^ EncryptionKey);
            }
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
                        Thread.Sleep(10);
                        SocketFrom.Close();
                    }
                    catch {; }
                }
            }
            if (SocketTo != null)
            {
                if (SocketTo.Connected)
                {
                    try
                    {
                        SocketTo.Shutdown(SocketShutdown.Both);
                        Thread.Sleep(10);
                        SocketTo.Close();
                    }
                    catch {; }
                }
            }
        }
    }
}
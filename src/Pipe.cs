using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace eagle.tunnel.dotnet.core {
    public class Pipe {
        public string UserFrom { get; set; }
        private long bytesTransferred;
        private long bytesLastChecked;
        public Socket SocketFrom { get; set; }
        public Socket SocketTo { get; set; }
        public bool EncryptFrom { get; set; }
        public bool EncryptTo { get; set; }
        private static byte EncryptionKey = 0x22;
        private byte[] bufferRead;
        public bool IsRunning { get; private set; }
        public object IsWaiting { get; set; }
        private DateTime timeLastChecked;

        public Pipe (Socket from = null, Socket to = null, string user = null) {
            SocketFrom = from;
            SocketTo = to;
            EncryptFrom = false;
            EncryptTo = false;

            UserFrom = user;
            bytesTransferred = 0;
            bytesLastChecked = 0;
            bufferRead = new byte[2048];
            IsRunning = false;
            timeLastChecked = DateTime.Now;
        }

        public bool Write (byte[] buffer, int offset, int count) {
            bool result = false;
            if (buffer != null) {
                byte[] tmpBuffer = new byte[count];
                Array.Copy (buffer, offset, tmpBuffer, 0, count);
                if (EncryptTo) {
                    tmpBuffer = Encrypt (tmpBuffer);
                }
                if (SocketTo != null && SocketTo.Connected) {
                    int written;
                    try {
                        written = SocketTo.Send (tmpBuffer);
                    } catch { written = 0; }
                    result = written > 0;
                }
            }
            return result;
        }

        public bool Write (byte[] buffer) {
            return Write (buffer, 0, buffer.Length);
        }

        public bool Write (string msg) {
            bool result = false;
            if (!string.IsNullOrEmpty (msg)) {
                byte[] buffer = Encoding.UTF8.GetBytes (msg);
                result = Write (buffer);
            }
            return result;
        }

        public string ReadString () {
            byte[] tmpBuffer = ReadByte ();
            if (tmpBuffer != null) {
                try {
                    return Encoding.UTF8.GetString (tmpBuffer);
                } catch {
                    return null;
                }
            } else {
                return null;
            }
        }

        public byte[] ReadByte () {
            byte[] result = null;
            if (SocketFrom != null && SocketFrom.Connected) {
                int count;
                try {
                    count = SocketFrom.Receive (bufferRead);
                } catch { count = 0; }
                if (count > 0) {
                    byte[] tmpBuffer = new byte[count];
                    Array.Copy (bufferRead, tmpBuffer, count);
                    if (EncryptFrom) {
                        tmpBuffer = Decrypt (tmpBuffer);
                    }
                    result = tmpBuffer;
                    bytesTransferred += count;
                    Wait ();
                }
            }
            return result;
        }

        // wait for speed limit
        private void Wait () {
            if (IsWaiting != null) {
                bool IsWaiting = (bool) this.IsWaiting;
                while (IsWaiting) {
                    Thread.Sleep (1000);
                }
            }
        }

        public void Flow () {
            if (!IsRunning) {
                IsRunning = true;
                Thread thread_Flow = new Thread (_Flow);
                thread_Flow.IsBackground = true;
                thread_Flow.Start ();
            }
        }

        private void _Flow () {
            byte[] buffer = ReadByte ();
            while (IsRunning) {
                if (buffer != null) {
                    bool done = Write (buffer);
                    if (done) {
                        buffer = ReadByte ();
                        continue;
                    }
                }
                Close ();
            }
        }

        public static byte[] Encrypt (byte[] src) {
            byte[] des = new byte[src.Length];
            for (int i = 0; i < src.Length; ++i) {
                des[i] = (byte) (src[i] ^ EncryptionKey);
            }
            return des;
        }

        public static byte[] Decrypt (byte[] src) {
            byte[] des = new byte[src.Length];
            for (int i = 0; i < src.Length; ++i) {
                des[i] = (byte) (src[i] ^ EncryptionKey);
            }
            return des;
        }

        public void Close () {
            IsRunning = false;
            if (SocketFrom != null) {
                if (SocketFrom.Connected) {
                    try {
                        SocketFrom.Shutdown (SocketShutdown.Both);
                    } catch {; }
                    Thread.Sleep (10);
                    SocketFrom.Close ();
                }
            }
            if (SocketTo != null) {
                if (SocketTo.Connected) {
                    try {
                        SocketTo.Shutdown (SocketShutdown.Both);
                    } catch {; }
                    Thread.Sleep (10);
                    SocketTo.Close ();
                }
            }
        }

        public double Speed () {
            DateTime timeNow = DateTime.Now;
            long bytesNow = bytesTransferred;
            double seconds = (timeNow - timeLastChecked).TotalSeconds;
            long bytes = bytesNow - bytesLastChecked;
            double speed = bytes / seconds;
            bytesLastChecked = bytesNow;
            timeLastChecked = timeNow;
            return speed;
        }
    }
}
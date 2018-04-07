using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace eagle.tunnel.dotnet.core {
    public class Pipe {
        public string UserFrom { get; set; }
        public long BytesTransferred {get; private set;}
        public Socket SocketFrom { get; set; }
        public Socket SocketTo { get; set; }
        public bool EncryptFrom { get; set; }
        public bool EncryptTo { get; set; }
        private static byte EncryptionKey = 0x22;
        private byte[] bufferRead;
        public bool IsRunning { get; private set; }
        public bool IsWaiting { get; set; }

        public Pipe (Socket from = null, Socket to = null, string user = null) {
            SocketFrom = from;
            SocketTo = to;
            EncryptFrom = false;
            EncryptTo = false;

            UserFrom = user;
            BytesTransferred = 0;
            bufferRead = new byte[1024];
            IsRunning = false;
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
                    BytesTransferred += count;
                    Wait ();
                }
            }
            return result;
        }

        // wait for speed limit
        private void Wait () {
            if (IsWaiting) {
                Thread.Sleep (5000);
                IsWaiting = false;
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
                    Thread.Sleep (100);
                    try {
                        SocketFrom.Close ();
                    } catch {; }
                }
                SocketFrom = null;
            }
            if (SocketTo != null) {
                if (SocketTo.Connected) {
                    try {
                        SocketTo.Shutdown (SocketShutdown.Both);
                    } catch {; }
                    Thread.Sleep (100);
                    try {
                        SocketTo.Close ();
                    } catch {; }
                }
                SocketTo = null;
            }
        }
    }
}
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace eagle.tunnel.dotnet.core {
    public class Pipe {
        public string UserFrom { get; set; }
        public int BytesTransferred { get; private set; }
        public Socket SocketFrom { get; set; }
        public Socket SocketTo { get; set; }
        public bool EncryptFrom { get; set; }
        public bool EncryptTo { get; set; }
        private static byte EncryptionKey = 0x22;
        private byte[] bufferRead;
        public bool IsRunning { get; private set; }
        public object IsWaiting { get; set; }

        private Thread threadRead;
        private Thread threadWrite;
        private ConcurrentQueue<byte[]> buffers;

        public Pipe (Socket from = null, Socket to = null, string user = null) {
            SocketFrom = from;
            SocketTo = to;
            EncryptFrom = false;
            EncryptTo = false;

            UserFrom = user;
            BytesTransferred = 0;
            bufferRead = new byte[2048];
            IsRunning = false;

            buffers = new ConcurrentQueue<byte[]> ();
        }

        // private void threadRead_Handler () {
        //     while (IsRunning) {
        //         byte[] buffer = ReadByte ();
        //         if (buffer == null) {
        //             Close ();
        //             break;
        //         } else {
        //             buffers.Enqueue (buffer);
        //         }
        //     }
        // }

        // private void threadWrite_Handler () {
        //     while (IsRunning) {
        //         if (buffers.TryDequeue (out byte[] buffer)) {
        //             if (!Write (buffer)) {
        //                 buffers = new ConcurrentQueue<byte[]> ();
        //                 Close ();
        //             }
        //         } else {
        //             Thread.Sleep (100);
        //         }
        //     }
        // }

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
            // if (!IsRunning) {
            //     IsRunning = true;
            //     threadRead = new Thread (threadRead_Handler);
            //     threadRead.IsBackground = true;
            //     threadRead.Start ();
            //     threadWrite = new Thread (threadWrite_Handler);
            //     threadWrite.IsBackground = true;
            //     threadWrite.Start ();
            // }
        }

        private void _Flow () {
            byte[] buffer = ReadByte ();
            while (IsRunning) {
                if (buffer != null) {
                    bool done = Write (buffer);
                    if (done) {
                        buffer = ReadByte ();
                    } else {
                        Close ();
                    }
                } else {
                    Close ();
                }
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
            while (buffers.Count > 0) {
                Thread.Sleep (100);
            }

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
    }
}
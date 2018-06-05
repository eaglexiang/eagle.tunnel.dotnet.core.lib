using System.Collections;
using System.Collections.Concurrent;
using System;

namespace eagle.tunnel.dotnet.core {
    public class EagleTunnelUser {
        public string ID { get; }
        public string Password { get; set; }
        public int SpeedLimit { get; set; } // KB/s
        private object lockOfSpeedCheck;
        private ConcurrentQueue<Tunnel> tunnels;
        private object IsWaiting;
        private ulong bytesLastChecked;
        DateTime timeLastChecked;

        public EagleTunnelUser (string id, string password, bool enableSpeedChecker) {
            ID = id;
            Password = password;
            SpeedLimit = 0;
            tunnels = new ConcurrentQueue<Tunnel> ();
            lockOfSpeedCheck = new object ();
            IsWaiting = false;
            speedNow = 0;
            bytesLastChecked = 0;
            timeLastChecked = DateTime.Now;

            if (enableSpeedChecker) {
                System.Threading.Thread thread_CheckSpeed =
                    new System.Threading.Thread (CheckingSpeed);
                thread_CheckSpeed.IsBackground = true;
                thread_CheckSpeed.Start ();
            }
        }

        public static bool TryParse (string parameter, out EagleTunnelUser user, bool enableSpeedChecker) {
            user = null;
            if (parameter != null) {
                string[] args = parameter.Split (':');
                if (args.Length >= 2) {
                    bool enableChecker = enableSpeedChecker;
                    if (enableChecker) {
                        if (Conf.allConf.ContainsKey ("speed-check")) {
                            if (Conf.allConf["speed-check"][0] == "on") {
                                enableChecker = true;
                            }
                        }
                    }
                    user = new EagleTunnelUser (args[0], args[1], enableChecker);
                    if (args.Length >= 3) {
                        if (int.TryParse (args[2], out int speed)) {
                            user.SpeedLimit = speed;
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        public void AddTunnel (Tunnel tunnel2Add) {
            if (Conf.allConf.ContainsKey ("speed-check")) {
                if (Conf.allConf["speed-check"][0] == "on") {
                    tunnel2Add.IsWaiting = IsWaiting;
                    tunnels.Enqueue (tunnel2Add);
                }
            }
        }

        private double speedNow;
        public double Speed {
            private set {
                speedNow = value;
            }
            get {
                return speedNow;
            }
        }

        private void CheckingSpeed () {
            while (true) {
                Speed = _Speed ();
                System.Threading.Thread.Sleep (5000);
            }
        }

        private double _Speed () {
            ulong bytesNow = 0;
            for (int i = tunnels.Count; i > 0; --i) {
                if (tunnels.TryDequeue (out Tunnel tunnel)) {
                    if (tunnel.IsOpening) {
                        tunnels.Enqueue (tunnel);
                    } else {
                        bytesNow += tunnel.BytesTransffered;
                        if (tunnel.IsFlowing) {
                            tunnels.Enqueue (tunnel);
                        }
                    }
                }
            }
            DateTime timeNow = DateTime.Now;
            double seconds = (timeNow - timeLastChecked).TotalSeconds;
            double speed = ((double)(bytesNow - bytesLastChecked))/seconds;
            timeLastChecked = timeNow;
            bytesLastChecked = bytesNow;
            return speed / 1024;
        }

        public void LimitSpeedAsync () {
            System.Threading.Thread thread =
                new System.Threading.Thread (LimitSpeed);
            thread.IsBackground = true;
            thread.Start ();
        }

        public void LimitSpeed () {
            if (SpeedLimit > 0) {
                while (Speed > SpeedLimit) {
                    bool IsWaiting = (bool) this.IsWaiting;
                    IsWaiting = true;
                    System.Threading.Thread.Sleep (1000);
                }
                IsWaiting = false;
            }
        }

        public override string ToString () {
            return ID + ':' + Password;
        }

        public bool CheckAuthen (string pswd) {
            if (ID == "anonymous") {
                return false;
            } else {
                return pswd == Password;
            }
        }
    }
}
using System;
using System.Collections;
using System.Collections.Concurrent;

namespace eagle.tunnel.dotnet.core {
    public class EagleTunnelUser {
        public static ConcurrentDictionary<string, EagleTunnelUser> users =
            new ConcurrentDictionary<string, EagleTunnelUser> ();
        public string ID { get; }
        public string Password { get; set; }
        public int SpeedLimit { get; set; } // B/s
        private object lockOfSpeedCheck;
        private ConcurrentQueue<Tunnel> tunnels;
        private object IsWaiting;
        private int bytesLastChecked;
        DateTime timeLastChecked;

        private EagleTunnelUser (string id, string password, bool enableSpeedChecker) {
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

        public static bool TryParse (string parameter, out EagleTunnelUser user,
            bool enableSpeedChecker = false) {
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

        public static bool TryAdd (string parameter) {
            bool result = false;
            if (TryParse (parameter, out EagleTunnelUser newUser, true)) {
                if (users.TryAdd (newUser.ID, newUser)) {
                    result = true;
                }
            }
            return result;
        }

        public static EagleTunnelUser Check (string id, string password) {
            EagleTunnelUser result = null;
            if (users.ContainsKey (id)) {
                if (users[id].CheckAuthen (password)) {
                    result = users[id];
                }
            }
            return result;
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
                System.Threading.Thread.Sleep (500);
            }
        }

        private double _Speed () {
            int bytesNow = 0;
            for (int i = tunnels.Count; i > 0; --i) {
                if (tunnels.TryDequeue (out Tunnel tunnel)) {
                    if (tunnel.IsOpening) {
                        tunnels.Enqueue (tunnel);
                    } else {
                        bytesNow += tunnel.BytesTransffered;
                        if (tunnel.IsFlowing) {
                            tunnels.Enqueue (tunnel);
                        } else {
                            tunnel.Close ();
                        }
                    }
                }
            }
            DateTime timeNow = DateTime.Now;
            double seconds = (timeNow - timeLastChecked).TotalSeconds;
            double speed = ((double) (bytesNow - bytesLastChecked)) / seconds;
            timeLastChecked = timeNow;
            bytesLastChecked = bytesNow;
            return speed;
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
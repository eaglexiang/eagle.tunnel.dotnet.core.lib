using System.Collections;
using System.Collections.Concurrent;

namespace eagle.tunnel.dotnet.core {
    public class EagleTunnelUser {
        public string ID { get; }
        public string Password { get; set; }
        public int SpeedLimit { get; set; } // KB/s
        private ArrayList tunnels;
        private object lockOfSpeedCheck;
        private ConcurrentQueue<Tunnel> tunnels2Add;
        private object IsWaiting;
        private int tunnelsGCThresshold;

        public EagleTunnelUser (string id, string password) {
            ID = id;
            Password = password;
            SpeedLimit = 0;
            tunnels = new ArrayList ();
            tunnels2Add = new ConcurrentQueue<Tunnel> ();
            lockOfSpeedCheck = new object ();
            IsWaiting = false;
            tunnelsGCThresshold = 8;
        }

        public static bool TryParse (string parameter, out EagleTunnelUser user) {
            user = null;
            if (parameter != null) {
                string[] args = parameter.Split (':');
                if (args.Length >= 2) {
                    user = new EagleTunnelUser (args[0], args[1]);
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
            tunnels2Add.Enqueue (tunnel2Add);
            tunnel2Add.IsWaiting = IsWaiting;
        }

        public double Speed () {
            double speed = 0;
            // unable to check speed concurrently
            lock (lockOfSpeedCheck) {
                // update tunnels
                while (tunnels2Add.Count > 0) {
                    if (tunnels2Add.TryDequeue (out Tunnel tunnel2Add)) {
                        tunnels.Add (tunnel2Add);
                    }
                }
                // release finished tunnels
                if (tunnels.Count > tunnelsGCThresshold) {
                    ArrayList newTunnels = new ArrayList ();
                    foreach (Tunnel item in tunnels) {
                        if (item.IsWorking) {
                            newTunnels.Add (item);
                            speed += item.Speed () / 1024;
                        }
                    }
                    tunnels = newTunnels;
                    if (tunnels.Count > tunnelsGCThresshold) {
                        tunnelsGCThresshold *= 2;
                    }
                }
            }
            return speed;
        }

        public void LimitSpeedAsync () {
            System.Threading.Thread thread = new System.Threading.Thread (LimitSpeed);
            thread.IsBackground = true;
            thread.Start ();
        }

        public void LimitSpeed () {
            if (SpeedLimit > 0) {
                while (Speed () > SpeedLimit) {
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
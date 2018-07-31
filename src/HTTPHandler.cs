using System;
using System.Net;

namespace eagle.tunnel.dotnet.core {
    public class HTTPHandler {
        public static bool Handle (string firstMsg, Tunnel tunnel) {
            bool result = false;
            if (firstMsg != null && tunnel != null) {
                if (HTTPReqArgs.TryParse (firstMsg, out HTTPReqArgs e0)) {
                    IPEndPoint reqEP = HTTPReqArgs.GetIPEndPoint (e0);
                    EagleTunnelArgs e1 = new EagleTunnelArgs ();
                    e1.EndPoint = reqEP;
                    Tunnel tmpTunnel = EagleTunnelSender.Handle (
                        EagleTunnelHandler.EagleTunnelRequestType.TCP, e1);
                    if (tmpTunnel != null) {
                        tunnel.SocketR = tmpTunnel.SocketR;
                        tunnel.EncryptR = tmpTunnel.EncryptR;
                        if (Conf.LocalUser != null) {
                            Conf.LocalUser.AddTunnel (tunnel);
                        }
                        if (e0.HTTP_Request_Type == HTTPRequestType.CONNECT) {
                            // HTTPS: reply web client;
                            string re443 = "HTTP/1.1 200 Connection Established\r\n\r\n";
                            result = tunnel.WriteL (re443);
                        } else {
                            // HTTP: relay new request to web server
                            string newReq = HTTPReqArgs.CreateNewRequest(firstMsg);
                            result = tunnel.WriteR (newReq);
                        }
                    }
                }
            }
            return result;
        }
    }
}
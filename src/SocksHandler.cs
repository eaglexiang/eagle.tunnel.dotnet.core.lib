using System.Net;
using System.Net.Sockets;
using System.Text;

namespace eagle.tunnel.dotnet.core
{
    public class SocksHandler
    {
        public enum SOCKS5_CMDType
        {
            ERROR,
            Connect,
            Bind,
            Udp
        }

        public static bool Handle (ByteBuffer request, Tunnel tunnel)
        {
            bool result = false;
            if (request != null && tunnel != null)
            {
                int version = request[0];
                // check if is socks version 5
                if (version == '\u0005')
                {
                    string reply = "\u0005\u0000";
                    result = tunnel.WriteL (reply);
                    if (result)
                    {
                        ByteBuffer buffer = new ByteBuffer();
                        int read = tunnel.ReadL (buffer);
                        if (read >= 2)
                        {
                            SOCKS5_CMDType cmdType = (SOCKS5_CMDType) buffer[1];
                            switch (cmdType)
                            {
                                case SOCKS5_CMDType.Connect:
                                    result = HandleTCPReq (buffer, tunnel);
                                    break;
                            }
                        }
                    }
                }
            }
            return result;
        }

        private static bool HandleTCPReq (ByteBuffer request, Tunnel tunnel)
        {
            bool result = false;
            if (request != null && tunnel != null)
            {
                IPAddress ip = GetIP (request);
                int port = GetPort (request);
                if (ip != null && port != 0)
                {
                    IPEndPoint reqIPEP = new IPEndPoint (ip, port);
                    string reply;
                    EagleTunnelArgs e = new EagleTunnelArgs ();
                    e.EndPoint = reqIPEP;
                    e.tunnel = tunnel;
                    if (EagleTunnelSender.Handle (
                        EagleTunnelHandler.EagleTunnelRequestType.TCP, e))
                    {
                        if (Conf.LocalUser != null)
                        {
                            Conf.LocalUser.AddTunnel (tunnel);
                        }
                        reply = "\u0005\u0000\u0000\u0001\u0000\u0000\u0000\u0000\u0000\u0000";
                    }
                    else
                    {
                        reply = "\u0005\u0001\u0000\u0001\u0000\u0000\u0000\u0000\u0000\u0000";
                    }
                    result = tunnel.WriteL (reply);
                }
            }
            return result;
        }

        public static IPAddress GetIP (ByteBuffer request)
        {
            IPAddress ip;
            int destype = request[3];
            string ip_str;
            switch (destype)
            {
                case 1:
                    ip_str = request[4].ToString ();
                    ip_str += '.' + request[5].ToString ();
                    ip_str += '.' + request[6].ToString ();
                    ip_str += '.' + request[7].ToString ();
                    if (IPAddress.TryParse (ip_str, out IPAddress ipa0))
                    {
                        ip = ipa0;
                    }
                    else
                    {
                        ip = null;
                    }
                    break;
                case 3:
                    int len = request[4];
                    char[] hostChars = new char[len];
                    for (int i = 0; i < len; ++i)
                    {
                        hostChars[i] = (char) request[5 + i];
                    }
                    string host = new string (hostChars);
                    // if host is real ip but not domain name
                    if (IPAddress.TryParse (host, out IPAddress ipa))
                    {
                        ip = ipa;
                    }
                    else
                    {
                        EagleTunnelArgs e = new EagleTunnelArgs ();
                        e.Domain = host;
                        EagleTunnelSender.Handle (
                            EagleTunnelHandler.EagleTunnelRequestType.DNS, e);
                        ip = e.IP;
                    }
                    break;
                default:
                    ip = null;
                    break;
            }
            return ip;
        }

        public static int GetPort (ByteBuffer request)
        {
            try
            {
                int destype = request[3];
                int port;
                int high;
                int low;
                switch (destype)
                {
                    case 1:
                        high = request[8];
                        low = request[9];
                        port = high * 0x100 + low;
                        break;
                    case 3:
                        int len = request[4];
                        high = request[5 + len];
                        low = request[6 + len];
                        port = high * 0x100 + low;
                        break;
                    default:
                        port = 0;
                        break;
                }
                return port;
            }
            catch
            {
                return 0;
            }
        }
    }
}
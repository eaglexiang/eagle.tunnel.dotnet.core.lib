using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace eagle.tunnel.dotnet.core
{
    public class RequestHandler
    {

        enum RequestType
        {
            Unknown,
            SOCKS5,
            HTTP_Proxy,
            Eagle_Tunnel
        }

        private RequestHandler() { }

        public static bool Handle(Tunnel tunnel)
        {
            bool result = false;
            if (tunnel == null)
            {
                result = false;
            }
            else
            {
                ByteBuffer firstMsg = ByteBufferPool.Get();
                int read = tunnel.ReadL(firstMsg);
                if (read > 0)
                {
                    string firstMsg_Str = firstMsg.ToString();
                    RequestType reqType = GetType(firstMsg);
                    switch (reqType)
                    {
                        case RequestType.Eagle_Tunnel:
                            if (Conf.EnableEagleTunnel)
                            {
                                result = EagleTunnelHandler.Handle(
                                    firstMsg_Str, tunnel);
                            }
                            break;
                        case RequestType.HTTP_Proxy:
                            if (Conf.EnableHTTP)
                            {
                                result = HTTPHandler.Handle(
                                    firstMsg_Str, tunnel);
                            }
                            break;
                        case RequestType.SOCKS5:
                            if (Conf.EnableSOCKS)
                            {
                                result = SocksHandler.Handle(
                                    firstMsg, tunnel);
                            }
                            break;
                    }
                }
                ByteBufferPool.Release(firstMsg);
            }
            return result;
        }

        private static RequestType GetType(ByteBuffer msg)
        {
            RequestType result = RequestType.Unknown;
            if (msg[0] == 5)
            {
                result = RequestType.SOCKS5;
            }
            else
            {
                string msgStr = msg.ToString();
                string[] args = msgStr.Split(' ');
                if (args.Length >= 2)
                {
                    if (Enum.TryParse(args[0], out HTTPRequestType type))
                    {
                        result = RequestType.HTTP_Proxy;
                    }
                    else if (args[0] == "eagle_tunnel")
                    {
                        result = RequestType.Eagle_Tunnel;
                    }
                }
            }
            return result;
        }
    }
}
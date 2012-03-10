using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;

namespace Proxy
{
    /// <summary>
    /// Half-duplex UDP Proxy, dispatches UDP packets from src to target (but not vice-versa!).
    /// </summary>
    public class UdpProxy : IProxy
    {

        private IPEndPoint _src;
        private IPEndPoint _target;

        public UdpProxy(IPEndPoint src, IPEndPoint target)
        {
            _src = src;
            _target = target;
        }
        
        private bool _alreadyCalledProxyMethod = false;

        Protocol IProxy.GetProtocol()
        {
            return Protocol.UDP;
        }

        IPEndPoint IProxy.GetSource()
        {
            return _src;
        }

        IPEndPoint IProxy.GetTarget()
        {
            return _target;
        }

        void IProxy.LaunchProxy()
        {
            if (_alreadyCalledProxyMethod)
            {
                return;
            }
            _alreadyCalledProxyMethod = true;

            try
            {

                UdpClient client = new UdpClient(_src);
                
                Socket server = new Socket(
                        AddressFamily.InterNetwork, 
                        SocketType.Dgram, 
                        ProtocolType.Udp);
                // ---

                AsyncCallback clientCallback = null;

                AsyncCallback serverCallback = null;

                // TODO: Only done once ... even so, put his in assync! :D
                server.Connect(_target);

                serverCallback = delegate(IAsyncResult res)
                {
                    SocketError err = default(SocketError);
                    int bytes = server.EndSend(res, out err);

                    // Check if all was sent ...
                    if (err == SocketError.Success)
                    {
                        Trace.WriteLine(string.Format("Sent {0} bytes to server.", bytes));
                    }
                    else 
                    {
                        Trace.WriteLine(string.Format("Error {0} on sending", err));
                    }
                    
                };

                clientCallback = delegate(IAsyncResult res)
                {
                    // Reattach listener asap

                    client.BeginReceive(clientCallback, client);

                    byte[] datagram = client.EndReceive(res, ref _src);                   
                                        
                    Trace.WriteLine(string.Format("Received {0} bytes from client.", datagram.Length));

                    // Send to target
                    
                    Trace.WriteLine(string.Format("About to send {0} bytes to server", datagram.Length));
                                        
                    server.BeginSend(
                            datagram, 0, datagram.Length,
                            SocketFlags.None, serverCallback, server);
                    // --
                };

                IAsyncResult result = client.BeginReceive(clientCallback, client);                                

            }
            catch (Exception e)
            {
                throw e;
                // TODO: Better exception message ...
                //Trace.WriteLine("Exception at UdpProxy: " + e.Message);
            }
        }



    }
}

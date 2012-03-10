using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;

namespace Proxy
{

    class TcpProxy : IProxy
    {

        IPEndPoint _src;
        IPEndPoint _target;

        public TcpProxy(IPEndPoint src, IPEndPoint target)
        {
            _src = src;
            _target = target;
        }

        void IProxy.LaunchProxy()
        {
            // This waits for incoming clients
            TcpListener proxyListener = new TcpListener(_src);
            proxyListener.Start();

            // Listener setup
            proxyListener.BeginAcceptTcpClient(
                    _acceptClientsCallback,
                    new TcpClientAcceptedState
                    {
                        Callback = _acceptClientsCallback,
                        ProxyListener = proxyListener,
                        Proxy = this
                    });
            // ---
        }

        #region "Client"

        private static AsyncCallback _connectCallback = new AsyncCallback(Connect);
        private static AsyncCallback _writeStreamCallback = new AsyncCallback(WriteStream);

        private class ConnectState
        {
            public AsyncCallback Callback;
            public TcpClient Client;
            public Action ActionAfterConnect;
        }

        /// <summary>
        /// Ends connecting to state.Client.
        /// </summary>
        /// <param name="res"></param>
        private static void Connect(IAsyncResult res)
        {
            try
            {
                ConnectState state = res.AsyncState as ConnectState;
                state.Client.EndConnect(res);

                if (state.ActionAfterConnect != null)
                {
                    state.ActionAfterConnect();
                }
            }
            catch (Exception e)
            {
                Console.ReadKey();
            }
        }

        private class WriteStreamState
        {
            public AsyncCallback Callback;
            public TcpClient WriteStreamOwner;
            public NetworkStream WriteStream;
            public byte[] Buffer;
            public int Offset;
            public TcpClient ClientToReport;

            public TcpProxy Proxy;
        }

        public static void StartReadingRemote(
                TcpProxy proxy,
                TcpClient remoteClient,
                TcpClient localClient)
        {
            try
            {
                NetworkStream stream = null;
                try
                {
                    stream = remoteClient.GetStream();

                    ReadStreamState readState = new ReadStreamState()
                    {
                        Callback = _readStreamCallback,
                        ReadStreamOwner = remoteClient,
                        ClientToReport = localClient,
                        Buffer = new byte[remoteClient.ReceiveBufferSize],
                        Offset = 0,
                        ReadStream = stream,
                        Proxy = proxy
                    };

                    // Begin write. Resume reading will be done in the callback.
                    stream.BeginRead(
                            readState.Buffer,
                            readState.Offset,
                            readState.Buffer.Length,
                            readState.Callback,
                            readState);
                    //--
                }
                catch (IOException ioEx)
                {
                    if (remoteClient.Connected)
                    {
                        Console.ReadKey();
                    }
                    else
                    {
                        // Connect, after connected, try to send again.
                        ConnectState connState = new ConnectState()
                        {
                            Callback = _connectCallback,
                            Client = new TcpClient()
                        };

                        connState.ActionAfterConnect = delegate()
                        {
                            StartReadingRemote(proxy, connState.Client, localClient);
                        };

                        // FIXME: Depends if writing is being done on proxy client or proxy server ...
                        connState.Client.BeginConnect(
                                proxy._target.Address,
                                proxy._target.Port,
                                connState.Callback,
                                connState);
                        //---
                    }
                }
                catch (Exception e)
                {
                    Console.ReadKey();
                }
            }
            catch (Exception e)
            {
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Ends writing to state.Stream.
        /// </summary>
        /// <param name="res"></param>
        private static void WriteStream(IAsyncResult res)
        {
            try
            {
                WriteStreamState state = res.AsyncState as WriteStreamState;
                state.WriteStream.EndWrite(res);

                Console.WriteLine("TCP: End Write with (local: {0}, remote: {1})",
                        state.WriteStreamOwner.Client.LocalEndPoint,
                        state.WriteStreamOwner.Client.RemoteEndPoint);
                //---

                StartReadingRemote(state.Proxy, state.WriteStreamOwner, state.ClientToReport);
            }
            catch (Exception e)
            {
                Console.WriteLine("EXCEPTION: WriteStream callback: {0}", e.Message);
                Console.ReadKey();
            }
        }

        #endregion

        #region "Listener"

        private static AsyncCallback _acceptClientsCallback = new AsyncCallback(TcpClientAccepted);
        private static AsyncCallback _readStreamCallback = new AsyncCallback(ReadStream);

        private class TcpClientAcceptedState
        {
            /// <summary>
            /// The callback that is being called, assigned to the ProxyListener.BeginAccept
            /// </summary>
            public AsyncCallback Callback;

            /// <summary>
            /// The proxy listener instance (local server)
            /// </summary>
            public TcpListener ProxyListener;

            public TcpProxy Proxy;
        }

        /// <summary>
        /// Begins a new Accept and ends the current one. Begins reading of client.GetStream()
        /// </summary>
        /// <param name="res"></param>
        private static void TcpClientAccepted(IAsyncResult res)
        {
            try
            {
                TcpClientAcceptedState state = res.AsyncState as TcpClientAcceptedState;

                // Re-regist listener asap
                state.ProxyListener.BeginAcceptTcpClient(state.Callback, state);

                // Get remote client
                TcpClient remoteClient = state.ProxyListener.EndAcceptTcpClient(res);

                TcpClient localClient = new TcpClient();

                ConnectState connState = new ConnectState() { 
                    Callback = _connectCallback,
                    Client = localClient
                };

                /*
                 * After successfully connecting to the real server, start reading requests 
                 * from the remote client
                 */
                connState.ActionAfterConnect = delegate()
                {
                    StartReadingRemote(state.Proxy, remoteClient, localClient);

                    //NetworkStream stream = remoteClient.GetStream();

                    //// Setup state
                    //ReadStreamState readState = new ReadStreamState()
                    //{
                    //    Callback = _readStreamCallback,
                    //    ReadStreamOwner = remoteClient,
                    //    ReadStream = stream,
                    //    Buffer = new byte[remoteClient.ReceiveBufferSize],
                    //    Offset = 0,
                    //    ClientToReport = localClient,
                    //    Proxy = state.Proxy
                    //};
                    
                    //// Being reading
                    //stream.BeginRead(
                    //        readState.Buffer,
                    //        readState.Offset,
                    //        readState.Buffer.Length,
                    //        readState.Callback,
                    //        readState);
                    ////---
                };

                /*
                 * Try to connect to the real server
                 */
                connState.Client.BeginConnect(
                        state.Proxy._target.Address,
                        state.Proxy._target.Port,
                        _connectCallback,
                        connState);
                // --                
            }
            catch (Exception e)
            {
                Console.WriteLine("EXCEPTION: TcpClientAccepted callback: {0}", e.Message);
                Console.ReadKey();
            }
        }



        private class ReadStreamState
        {

            public AsyncCallback Callback;

            public TcpClient ReadStreamOwner;
            public NetworkStream ReadStream;

            public byte[] Buffer;
            public int Offset;

            public TcpClient ClientToReport;

            public TcpProxy Proxy;
        }

        private static void StartWritingRemote(
                TcpProxy proxy, 
                TcpClient clientWriter, 
                TcpClient clientToReport, 
                byte[] data)
        {
            try
            {
                NetworkStream clientStream = null;
                try
                {
                    // Can raise InvalidOperationException if tcpclient is disconnected
                    clientStream = clientWriter.GetStream();
                    
                    // TODO: This might raise an exception, put in a try-catch
                    WriteStreamState writeState = new WriteStreamState()
                    {
                        Callback = _writeStreamCallback,
                        WriteStreamOwner = clientWriter,
                        WriteStream = clientStream,
                        Offset = 0,
                        Buffer = data,
                        ClientToReport = clientToReport,
                        Proxy = proxy
                    };

                    // Begin write. Resume reading will be done in the callback.
                    // Can raise IOException if tcpclient is disconnected
                    clientStream.BeginWrite(
                            writeState.Buffer,
                            writeState.Offset,
                            writeState.Buffer.Length,
                            writeState.Callback,
                            writeState);
                    //---
                }
                catch (Exception e)
                {
                    if (clientWriter.Connected)
                    {
                        Console.ReadKey();
                    }
                    else
                    {
                        // Connect new TcpClient, drop the current one, after connected, try to send again.
                        ConnectState connState = new ConnectState()
                        {
                            Callback = _connectCallback,
                            Client = new TcpClient()
                        };

                        connState.ActionAfterConnect = delegate()
                        {
                            StartWritingRemote(proxy, connState.Client, clientToReport, data);
                        };

                        // FIXME: Depends if writing is being done on proxy client or proxy server ...
                        connState.Client.BeginConnect(
                                proxy._target.Address,
                                proxy._target.Port,
                                connState.Callback,
                                connState);
                        //---
                    }
                }               
            }
            catch (Exception e)
            {
                Console.ReadKey();
            }
        }


        /// <summary>
        /// Ends reading to state.Stream; begins writing to state.ProxyClient.GetStream(); 
        /// Begins reading from state.Proxy.GetStream().
        /// </summary>
        /// <param name="res"></param>
        private static void ReadStream(IAsyncResult res)
        {
            try
            {
                ReadStreamState state = res.AsyncState as ReadStreamState;

                // End reading
                int count = state.ReadStream.EndRead(res);

                Console.WriteLine("TCP: End Read with (local: {0}, remote: {1}, count: {2})",
                        state.ReadStreamOwner.Client.LocalEndPoint,
                        state.ReadStreamOwner.Client.RemoteEndPoint,
                        count);
                //---

                if (count > 0)
                {
                    // Writing to proxy client.           
                    byte[] writeBuffer = new byte[count];

                    // Copy buffer
                    Array.Copy(state.Buffer, writeBuffer, count);

                    // Starts writing and waiting for response
                    StartWritingRemote(state.Proxy, state.ClientToReport, state.ReadStreamOwner, writeBuffer);
                }

                // Resume reading
                StartReadingRemote(state.Proxy, state.ReadStreamOwner, state.ClientToReport);

                //NetworkStream stream = state.ReadStreamOwner.GetStream();

                //state.Offset = 0;
                //state.Buffer = new byte[state.ReadStreamOwner.ReceiveBufferSize];
                //state.ReadStream = stream;

                //stream.BeginRead(
                //        state.Buffer,
                //        state.Offset,
                //        state.Buffer.Length - state.Offset,
                //        state.Callback,
                //        state);
                // // --
            }
            catch (Exception e)
            {
                Console.WriteLine("EXCEPTION: ReadStream callback: {0}", e.Message);
                Console.ReadKey();
            }
        }

        #endregion

        Protocol IProxy.GetProtocol()
        {
            return Protocol.TCP;
        }

        IPEndPoint IProxy.GetSource()
        {
            return _src;
        }

        IPEndPoint IProxy.GetTarget()
        {
            return _target;
        }

    }
}

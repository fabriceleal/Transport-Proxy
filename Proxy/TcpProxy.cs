using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

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
            // This connects to the server
            TcpClient proxyClient = new TcpClient();

            // This waits for incoming clients
            TcpListener proxyListener = new TcpListener(_src);
            proxyListener.Start();


            // Client setup
            proxyClient.BeginConnect(
                    _target.Address,
                    _target.Port,
                    _connectCallback,
                    new ConnectState()
                    {
                        Callback = _connectCallback,
                        Client = proxyClient
                    }
                    );
            // ---

            // Listener setup
            proxyListener.BeginAcceptTcpClient(
                    _acceptClientsCallback,
                    new TcpClientAcceptedState
                    {
                        Callback = _acceptClientsCallback,
                        ProxyListener = proxyListener,
                        ProxyClient = proxyClient
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
            ConnectState state = res.AsyncState as ConnectState;
            state.Client.EndConnect(res);

            if (state.ActionAfterConnect != null)
            {
                state.ActionAfterConnect();
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
        }

        public static void StartReadingRemote(
                TcpClient remoteClient,
                TcpClient localClient)
        {
            try
            {
                NetworkStream stream = remoteClient.GetStream();
                ReadStreamState readState = new ReadStreamState()
                {
                    Callback = _readStreamCallback,
                    ReadStreamOwner = remoteClient,
                    ClientToReport = localClient,
                    Buffer = new byte[remoteClient.ReceiveBufferSize],
                    Offset = 0,
                    ReadStream = stream
                };
                stream.BeginRead(
                        readState.Buffer,
                        readState.Offset,
                        readState.Buffer.Length,
                        readState.Callback,
                        readState);
                //--
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

                // Check if state.WriteStreamOwner is Connected.
                // if not, reconnect
                if (!state.WriteStreamOwner.Connected)
                {
                    ConnectState connState = new ConnectState()
                    {
                        Callback = _connectCallback,
                        Client = state.WriteStreamOwner,
                        ActionAfterConnect = delegate()
                        {
                            StartReadingRemote(state.WriteStreamOwner, state.ClientToReport);
                        }
                    };

                    state.WriteStreamOwner.BeginConnect(
                            "",
                            123,
                            _connectCallback,
                            connState);
                    //---
                }
                else
                {
                    StartReadingRemote(state.WriteStreamOwner, state.ClientToReport);

                    //NetworkStream stream = state.WriteStreamOwner.GetStream();
                    //ReadStreamState readState = new ReadStreamState()
                    //{
                    //    Callback = _readStreamCallback,
                    //    ReadStreamOwner = state.WriteStreamOwner,
                    //    ClientToReport = state.ClientToReport,
                    //    Buffer = new byte[state.WriteStreamOwner.ReceiveBufferSize],
                    //    Offset = 0,
                    //    ReadStream = stream
                    //};
                    //stream.BeginRead(
                    //        readState.Buffer,
                    //        readState.Offset,
                    //        readState.Buffer.Length,
                    //        readState.Callback,
                    //        readState);
                    ////--
                }
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

            /// <summary>
            /// The proxy client instance (local client)
            /// </summary>
            public TcpClient ProxyClient;
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

                // Get client and stream for reading incoming data
                TcpClient client = state.ProxyListener.EndAcceptTcpClient(res);

                Console.WriteLine("TCP: End Accept with (local: {0}, remote: {1})",
                        client.Client.LocalEndPoint, client.Client.RemoteEndPoint);
                // --

                NetworkStream stream = client.GetStream();

                // Setup state
                ReadStreamState readState = new ReadStreamState()
                {
                    Callback = _readStreamCallback,
                    ReadStreamOwner = client,
                    ReadStream = stream,
                    Buffer = new byte[client.ReceiveBufferSize],
                    Offset = 0,
                    ClientToReport = state.ProxyClient
                };

                // Being reading
                stream.BeginRead(
                        readState.Buffer,
                        readState.Offset,
                        readState.Buffer.Length,
                        readState.Callback,
                        readState);
                //---
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

                //if (count > 0)
                //{
                // Writing to proxy client.
                NetworkStream clientStream = state.ClientToReport.GetStream();
                byte[] writeBuffer = new byte[count];

                // Copy buffer
                Array.Copy(state.Buffer, writeBuffer, count);

                // Write to server
                WriteStreamState writeState = new WriteStreamState()
                {
                    Callback = _writeStreamCallback,
                    WriteStreamOwner = state.ClientToReport,
                    WriteStream = clientStream,
                    Offset = 0,
                    Buffer = writeBuffer,
                    ClientToReport = state.ReadStreamOwner
                };

                // Being write. Resume reading will be done in the callback.
                clientStream.BeginWrite(
                        writeState.Buffer,
                        writeState.Offset,
                        writeState.Buffer.Length,
                        writeState.Callback,
                        writeState);
                //---                    
                /*}
                else
                {
                    // Just resume reading
                    NetworkStream stream = state.ReadStreamOwner.GetStream();

                    state.Offset = 0;
                    state.Buffer = new byte[state.ReadStreamOwner.ReceiveBufferSize];
                    state.ReadStream = stream;

                    stream.BeginRead(
                            state.Buffer,
                            state.Offset,
                            state.Buffer.Length - state.Offset,
                            state.Callback,
                            state);
                    // --
                }*/
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

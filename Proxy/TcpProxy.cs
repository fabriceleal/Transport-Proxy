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
            TcpListener proxyListener = new TcpListener(_src);
            TcpClient proxyClient = new TcpClient(_target);
            
            
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
        }

        private static void Connect(IAsyncResult res)
        {
            ConnectState state = res.AsyncState as ConnectState;
            state.Client.EndConnect(res);
        }

        private class WriteStreamState
        {
            public AsyncCallback Callback;
            public TcpClient ProxyClient;
            public NetworkStream Stream;
            public byte[] Buffer;
            public int Offset;            
        }

        private static void WriteStream(IAsyncResult res)
        {
            WriteStreamState state = res.AsyncState as WriteStreamState;
            state.Stream.EndWrite(res);
        }
        
        #endregion

        #region "Listener"

        private static AsyncCallback _acceptClientsCallback = new AsyncCallback(TcpClientAccepted);
        private static AsyncCallback _readStreamCallback = new AsyncCallback(ReadStream);

        private class TcpClientAcceptedState
        {
            public AsyncCallback Callback;
            public TcpListener ProxyListener;
            public TcpClient ProxyClient;
        }

        private static void TcpClientAccepted(IAsyncResult res)
        {
            TcpClientAcceptedState state = res.AsyncState as TcpClientAcceptedState;

            // Re-regist listener asap
            state.ProxyListener.BeginAcceptTcpClient(state.Callback, state.ProxyListener);

            // Get client and stream for reading incoming data
            TcpClient client = state.ProxyListener.EndAcceptTcpClient(res);
            NetworkStream stream = client.GetStream();

            // Setup state
            ReadStreamState readState = new ReadStreamState()
            {
                Callback = _readStreamCallback,
                Client = client,
                Stream = stream,
                Buffer = new byte[client.ReceiveBufferSize],
                Offset = 0,
                ProxyClient = state.ProxyClient
            };

            // Being reading
            stream.BeginRead(
                    readState.Buffer,
                    readState.Offset,
                    readState.Buffer.Length,
                    readState.Callback,
                    readState);
            //--
        }

        private class ReadStreamState
        {
            public AsyncCallback Callback;
            public TcpClient Client;
            public NetworkStream Stream;
            public byte[] Buffer;
            public int Offset;

            public TcpClient ProxyClient;
        }

        private static void ReadStream(IAsyncResult res)
        {
            ReadStreamState state = res.AsyncState as ReadStreamState;

            // End reading
            int count = state.Stream.EndRead(res);
            if (count > 0)
            {   
                // Writing to proxy client.
                NetworkStream clientStream = state.ProxyClient.GetStream();
                byte[] writeBuffer = new byte[count];
                WriteStreamState writeState = new WriteStreamState(){
                    Callback = _writeStreamCallback,
                    ProxyClient = state.ProxyClient,
                    Stream = clientStream,
                    Buffer = writeBuffer,
                    Offset = 0
                };

                clientStream.BeginWrite(
                        writeState.Buffer,
                        writeState.Offset,
                        writeState.Buffer.Length,
                        writeState.Callback,
                        writeState);
                //---
            }

            // Resume reading
            NetworkStream stream = state.Client.GetStream();

            state.Offset = 0;
            state.Buffer = new byte[state.Client.ReceiveBufferSize];
            state.Stream = stream;

            stream.BeginRead(
                    state.Buffer,
                    state.Offset,
                    state.Buffer.Length - state.Offset,
                    state.Callback,
                    state);
            // --
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

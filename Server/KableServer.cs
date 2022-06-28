using System;
using System.Net;
using System.Net.Sockets;

using KableNet.Common;

namespace KableNet.Server
{
    /// <summary>
    ///     Server Listener for KableNet
    /// </summary>
    public class KableServer
    {


        /// <summary>
        ///     Initializes a KableNet Server on the specified port bound to "0.0.0.0"
        ///     and starts listening.
        /// </summary>
        /// <param name="port"></param>
        public KableServer( int port )
        {
            Port = port;
            _tcpServerSocket = new Socket( AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp );
            _tcpServerSocket.Bind( new IPEndPoint( IPAddress.Any, Port ) );
            _tcpServerSocket.NoDelay = true;
            _tcpServerSocket.Listen( 10 );
        }

        public void StartListening( )
        {
            StartTcpAccept( );
        }

        private void OnTcpAcceptCallback( IAsyncResult ar )
        {
            try
            {
                Socket sock = _tcpServerSocket.EndAccept( ar );
                if ( sock != null )
                {
                    KableConnection conn = new KableConnection( sock, this );

                    NewConnectionEvent?.Invoke( conn );
                }
                else
                {
                    throw new Exception( "KableServer.OnTcpAcceptCallback sock was null!" );
                }

                StartTcpAccept( );
            }
            catch ( SocketException ex )
            {
                NewConnectionErroredEvent?.Invoke( $"[SocketException]New Connection Error'd!\n{ex}" );
                throw;
            }
            catch ( Exception ex )
            {
                NewConnectionErroredEvent?.Invoke( $"[Exception]New Connection Error'd!\n{ex}" );
                throw;
            }
        }
        
        private void StartTcpAccept( )
        {
            _tcpServerSocket.BeginAccept( OnTcpAcceptCallback, _tcpServerSocket );
        }

        public int Port { get; private set; } = -1;
        readonly private Socket _tcpServerSocket;
        
        public delegate void NewConnection( KableConnection connection );
        public event NewConnection NewConnectionEvent;

        public delegate void NewConnectionSocketError( string errorMessage );
        public event NewConnectionSocketError NewConnectionErroredEvent;
    }
}

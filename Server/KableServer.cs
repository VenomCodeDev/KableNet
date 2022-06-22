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

        public delegate void NewConnection( KableConnection connection );

        public delegate void NewConnectionSocketError( string errorMessage );

        readonly private Socket _socket;
        /// <summary>
        ///     Initializes a KableNet Server on the specified port bound to "0.0.0.0"
        ///     and starts listening.
        /// </summary>
        /// <param name="port"></param>
        public KableServer( int port )
        {
            Port = port;
            _socket = new Socket( AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp );
            _socket.Bind( new IPEndPoint( IPAddress.Any, Port ) );
            _socket.NoDelay = true;
            _socket.Listen( 10 );
        }

        public void StartListening( )
        {
            StartTcpAccept( );
        }

        private void OnTcpAcceptCallback( IAsyncResult ar )
        {
            try
            {
                Socket sock = _socket.EndAccept( ar );
                if ( sock != null )
                {
                    KableConnection conn = new KableConnection( sock, this );

                    NewConnectionEvent?.Invoke( conn );
                }
                else
                {
                    throw new Exception( "KableServer.OnTcpAcceptCallback sock was null!" );
                }
                // If its null, just continue the loop I guess?
                // Im not sure what would cause that situation, so ill deal
                // with it when/if it happens.

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
            _socket.BeginAccept( OnTcpAcceptCallback, _socket );
        }

        public int Port { get; private set; } = -1;
        
        public event NewConnection NewConnectionEvent;
        public event NewConnectionSocketError NewConnectionErroredEvent;
    }
}

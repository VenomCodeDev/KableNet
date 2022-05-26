using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace KableNet.Common
{
    /// <summary>
    ///     Represents a Kable-Networking connection. Both Client AND Server side will have this sort
    ///     of instance for communication.
    /// </summary>
    public class KableConnection
    {
        public int maxProcessIterations = 5;

        /// <summary>
        ///     ClientSide way to get a KableConnection instance.
        ///     Make sure to call Connect()!
        /// </summary>
        /// <param name="address">Address to connect to</param>
        /// <param name="port">Port to connect to</param>
        public KableConnection( IPAddress address, int port )
        {
            this.address = address;
            this.port = port;

            tcpSocket = new Socket( AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp );
            tcpSocket.NoDelay = true;
            _tcpBuffer = new byte[ SizeHelper.Normal ];

            udpSocket = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp );
            udpSocket.SetSocketOption( SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true );
            udpSocket.Bind( new IPEndPoint( IPAddress.Any, port + 1 ) );
            _udpBuffer = new byte[ SizeHelper.Normal ];

            connected = false;
        }
        /// <summary>
        ///     ServerSide way to get a KableConnection instance.
        /// </summary>
        /// <param name="activeTCPSocket">The raw TCP Socket.</param>
        internal KableConnection( Socket activeTCPSocket )
        {
            tcpSocket = activeTCPSocket;

            connected = true;

            address = null;
            connected = true;
            _tcpBuffer = new byte[ SizeHelper.Normal ];

            udpSocket = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp );
            udpSocket.SetSocketOption( SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true );
            udpSocket.Bind( new IPEndPoint( IPAddress.Any, port + 1 ) );
            _udpBuffer = new byte[ SizeHelper.Normal ];

            BeginRecieveTCP( );
            BeginRecieveUDP( );
        }

        public bool backgroundProcessing { get; private set; }

        /// <summary>
        ///     a
        ///     Only call this if you are ClientSided. ServerSided already
        ///     handles this via the client's connection.
        /// </summary>
        public void Connect( )
        {
            if ( closed )
                return;
            try
            {
                tcpSocket.BeginConnect( new IPEndPoint( address, port ), ConnectCallback, null );
            }
            catch ( SocketException ex )
            {
                ConnectErroredEvent?.Invoke( ex, this );
                connected = false;
            }
            catch ( Exception ex )
            {
                ConnectionErroredEvent?.Invoke( ex, this );
                connected = false;
            }
        }

        /// <summary>
        ///     Enables background thread processing of the packets
        ///     WARNING: Does NOT work with Unity3D Engine
        /// </summary>
        public void EnableBackgroundProcessing( )
        {
            backgroundProcessing = true;
        }

        /// <summary>
        ///     Starts the Async Callback for reading TCP data from the socket
        /// </summary>
        private void BeginRecieveTCP( )
        {
            if ( closed )
                return;
            try
            {
                if ( _tcpPendingPacket is null )
                {
                    _tcpBuffer = new byte[ SizeHelper.Normal ];
                }
                else
                {
                    _tcpBuffer = new byte[ _tcpPendingPacket.payloadSize ];
                }
                tcpSocket.BeginReceive( _tcpBuffer, 0, _tcpBuffer.Length, SocketFlags.None, OnTCPRecvCallback, null );
            }
            catch ( SocketException ex )
            {
                ConnectErroredEvent?.Invoke( ex, this );
                connected = false;
            }
            catch ( Exception ex )
            {
                ConnectionErroredEvent?.Invoke( ex, this );
                connected = false;
            }
        }

        /// <summary>
        ///     Starts the Async Callback for reading UDP data from the socket
        /// </summary>
        private void BeginRecieveUDP( )
        {
            if ( closed )
                return;
            try
            {
                if ( _udpPendingPacket is null )
                {
                    _udpBuffer = new byte[ SizeHelper.Normal ];
                }
                else
                {
                    _udpBuffer = new byte[ _udpPendingPacket.payloadSize ];
                }
                udpSocket.BeginReceive( _udpBuffer, 0, _udpBuffer.Length, SocketFlags.None, OnUDPRecvCallback, null );
            }
            catch ( SocketException ex )
            {
                ConnectErroredEvent?.Invoke( ex, this );
                connected = false;
            }
            catch ( Exception ex )
            {
                ConnectionErroredEvent?.Invoke( ex, this );
                connected = false;
            }
        }

        /// <summary>
        ///     Used ClientSide for completing the Async connection to the TCP server
        /// </summary>
        /// <param name="AR"></param>
        private void ConnectCallback( IAsyncResult AR )
        {
            if ( closed )
                return;
            try
            {
                tcpSocket.EndConnect( AR );
                connected = true;
                ConnectedEvent?.Invoke( this );
            }
            catch ( SocketException ex )
            {
                ConnectErroredEvent?.Invoke( ex, this );
                connected = false;
            }
            catch ( Exception ex )
            {
                ConnectionErroredEvent?.Invoke( ex, this );
                connected = false;
            }

            if ( connected )
            {
                BeginRecieveTCP( );
                BeginRecieveUDP( );
            }
        }

        public void SendPacketTCP( KablePacket packet )
        {
            if ( closed )
                return;
            SendPacketTCPAsync( packet ).Wait( );
        }
        public void SendPacketTCP( List<byte> packetBuffer )
        {
            if ( closed )
                return;
            SendPacketTCPAsync( packetBuffer ).Wait( );
        }

        public async Task SendPacketTCPAsync( KablePacket packet )
        {
            if ( closed )
                return;
            await SendPacketTCPAsync( packet.GetRaw( ) );
        }

        /// <summary>
        ///     Sends a TCP buffer through the KableConnection to the recieving end.
        /// </summary>
        /// <param name="packetBuffer">Bytes to send</param>
        /// <returns></returns>
        public async Task SendPacketTCPAsync( List<byte> packetBuffer )
        {
            if ( closed )
                return;
            try
            {
                if ( tcpSocket != null && connected )
                {
                    List<byte> sendBuffer = new List<byte>( );

                    // Get the amount of bytes as a UInt and convert it to bytes.
                    // This goes as a suffix to our actual payload btyes to tell the
                    // recieving end how many bytes we're sending.
                    sendBuffer.AddRange( BitConverter.GetBytes( (uint)packetBuffer.Count ) );
                    sendBuffer.AddRange( packetBuffer );

                    byte[ ] sendBufferArray = sendBuffer.ToArray( );

                    // Make sure we account for differences in LittleEdian!
                    if ( !BitConverter.IsLittleEndian )
                    {
                        Array.Reverse( sendBufferArray );
                    }

                    await Task.Run( ( ) => tcpSocket.Send( sendBufferArray ) );
                }
            }
            catch ( SocketException ex )
            {
                ConnectErroredEvent?.Invoke( ex, this );
                connected = false;
            }
            catch ( Exception ex )
            {
                ConnectionErroredEvent?.Invoke( ex, this );
                connected = false;
            }
        }


        public void SendPacketUDP( KablePacket packet )
        {
            if ( closed )
                return;
            SendPacketUDPAsync( packet ).Wait( );
        }
        public void SendPacketUDP( List<byte> packetBuffer )
        {
            if ( closed )
                return;
            SendPacketUDPAsync( packetBuffer ).Wait( );
        }

        public async Task SendPacketUDPAsync( KablePacket packet )
        {
            if ( closed )
                return;
            await SendPacketUDPAsync( packet.GetRaw( ) );
        }

        /// <summary>
        ///     Sends a TCP buffer through the KableConnection to the recieving end.
        /// </summary>
        /// <param name="packetBuffer">Bytes to send</param>
        /// <returns></returns>
        public async Task SendPacketUDPAsync( List<byte> packetBuffer )
        {
            if ( closed )
                return;
            try
            {
                if ( udpSocket != null && connected )
                {
                    List<byte> sendBuffer = new List<byte>( );

                    // Get the amount of bytes as a UInt and convert it to bytes.
                    // This goes as a suffix to our actual payload btyes to tell the
                    // recieving end how many bytes we're sending.
                    sendBuffer.AddRange( BitConverter.GetBytes( (uint)packetBuffer.Count ) );
                    sendBuffer.AddRange( packetBuffer );

                    byte[ ] sendBufferArray = sendBuffer.ToArray( );

                    // Make sure we account for differences in LittleEdian!
                    if ( !BitConverter.IsLittleEndian )
                    {
                        Array.Reverse( sendBufferArray );
                    }

                    await Task.Run( ( ) => udpSocket.Send( sendBufferArray ) );
                }
            }
            catch ( SocketException ex )
            {
                ConnectErroredEvent?.Invoke( ex, this );
                connected = false;
            }
            catch ( Exception ex )
            {
                ConnectionErroredEvent?.Invoke( ex, this );
                connected = false;
            }
        }

        /// <summary>
        ///     Callback for the Async TCP reading
        /// </summary>
        /// <param name="ar"></param>
        private void OnTCPRecvCallback( IAsyncResult ar )
        {
            if ( closed )
                return;
            try
            {
                if ( tcpSocket != null )
                {
                    int bytesRead = tcpSocket.EndReceive( ar );

                    // Make sure we account for differences in LittleEdian!
                    if ( !BitConverter.IsLittleEndian )
                    {
                        Array.Reverse( _tcpBuffer );
                    }

                    // Check that we actually read something, otherwise error
                    if ( bytesRead > 0 )
                    {
                        lock ( _tcpPacketBuffer )
                        {
                            // Get only the read bytes; ignore the excess data
                            // I dont know if this is needed, but ill remove later if its not.
                            _tcpPacketBuffer.AddRange( new List<byte>( _tcpBuffer ).GetRange( 0, bytesRead ) );
                        }
                    }
                    else
                    {
                        // We didnt read any data. Assume the connection was terminated and
                        // throw a error for it.
                        ConnectionErroredEvent?.Invoke( new Exception( "[KableConnection_Error]Connection was lost: Read zero bytes!" ), this );
                        connected = false;
                    }
                }
            }
            catch ( SocketException ex )
            {
                ConnectErroredEvent?.Invoke( ex, this );
                connected = false;
            }
            catch ( Exception ex )
            {
                ConnectionErroredEvent?.Invoke( ex, this );
                connected = false;
            }

            if ( connected && !closed )
            {
                if ( backgroundProcessing )
                {
                    ProcessBuffer( );
                }

                BeginRecieveTCP( );
            }
        }

        /// <summary>
        ///     Callback for the Async UDP reading
        /// </summary>
        /// <param name="ar"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void OnUDPRecvCallback( IAsyncResult ar )
        {
            if ( closed )
                return;
            try
            {
                if ( udpSocket != null )
                {
                    int bytesRead = udpSocket.EndReceive( ar );

                    // Make sure we account for differences in LittleEdian!
                    if ( !BitConverter.IsLittleEndian )
                    {
                        Array.Reverse( _udpBuffer );
                    }

                    // Check that we actually read something, otherwise error
                    if ( bytesRead > 0 )
                    {
                        lock ( _tcpPacketBuffer )
                        {
                            // Get only the read bytes; ignore the excess data
                            // I dont know if this is needed, but ill remove later if its not.
                            _tcpPacketBuffer.AddRange( new List<byte>( _udpBuffer ).GetRange( 0, bytesRead ) );
                        }
                    }
                    else
                    {
                        // We didnt read any data. Assume the connection was terminated and
                        // throw a error for it.
                        ConnectionErroredEvent?.Invoke( new Exception( "[KableConnection_Error]Connection was lost: Read zero bytes!" ), this );
                        connected = false;
                    }
                }
            }
            catch ( SocketException ex )
            {
                ConnectErroredEvent?.Invoke( ex, this );
                connected = false;
            }
            catch ( Exception ex )
            {
                ConnectionErroredEvent?.Invoke( ex, this );
                connected = false;
            }

            if ( connected && !closed )
            {
                if ( backgroundProcessing )
                {
                    ProcessBuffer( );
                }

                BeginRecieveUDP( );
            }
        }

        private ProcessedResultType ProcessBufferTCP( )
        {
            if ( _tcpPacketBuffer.Count <= 0 || closed )
                return ProcessedResultType.EXIT;

            if ( _tcpPendingPacket is null )
            {
                if ( _tcpPacketBuffer.Count >= SizeHelper.Normal )
                {
                    int newPayloadSize = -1;

                    lock ( _tcpPacketBuffer )
                    {
                        newPayloadSize = BitConverter.ToInt32( _tcpPacketBuffer.ToArray( ), 0 );
                    }

                    _tcpPendingPacket = new PendingPacket
                    {
                        payloadSize = newPayloadSize,
                    };
                    // Change tmpBuffer to the suffix of data after our "Payload Size" marker
                    lock ( _tcpPacketBuffer )
                    {
                        _tcpPacketBuffer = _tcpPacketBuffer.GetRange( SizeHelper.Normal, _tcpPacketBuffer.Count - SizeHelper.Normal );
                    }
                }
                else
                {
                    // If this executes then we have no pending packet
                    // AND the read size is too small to tell us the
                    // size of a new pending packet. Continue on next
                    // iteration and break the loop so we can check again next iteration.
                    return ProcessedResultType.EXIT;
                }
            }

            if ( _tcpPacketBuffer.Count >= _tcpPendingPacket.payloadSize )
            {
                // Add current buffer's data to the pendingPacket to fill it up more
                lock ( _tcpPacketBuffer )
                {
                    _tcpPendingPacket.currentPayload.AddRange( _tcpPacketBuffer.GetRange( 0, _tcpPendingPacket.payloadSize ) );
                }
                // Check if the pendingPacket is full...
                if ( _tcpPendingPacket.currentPayload.Count >= _tcpPendingPacket.payloadSize )
                {
                    // its full! Raise the event and then if we have enough data, repeat this loop.
                    try
                    {
                        PacketReadyEvent?.Invoke( new KablePacket( _tcpPendingPacket.currentPayload ), this );
                    }
                    catch ( Exception ex )
                    {
                        // Crash from a subscriber to the event.
                        // Not sure how to handle these, so for now just ignore it and continue?
                        // Will try to figure out a better solution later, of course
                        ConnectionErroredEvent?.Invoke( ex, this );
                    }

                    lock ( _tcpPacketBuffer )
                    {
                        //_tcpPacketBuffer = _tcpPacketBuffer.GetRange( _tcpPendingPacket.payloadSize, _tcpPacketBuffer.Count - _tcpPendingPacket.payloadSize );
                        _tcpPacketBuffer.Clear( );
                    }

                    _tcpPendingPacket = null;
                }
            }

            return ProcessedResultType.CONTINUE;
        }

        private ProcessedResultType ProcessBufferUDP( )
        {
            if ( _udpPacketBuffer.Count <= 0 || closed )
                return ProcessedResultType.EXIT;

            if ( _udpPendingPacket is null )
            {
                if ( _udpPacketBuffer.Count >= SizeHelper.Normal )
                {
                    int newPayloadSize = -1;

                    lock ( _udpPacketBuffer )
                    {
                        newPayloadSize = BitConverter.ToInt32( _udpPacketBuffer.ToArray( ), 0 );
                    }

                    _udpPendingPacket = new PendingPacket
                    {
                        payloadSize = newPayloadSize,
                    };
                    // Change tmpBuffer to the suffix of data after our "Payload Size" marker
                    lock ( _udpPacketBuffer )
                    {
                        //_udpPacketBuffer = _udpPacketBuffer.GetRange( SizeHelper.Normal, _udpPacketBuffer.Count - SizeHelper.Normal );
                        _udpPacketBuffer.Clear( );
                    }
                }
                else
                {
                    // If this executes then we have no pending packet
                    // AND the read size is too small to tell us the
                    // size of a new pending packet. Continue on next
                    // iteration and break the loop so we can check again next iteration.
                    return ProcessedResultType.EXIT;
                }
            }

            if ( _udpPacketBuffer.Count >= _udpPendingPacket.payloadSize )
            {
                // Add current buffer's data to the pendingPacket to fill it up more
                lock ( _udpPacketBuffer )
                {
                    _udpPendingPacket.currentPayload.AddRange( _udpPacketBuffer.GetRange( 0, _udpPendingPacket.payloadSize ) );
                }
                // Check if the pendingPacket is full...
                if ( _udpPendingPacket.currentPayload.Count >= _udpPendingPacket.payloadSize )
                {
                    // its full! Raise the event and then if we have enough data, repeat this loop.
                    try
                    {
                        PacketReadyEvent?.Invoke( new KablePacket( _udpPendingPacket.currentPayload ), this );
                    }
                    catch ( Exception ex )
                    {
                        // Crash from a subscriber to the event.
                        // Not sure how to handle these, so for now just ignore it and continue?
                        // Will try to figure out a better solution later, of course
                        ConnectionErroredEvent?.Invoke( ex, this );
                    }

                    lock ( _udpPacketBuffer )
                    {
                        _udpPacketBuffer = _udpPacketBuffer.GetRange( _udpPendingPacket.payloadSize,
                            _udpPacketBuffer.Count - _udpPendingPacket.payloadSize );
                    }

                    _tcpPendingPacket = null;
                }
            }

            return ProcessedResultType.CONTINUE;
        }

        /// <summary>
        ///     Process's the entire network buffer(to an extent) and
        ///     triggeres events for the processed packets.
        ///     You MUST call this in order to recieve
        ///     packet data events!
        /// </summary>
        public void ProcessBuffer( )
        {
            if ( closed )
                return;
            int againCount;

            // Dont rerun these loops more than maxProcessIterations times.

            againCount = 0;
            // TCP
            while ( againCount < maxProcessIterations )
            {
                if ( closed )
                    return;
                againCount++;

                ProcessedResultType result = ProcessBufferTCP( );
                if ( result is ProcessedResultType.EXIT )
                    break;
            }

            againCount = 0;
            // UDP
            while ( againCount < maxProcessIterations )
            {
                if ( closed )
                    return;
                againCount++;

                ProcessedResultType result = ProcessBufferUDP( );
                if ( result is ProcessedResultType.EXIT )
                    break;
            }
        }

        public void Close( )
        {
            
            closed = true;
            try
            {
                tcpSocket.Close( );
            }
            catch { }

            try
            {
                udpSocket.Close( );
            }
            catch { }
        }

        public bool connected { get; private set; }
        public bool closed { get; private set; } = false;
        public Socket tcpSocket { get; }
        public Socket udpSocket { get; }
        public IPAddress address { get; }
        public int port { get; }

        private byte[ ] _tcpBuffer;
        private List<byte> _tcpPacketBuffer = new List<byte>( );
        private PendingPacket _tcpPendingPacket;

        private byte[ ] _udpBuffer;
        private List<byte> _udpPacketBuffer = new List<byte>( );
        private PendingPacket _udpPendingPacket;
        
        public delegate void KableConnected( KableConnection source );
        public event KableConnected ConnectedEvent;
        public delegate void KableConnectErrored( SocketException exception, KableConnection source );
        public event KableConnectErrored ConnectErroredEvent;

        public delegate void KableConnectionErrored( Exception ex, KableConnection source );
        public event KableConnectionErrored ConnectionErroredEvent;

        public delegate void KablePacketReady( KablePacket packet, KableConnection source );
        public event KablePacketReady PacketReadyEvent;

        /// <summary>
        ///     Used for simple data storage about the current "pending packet" while
        ///     we wait for it to fill.
        /// </summary>
        private class PendingPacket
        {
            public int payloadSize { get; set; }
            public List<byte> currentPayload { get; } = new List<byte>( );
        }
    }
}

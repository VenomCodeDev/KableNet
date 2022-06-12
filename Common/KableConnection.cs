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
            this.Address = address;
            this.Port = port;

            TcpSocket = new Socket( AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp );
            TcpSocket.NoDelay = true;
            _tcpBuffer = new byte[ SizeHelper.Normal ];

            UdpSocket = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp );
            UdpSocket.SetSocketOption( SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true );
            UdpSocket.Bind( new IPEndPoint( IPAddress.Any, port + 1 ) );
            _udpBuffer = new byte[ SizeHelper.Normal ];

            Connected = false;
        }
        /// <summary>
        ///     ServerSide way to get a KableConnection instance.
        /// </summary>
        /// <param name="activeTcpSocket">The raw Tcp Socket.</param>
        internal KableConnection( Socket activeTcpSocket )
        {
            TcpSocket = activeTcpSocket;

            Connected = true;

            Address = null;
            Connected = true;
            _tcpBuffer = new byte[ SizeHelper.Normal ];

            UdpSocket = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp );
            UdpSocket.SetSocketOption( SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true );
            UdpSocket.Bind( new IPEndPoint( IPAddress.Any, Port + 1 ) );
            _udpBuffer = new byte[ SizeHelper.Normal ];

            BeginRecieveTcp( );
            BeginRecieveUdp( );
        }

        public bool BackgroundProcessing { get; private set; }

        /// <summary>
        ///     a
        ///     Only call this if you are ClientSided. ServerSided already
        ///     handles this via the client's connection.
        /// </summary>
        public void Connect( )
        {
            if ( Closed )
                return;
            try
            {
                TcpSocket.BeginConnect( new IPEndPoint( Address, Port ), ConnectCallback, null );
            }
            catch ( SocketException ex )
            {
                ConnectErroredEvent?.Invoke( ex, this );
                Connected = false;
            }
            catch ( Exception ex )
            {
                ConnectionErroredEvent?.Invoke( ex, this );
                Connected = false;
            }
        }

        /// <summary>
        ///     Enables background thread processing of the packets
        ///     WARNING: Does NOT work with Unity3D Engine
        /// </summary>
        public void EnableBackgroundProcessing( )
        {
            BackgroundProcessing = true;
        }

        /// <summary>
        ///     Starts the Async Callback for reading Tcp data from the socket
        /// </summary>
        private void BeginRecieveTcp( )
        {
            if ( Closed )
                return;
            try
            {
                if ( _tcpPendingPacket is null )
                {
                    _tcpBuffer = new byte[ SizeHelper.Normal ];
                }
                else
                {
                    _tcpBuffer = new byte[ _tcpPendingPacket.PayloadSize ];
                }
                TcpSocket.BeginReceive( _tcpBuffer, 0, _tcpBuffer.Length, SocketFlags.None, OnTcpRecvCallback, null );
            }
            catch ( SocketException ex )
            {
                ConnectErroredEvent?.Invoke( ex, this );
                Connected = false;
            }
            catch ( Exception ex )
            {
                ConnectionErroredEvent?.Invoke( ex, this );
                Connected = false;
            }
        }

        /// <summary>
        ///     Starts the Async Callback for reading Udp data from the socket
        /// </summary>
        private void BeginRecieveUdp( )
        {
            if ( Closed )
                return;
            try
            {
                if ( _udpPendingPacket is null )
                {
                    _udpBuffer = new byte[ SizeHelper.Normal ];
                }
                else
                {
                    _udpBuffer = new byte[ _udpPendingPacket.PayloadSize ];
                }
                UdpSocket.BeginReceive( _udpBuffer, 0, _udpBuffer.Length, SocketFlags.None, OnUdpRecvCallback, null );
            }
            catch ( SocketException ex )
            {
                ConnectErroredEvent?.Invoke( ex, this );
                Connected = false;
            }
            catch ( Exception ex )
            {
                ConnectionErroredEvent?.Invoke( ex, this );
                Connected = false;
            }
        }

        /// <summary>
        ///     Used ClientSide for completing the Async connection to the Tcp server
        /// </summary>
        /// <param name="asyncResult"></param>
        private void ConnectCallback( IAsyncResult asyncResult )
        {
            if ( Closed )
                return;
            try
            {
                TcpSocket.EndConnect( asyncResult );
                Connected = true;
                ConnectedEvent?.Invoke( this );
            }
            catch ( SocketException ex )
            {
                ConnectErroredEvent?.Invoke( ex, this );
                Connected = false;
            }
            catch ( Exception ex )
            {
                ConnectionErroredEvent?.Invoke( ex, this );
                Connected = false;
            }

            if ( Connected && !Closed )
            {
                BeginRecieveTcp( );
                BeginRecieveUdp( );
            }
        }

        public void SendPacketTc( KablePacket packet )
        {
            if ( Closed )
                return;
            SendPacketTcpAsync( packet ).Wait( );
        }
        public void SendPacketTcp( List<byte> packetBuffer )
        {
            if ( Closed )
                return;
            SendPacketTcpAsync( packetBuffer ).Wait( );
        }

        public async Task SendPacketTcpAsync( KablePacket packet )
        {
            if ( Closed )
                return;
            await SendPacketTcpAsync( packet.GetRaw( ) );
        }

        /// <summary>
        ///     Sends a Tcp buffer through the KableConnection to the recieving end.
        /// </summary>
        /// <param name="packetBuffer">Bytes to send</param>
        /// <returns></returns>
        public async Task SendPacketTcpAsync( List<byte> packetBuffer )
        {
            if ( Closed )
                return;
            try
            {
                if ( TcpSocket != null && Connected )
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

                    // I think I misunderstood how ASYNC works with Task.Run and,
                    // after I implemented this, the method started hanging here 
                    //await Task.Run( ( ) => tcpSocket.Send( sendBufferArray ) );
                    // Trying, instead, to do the following:                    (Yes, I know it needs to be optimized. SORRY!)
                    ArraySegment<Byte> arraySeg = new ArraySegment<byte>( sendBufferArray );
                    await TcpSocket.SendAsync( arraySeg, SocketFlags.None );
                }
            }
            catch ( SocketException ex )
            {
                ConnectErroredEvent?.Invoke( ex, this );
                Connected = false;
            }
            catch ( Exception ex )
            {
                ConnectionErroredEvent?.Invoke( ex, this );
                Connected = false;
            }
        }


        public void SendPacketUdp( KablePacket packet )
        {
            if ( Closed )
                return;
            SendPacketUdpAsync( packet ).Wait( );
        }
        public void SendPacketUdp( List<byte> packetBuffer )
        {
            if ( Closed )
                return;
            SendPacketUdpAsync( packetBuffer ).Wait( );
        }

        public async Task SendPacketUdpAsync( KablePacket packet )
        {
            if ( Closed )
                return;
            await SendPacketUdpAsync( packet.GetRaw( ) );
        }

        /// <summary>
        ///     Sends a Tcp buffer through the KableConnection to the recieving end.
        /// </summary>
        /// <param name="packetBuffer">Bytes to send</param>
        /// <returns></returns>
        public async Task SendPacketUdpAsync( List<byte> packetBuffer )
        {
            if ( Closed )
                return;
            try
            {
                if ( UdpSocket != null && Connected )
                {
                    List<byte> sendBuffer = new List<byte>( );

                    // Get the amount of bytes as a UInt and convert it to bytes.
                    // This goes as a suffix to our actual payload bytes to tell the
                    // recieving end how many bytes we're sending.
                    sendBuffer.AddRange( BitConverter.GetBytes( (uint)packetBuffer.Count ) );
                    sendBuffer.AddRange( packetBuffer );

                    byte[ ] sendBufferArray = sendBuffer.ToArray( );

                    // Make sure we account for differences in LittleEndian!
                    if ( !BitConverter.IsLittleEndian )
                    {
                        Array.Reverse( sendBufferArray );
                    }

                    ArraySegment<Byte> arraySeg = new ArraySegment<byte>( sendBufferArray );
                    await UdpSocket.SendAsync( arraySeg, SocketFlags.None );
                }
            }
            catch ( SocketException ex )
            {
                ConnectErroredEvent?.Invoke( ex, this );
                Connected = false;
            }
            catch ( Exception ex )
            {
                ConnectionErroredEvent?.Invoke( ex, this );
                Connected = false;
            }
        }

        /// <summary>
        ///     Callback for the Async Tcp reading
        /// </summary>
        /// <param name="ar"></param>
        private void OnTcpRecvCallback( IAsyncResult ar )
        {
            if ( Closed )
                return;
            try
            {
                if ( TcpSocket != null )
                {
                    int bytesRead = TcpSocket.EndReceive( ar );

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
                        Connected = false;
                    }
                }
            }
            catch ( SocketException ex )
            {
                ConnectErroredEvent?.Invoke( ex, this );
                Connected = false;
            }
            catch ( Exception ex )
            {
                ConnectionErroredEvent?.Invoke( ex, this );
                Connected = false;
            }

            if ( Connected && !Closed )
            {
                if ( BackgroundProcessing )
                {
                    ProcessBuffer( );
                }

                BeginRecieveTcp( );
            }
        }

        /// <summary>
        ///     Callback for the Async Udp reading
        /// </summary>
        /// <param name="ar"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void OnUdpRecvCallback( IAsyncResult ar )
        {
            if ( Closed )
                return;
            try
            {
                if ( UdpSocket != null )
                {
                    int bytesRead = UdpSocket.EndReceive( ar );

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
                        Connected = false;
                    }
                }
            }
            catch ( SocketException ex )
            {
                ConnectErroredEvent?.Invoke( ex, this );
                Connected = false;
            }
            catch ( Exception ex )
            {
                ConnectionErroredEvent?.Invoke( ex, this );
                Connected = false;
            }

            if ( Connected && !Closed )
            {
                if ( BackgroundProcessing )
                {
                    ProcessBuffer( );
                }

                BeginRecieveUdp( );
            }
        }

        private ProcessedResultType ProcessBufferTcp( )
        {
            int tcpPacketBuffCount = -1;

            lock ( _tcpPacketBuffer )
            {
                tcpPacketBuffCount = _tcpPacketBuffer.Count;
            }
            
            if ( tcpPacketBuffCount <= 0 || Closed )
                return ProcessedResultType.EXIT;

            if ( _tcpPendingPacket is null )
            {
                if ( tcpPacketBuffCount >= SizeHelper.Normal )
                {
                    int newPayloadSize = -1;

                    lock ( _tcpPacketBuffer )
                    {
                        newPayloadSize = BitConverter.ToInt32( _tcpPacketBuffer.ToArray( ), 0 );
                    }

                    _tcpPendingPacket = new PendingPacket
                    {
                        PayloadSize = newPayloadSize,
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

            if ( tcpPacketBuffCount >= _tcpPendingPacket.PayloadSize )
            {
                // Add current buffer's data to the pendingPacket to fill it up more
                lock ( _tcpPacketBuffer )
                {
                    _tcpPendingPacket.CurrentPayload.AddRange( _tcpPacketBuffer.GetRange( 0, _tcpPendingPacket.PayloadSize ) );
                }
                // Check if the pendingPacket is full...
                if ( _tcpPendingPacket.CurrentPayload.Count >= _tcpPendingPacket.PayloadSize )
                {
                    // its full! Raise the event and then if we have enough data, repeat this loop.
                    try
                    {
                        PacketReadyEvent?.Invoke( new KablePacket( _tcpPendingPacket.CurrentPayload ), this );
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

        private ProcessedResultType ProcessBufferUdp( )
        {
            int udpPacketBuffCount = -1;

            lock ( _udpPacketBuffer )
            {
                udpPacketBuffCount = _udpPacketBuffer.Count;
            }
            
            if ( udpPacketBuffCount <= 0 || Closed )
                return ProcessedResultType.EXIT;

            if ( _udpPendingPacket is null )
            {
                if ( udpPacketBuffCount >= SizeHelper.Normal )
                {
                    int newPayloadSize = -1;

                    lock ( _udpPacketBuffer )
                    {
                        newPayloadSize = BitConverter.ToInt32( _udpPacketBuffer.ToArray( ), 0 );
                    }

                    _udpPendingPacket = new PendingPacket
                    {
                        PayloadSize = newPayloadSize,
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

            if ( udpPacketBuffCount >= _udpPendingPacket.PayloadSize )
            {
                // Add current buffer's data to the pendingPacket to fill it up more
                lock ( _udpPacketBuffer )
                {
                    _udpPendingPacket.CurrentPayload.AddRange( _udpPacketBuffer.GetRange( 0, _udpPendingPacket.PayloadSize ) );
                }
                // Check if the pendingPacket is full...
                if ( _udpPendingPacket.CurrentPayload.Count >= _udpPendingPacket.PayloadSize )
                {
                    // its full! Raise the event and then if we have enough data, repeat this loop.
                    try
                    {
                        PacketReadyEvent?.Invoke( new KablePacket( _udpPendingPacket.CurrentPayload ), this );
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
                        _udpPacketBuffer = _udpPacketBuffer.GetRange( _udpPendingPacket.PayloadSize,
                            _udpPacketBuffer.Count - _udpPendingPacket.PayloadSize );
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
            if ( Closed )
                return;

            int againCount = 0;
            // Dont rerun these loops more than maxProcessIterations times.
            
            // Tcp
            while ( againCount < maxProcessIterations )
            {
                if ( Closed )
                    return;
                againCount++;

                ProcessedResultType result = ProcessBufferTcp( );
                if ( result is ProcessedResultType.EXIT )
                    break;
            }

            againCount = 0;
            // Udp
            while ( againCount < maxProcessIterations )
            {
                if ( Closed )
                    return;
                againCount++;

                ProcessedResultType result = ProcessBufferUdp( );
                if ( result is ProcessedResultType.EXIT )
                    break;
            }
        }

        public void Close( )
        {
            Closed = true;
            try
            {
                TcpSocket.Close( );
            }
            catch
            {
                // ignored
            }

            try
            {
                UdpSocket.Close( );
            }
            catch
            {
                // ignored
            }
        }

        public bool Connected { get; private set; }
        public bool Closed { get; private set; } = false;
        public Socket TcpSocket { get; }
        public Socket UdpSocket { get; }
        public IPAddress Address { get; }
        public int Port { get; }

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
            public int PayloadSize { get; set; }
            public List<byte> CurrentPayload { get; } = new List<byte>( );
        }
    }
}

using CimonPlc.Enums;
using CimonPlc.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CimonPlc.Sockets
{
    public class TcpSocket : IEthernetSocket, IDisposable
    {
        private const int PollInterval = 5;

        private readonly IPAddress _ip;
        private readonly int _port;
        private readonly bool _usePing;
        private Socket _socket;
        private int _readTimeout = 1000;

        public bool IsConnected => _socket?.Connected == true;

        /// <param name="ip">IP address of the PLC</param>
        /// <param name="port">TCP port of the PLC, default is 10620</param>
        /// <param name="usePing">
        ///     If true, the PLC is pinged before connecting and <see cref="ConnectionStatus.NoRouteToDestination"/>
        ///     is returned when it does not answer. Disable it on networks that block ICMP.
        /// </param>
        public TcpSocket(string ip, int port = 10620, bool usePing = true)
        {
            Guard.Against.OutOfRange(port, nameof(port), 1, 65535);
            Guard.Against.NullOrEmpty(ip, nameof(ip));
            Guard.Against.InvalidData(ip, nameof(ip), x => IPAddress.TryParse(x, out _));

            _ip = IPAddress.Parse(ip);
            _port = port;
            _usePing = usePing;
        }

        public async Task<ConnectionStatus> Connect(int readTimeout = 1000, int writeTimeout = 1000, int pingTimeout = 3000)
        {
            if (IsConnected)
                return ConnectionStatus.Connected;

            if (_usePing && Tools.Ping(_ip, pingTimeout) != IPStatus.Success)
                return ConnectionStatus.NoRouteToDestination;

            // A closed socket can't be reused, so a new one is created for every connection.
            CloseSocket();
            _readTimeout = readTimeout;
            _socket = new Socket(_ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                ReceiveTimeout = readTimeout,
                SendTimeout = writeTimeout,
                NoDelay = true
            };

            try
            {
                await _socket.ConnectAsync(new IPEndPoint(_ip, _port)).ConfigureAwait(false);
            }
            catch
            {
                CloseSocket();
                throw;
            }

            return _socket.Connected ? ConnectionStatus.Connected : ConnectionStatus.DisConnected;
        }

        public ConnectionStatus Disconnect()
        {
            CloseSocket();
            return ConnectionStatus.DisConnected;
        }

        public async Task<bool> SendData(byte[] frame)
        {
            if (!IsConnected)
                return false;

            var result = await _socket.SendAsync(new ArraySegment<byte>(frame), SocketFlags.None).ConfigureAwait(false);
            return result == frame.Length;
        }

        /// <summary>
        /// Waits for a complete response frame from PLC, up to the read timeout.
        /// </summary>
        /// <returns>The received frame, or null if nothing was received</returns>
        public async Task<byte[]> ReceiveData()
        {
            if (!IsConnected)
                return null;

            var frame = new List<byte>();
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds < _readTimeout)
            {
                var available = _socket.Available;
                if (available > 0)
                {
                    var buffer = new byte[available];
                    var received = _socket.Receive(buffer, 0, available, SocketFlags.None);
                    for (var i = 0; i < received; i++)
                        frame.Add(buffer[i]);

                    var expectedLength = EthernetFrame.ExpectedLength(frame);
                    if (expectedLength > 0 && frame.Count >= expectedLength)
                        break;

                    continue;
                }

                // Readable with nothing to read means the remote side closed the connection.
                if (_socket.Poll(0, SelectMode.SelectRead) && _socket.Available == 0)
                    break;

                await Task.Delay(PollInterval).ConfigureAwait(false);
            }

            return frame.Count == 0 ? null : frame.ToArray();
        }

        public void Dispose()
        {
            CloseSocket();
        }

        private void CloseSocket()
        {
            var socket = _socket;
            _socket = null;
            if (socket == null)
                return;

            try
            {
                if (socket.Connected)
                    socket.Shutdown(SocketShutdown.Both);
            }
            catch (SocketException)
            {
                // The connection is already gone
            }
            finally
            {
                socket.Dispose();
            }
        }
    }
}

using CimonPlc.Enums;
using CimonPlc.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading.Tasks;

namespace CimonPlc.Sockets
{
    public class SerialSocket : ISerialSocket, IDisposable
    {
        private const int PollInterval = 5;

        private readonly SerialPort _port;

        public bool IsConnected => _port.IsOpen;

        public SerialSocket(string portName, int baudRate = 9600)
        {
            Guard.Against.NullOrEmpty(portName, nameof(portName));
            Guard.Against.OutOfRange(baudRate, nameof(baudRate), 75, 256000);

            _port = new SerialPort(portName, baudRate);
        }

        public Task<ConnectionStatus> Connect(int readTimeout = 1000, int writeTimeout = 1000, int pingTimeout = 3000)
        {
            _port.ReadTimeout = readTimeout;
            _port.WriteTimeout = writeTimeout;

            if (!_port.IsOpen)
                _port.Open();

            return Task.FromResult(_port.IsOpen ? ConnectionStatus.Connected : ConnectionStatus.DisConnected);
        }

        public ConnectionStatus Disconnect()
        {
            if (_port.IsOpen)
                _port.Close();

            return ConnectionStatus.DisConnected;
        }

        public Task<bool> SendData(byte[] frame)
        {
            if (!_port.IsOpen)
                return Task.FromResult(false);

            _port.DiscardInBuffer();
            _port.Write(frame, 0, frame.Length);
            return Task.FromResult(true);
        }

        /// <summary>
        /// Waits for a complete response frame (terminated by ETX) from PLC, up to the read timeout.
        /// </summary>
        /// <returns>The received frame, or null if nothing was received</returns>
        public async Task<byte[]> ReceiveData()
        {
            if (!_port.IsOpen)
                return null;

            var frame = new List<byte>();
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds < _port.ReadTimeout)
            {
                var available = _port.BytesToRead;
                if (available > 0)
                {
                    var buffer = new byte[available];
                    var received = _port.Read(buffer, 0, available);
                    for (var i = 0; i < received; i++)
                        frame.Add(buffer[i]);

                    if (frame[frame.Count - 1] == SerialFrame.Etx)
                        break;

                    continue;
                }

                await Task.Delay(PollInterval).ConfigureAwait(false);
            }

            return frame.Count == 0 ? null : frame.ToArray();
        }

        public void Dispose()
        {
            _port.Dispose();
        }
    }
}

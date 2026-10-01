using CimonPlc.Enums;
using CimonPlc.Interfaces;
using System;
using System.Threading.Tasks;

namespace CimonPlc.UnitTests.FakeClasses
{
    /// <summary>
    /// Ethernet socket which answers every request using the given response factory.
    /// </summary>
    public class FakeEthernetSocket : IEthernetSocket
    {
        private readonly Func<byte[], byte[]> _responseFactory;
        private byte[] _lastRequest;

        public FakeEthernetSocket(Func<byte[], byte[]> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public bool IsConnected { get; private set; }

        public int ConnectCount { get; private set; }

        public byte[] LastRequest => _lastRequest;

        public Task<ConnectionStatus> Connect(int readTimeout = 1000, int writeTimeout = 1000, int pingTimeout = 3000)
        {
            ConnectCount++;
            IsConnected = true;
            return Task.FromResult(ConnectionStatus.Connected);
        }

        public ConnectionStatus Disconnect()
        {
            IsConnected = false;
            return ConnectionStatus.DisConnected;
        }

        public Task<bool> SendData(byte[] frame)
        {
            _lastRequest = frame;
            return Task.FromResult(true);
        }

        public Task<byte[]> ReceiveData()
        {
            return Task.FromResult(_responseFactory(_lastRequest));
        }
    }
}

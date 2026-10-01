using CimonPlc.Enums;
using CimonPlc.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CimonPlc.Models
{
    public abstract class PlcConnector : IDisposable
    {
        protected readonly IPlcSocket _socket;
        protected readonly bool _autoConnect;

        // PLC protocols are strictly request/response, so only one exchange may be in flight per connection.
        private readonly SemaphoreSlim _exchangeLock = new SemaphoreSlim(1, 1);
        private bool _disposed;

        public bool IsConnected => _socket.IsConnected;

        /// <param name="socket">The socket used to talk to the PLC</param>
        /// <param name="autoConnect">
        ///     If true, read/write functions open the connection when it is not open and
        ///     close it again after the request is completed.
        /// </param>
        protected PlcConnector(IPlcSocket socket, bool autoConnect = true)
        {
            _socket = Guard.Against.Null(socket, nameof(socket));
            _autoConnect = autoConnect;
        }

        /// <summary>
        /// Creates a connection to PLC using a network socket. It must be called before reading or writing data,
        /// unless the connector is created with auto connect enabled.
        /// </summary>
        /// <param name="readTimeout">Data read timeout in ms, valid range is between 100 and 10,000</param>
        /// <param name="writeTimeout">Data write timeout in ms, valid range is between 100 and 10,000</param>
        /// <param name="pingTimeout">Ping Timeout in ms, valid range is between 100 and 10,000</param>
        /// <returns>Returns Connected if it can connect to PLC successfully</returns>
        public virtual Task<ConnectionStatus> Connect(int readTimeout = 1000, int writeTimeout = 1000, int pingTimeout = 3000)
        {
            Guard.Against.OutOfRange(readTimeout, nameof(readTimeout), 100, 10000);
            Guard.Against.OutOfRange(writeTimeout, nameof(writeTimeout), 100, 10000);
            Guard.Against.OutOfRange(pingTimeout, nameof(pingTimeout), 100, 10000);
            ThrowIfDisposed();

            return _socket.Connect(readTimeout, writeTimeout, pingTimeout);
        }

        /// <summary>
        /// Drops the connection to PLC. It should be called when auto connect is not used.
        /// </summary>
        /// <returns>Returns DisConnected if it can disconnect from PLC successfully</returns>
        public virtual ConnectionStatus Disconnect()
        {
            return _socket.Disconnect();
        }

        public abstract Task<(ResponseCode responseCode, int[] data)> ReadWordAsync(MemoryType memoryType, string address, int length);

        public abstract Task<(ResponseCode responseCode, byte[] data)> ReadBitAsync(MemoryType memoryType, string address, int length);

        public abstract Task<ResponseCode> WriteWordAsync(MemoryType memoryType, string address, params int[] data);

        public abstract Task<ResponseCode> WriteBitAsync(MemoryType memoryType, string address, params byte[] data);

        /// <summary>
        /// Sends a request frame to the PLC and waits for its response, taking care of auto connect.
        /// </summary>
        /// <returns>
        ///     The response frame on success; otherwise a null frame and the
        ///     error code describing why the exchange failed.
        /// </returns>
        protected async Task<(ResponseCode error, byte[] response)> ExchangeAsync(byte[] request)
        {
            ThrowIfDisposed();

            await _exchangeLock.WaitAsync().ConfigureAwait(false);
            var openedHere = false;
            try
            {
                if (!IsConnected)
                {
                    if (!_autoConnect)
                        return (ResponseCode.SystemError, null);

                    if (await Connect().ConfigureAwait(false) != ConnectionStatus.Connected)
                        return (ResponseCode.SystemError, null);

                    openedHere = true;
                }

                if (!await _socket.SendData(request).ConfigureAwait(false))
                    return (ResponseCode.WritingError, null);

                var response = await _socket.ReceiveData().ConfigureAwait(false);
                if (response == null || response.Length == 0)
                    return (ResponseCode.SystemError, null);

                return (ResponseCode.Success, response);
            }
            finally
            {
                // Only close connections that were opened automatically, so callers who
                // connect explicitly can keep a persistent connection.
                if (openedHere)
                    _socket.Disconnect();

                _exchangeLock.Release();
            }
        }

        protected static string NormalizeAddress(string address)
        {
            return address.PadLeft(6, '0').ToUpperInvariant();
        }

        protected void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                if (_socket is IDisposable disposable)
                    disposable.Dispose();
                else
                    _socket.Disconnect();

                _exchangeLock.Dispose();
            }

            _disposed = true;
        }
    }
}

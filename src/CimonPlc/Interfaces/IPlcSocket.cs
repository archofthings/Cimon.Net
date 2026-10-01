using CimonPlc.Enums;
using System.Threading.Tasks;

namespace CimonPlc.Interfaces
{
    public interface IPlcSocket
    {
        bool IsConnected { get; }

        Task<ConnectionStatus> Connect(int readTimeout = 1000, int writeTimeout = 1000, int pingTimeout = 3000);

        ConnectionStatus Disconnect();

        Task<bool> SendData(byte[] frame);

        /// <summary>
        /// Waits for the response of the last sent frame.
        /// Implementations should return as soon as a complete frame is received, or when the read timeout expires.
        /// </summary>
        /// <returns>The received frame, or null if nothing was received</returns>
        Task<byte[]> ReceiveData();
    }
}

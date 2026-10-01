using System;
using System.Threading.Tasks;
using Xunit;
using CimonPlc.PlcConnectors;
using CimonPlc.Enums;
using CimonPlc.Sockets;
using Rony.Net;
using Rony.Listeners;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace CimonPlc.UnitTests
{
    public class EthernetConnectorTests
    {
        [Theory]
        [InlineData(100, 100, 100)]
        [InlineData(1000, 1000, 500)]
        [InlineData(1000, 1000, 1000)]
        [InlineData(5000, 4000, 3000)]
        [InlineData(1000, 1000, 5000)]
        public async Task Connect_Should_Return_Connected_On_Valid_Data(int readTimeout, int writeTimeout, int pingTimeout)
        {
            //Arrange 
            var connector = new EthernetConnector(new TcpSocket("127.0.0.1", 10619));
            using var mockServer = new MockServer(new TcpServer(10619));
            mockServer.Start();

            //Act
            var result = await connector.Connect(readTimeout, writeTimeout, pingTimeout);
            mockServer.Stop();
            //Assert
            Assert.Equal(ConnectionStatus.Connected, result);
        }

        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(-1000, -1000, -500)]
        [InlineData(0, 1000, 1000)]
        [InlineData(1000, 0, 1000)]
        [InlineData(1000, 1000, 0)]
        public async Task Connect_Should_Return_Error_On_Incorrect_Data(int readTimeout, int writeTimeout, int pingTimeout)
        {
            //Arrange 
            var connector = new EthernetConnector(new TcpSocket("127.0.0.1", 10620));

            //Assert
            await Assert.ThrowsAnyAsync<ArgumentOutOfRangeException>(() => connector.Connect(readTimeout, writeTimeout, pingTimeout));
        }

        [Theory]
        [InlineData(MemoryType.X, "000F0", 10)]
        [InlineData(MemoryType.Y, "00010", 512)]
        [InlineData(MemoryType.D, "000F0", 6)]
        [InlineData(MemoryType.M, "0F010", 100)]
        [InlineData(MemoryType.L, "000F10", 100)]
        public async Task ReadWordAsync_Should_Return_Value_On_Correct_Data(MemoryType memoryType, string address, int length)
        {
            //Arrange 
            var connector = new EthernetConnector(new TcpSocket("127.0.0.1", 10620));
            using var mockServer = new MockServer(new TcpServer(10620));
            mockServer.Start();

            //Act
            mockServer.Mock.Send("").Receive(x =>
            {
                var response = new List<byte>();
                response.AddRange(Encoding.ASCII.GetBytes("KDT_PLC_S"));
                response.Add((byte)(x[9] + 128));
                response.Add(x[10]);
                response.Add(0);
                response.AddRange((9 + length * 2).ToDualByte());
                response.AddRange(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0 });
                for (var i = 0; i < length; i++)
                    response.AddRange(new byte[] { 1, 1 });
                response.AddRange(response.Sum(x => x).ToDualByte());
                return response.ToArray();
            });
            var (responseCode, data) = await connector.ReadWordAsync(memoryType, address, length);
            mockServer.Stop();

            //Assert
            Assert.Equal(ResponseCode.Success, responseCode);
            Assert.Equal(length, data.Length);
        }

        [Theory]
        [InlineData(MemoryType.X, "00FF0F1", 10)]
        [InlineData(MemoryType.D, "000x0", 6)]
        [InlineData(MemoryType.D, "000051", 6)]
        [InlineData(MemoryType.M, "0F01", 600)]
        [InlineData(MemoryType.Y, "000F1", 0)]
        public async Task ReadWordAsync_Should_Return_Error_On_Incorrect_Data(MemoryType memoryType, string address, int length)
        {
            //Arrange 
            var connector = new EthernetConnector(new TcpSocket("127.0.0.1", 10620));

            //Assert
            await Assert.ThrowsAnyAsync<ArgumentException>(() => connector.ReadWordAsync(memoryType, address, length));
        }

        [Theory]
        [InlineData(MemoryType.X, "000F1", 10)]
        [InlineData(MemoryType.Y, "0", 1024)]
        [InlineData(MemoryType.D, "000F5", 6)]
        [InlineData(MemoryType.M, "0F01", 100)]
        [InlineData(MemoryType.L, "000F1", 100)]
        public async Task ReadBitAsync_Should_Return_Value_On_Correct_Data(MemoryType memoryType, string address, int length)
        {
            //Arrange
            var connector = new EthernetConnector(new TcpSocket("127.0.0.1", 10621));
            using var mockServer = new MockServer(new TcpServer(10621));
            mockServer.Start();

            //Act
            mockServer.Mock.Send("").Receive(x =>
            {
                var response = new List<byte>();
                response.AddRange(Encoding.ASCII.GetBytes("KDT_PLC_S"));
                response.Add((byte)(x[9] + 128));
                response.Add(x[10]);
                response.Add(0);
                response.AddRange((9 + length).ToDualByte());
                response.AddRange(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0 });
                for (var i = 0; i < length; i++)
                    response.Add(1);
                response.AddRange(response.Sum(x => x).ToDualByte());
                return response.ToArray();
            });
            var (responseCode, data) = await connector.ReadBitAsync(memoryType, address, length);
            mockServer.Stop();

            //Assert
            Assert.Equal(ResponseCode.Success, responseCode);
            Assert.Equal(length, data.Length);
        }

        [Theory]
        [InlineData(MemoryType.X, "00FF0F1", 10)]
        [InlineData(MemoryType.D, "000x5", 6)]
        [InlineData(MemoryType.M, "0F01", 1060)]
        [InlineData(MemoryType.Y, "000F1", 0)]
        public async Task ReadBitAsync_Should_Return_Error_On_Incorrect_Data(MemoryType memoryType, string address, int length)
        {
            //Arrange
            var connector = new EthernetConnector(new TcpSocket("127.0.0.1", 10620));

            //Assert
            await Assert.ThrowsAnyAsync<ArgumentException>(() => connector.ReadBitAsync(memoryType, address, length));
        }

        [Theory]
        [InlineData(MemoryType.X, "000F0", 10, 100, 1000)]
        [InlineData(MemoryType.Y, "0", 1024, 35000)]
        [InlineData(MemoryType.D, "000F0", 16050)]
        [InlineData(MemoryType.M, "0F010", 100, 100, 10000, 1200, 1400)]
        [InlineData(MemoryType.L, "000F0", 1010, 65000, 3403, 2302)]
        public async Task WriteWordAsync_Should_Return_Value_On_Correct_Data(MemoryType memoryType, string address, params int[] data)
        {
            //Arrange
            var connector = new EthernetConnector(new TcpSocket("127.0.0.1", 10622));
            using var mockServer = new MockServer(new TcpServer(10622));
            mockServer.Start();

            //Act
            mockServer.Mock.Send("").Receive(x =>
            {
                var response = new List<byte>();
                response.AddRange(Encoding.ASCII.GetBytes("KDT_PLC_S"));
                response.Add((byte)(x[9] + 128));
                response.Add(0x41);
                response.Add(0);
                response.AddRange(new byte[] { 0, 2, 0, 0 });
                response.AddRange(response.Sum(x => x).ToDualByte());
                return response.ToArray();
            });
            var result = await connector.WriteWordAsync(memoryType, address, data);
            mockServer.Stop();

            //Assert
            Assert.Equal(ResponseCode.Success, result);
        }

        [Theory]
        [InlineData(MemoryType.X, "00FF01", 10, 110, 0, 1205)]
        [InlineData(MemoryType.D, "000x5", 6)]
        [InlineData(MemoryType.M, "0F00", 70000, 30000, 40000)]
        [InlineData(MemoryType.Y, "000F0", -10, 100, 1000)]
        [InlineData(MemoryType.Y, "0")]
        public async Task WriteWordAsync_Should_Return_Error_On_Incorrect_Data(MemoryType memoryType, string address, params int[] data)
        {
            //Arrange
            var connector = new EthernetConnector(new TcpSocket("127.0.0.1", 10620));

            //Assert
            await Assert.ThrowsAnyAsync<ArgumentException>(() => connector.WriteWordAsync(memoryType, address, data));
        }


        [Theory]
        [InlineData(MemoryType.X, "000F0", (byte)1, (byte)1, (byte)1)]
        [InlineData(MemoryType.Y, "0", (byte)1, (byte)1, (byte)1, (byte)1)]
        [InlineData(MemoryType.D, "000F1", (byte)1, (byte)1)]
        [InlineData(MemoryType.M, "0F011", (byte)1, (byte)1, (byte)0, (byte)1, (byte)1)]
        [InlineData(MemoryType.L, "000F0", (byte)1, (byte)1, (byte)1, (byte)0)]
        public async Task WriteBitAsync_Should_Return_Value_On_Correct_Data(MemoryType memoryType, string address, params byte[] data)
        {
            //Arrange
            var connector = new EthernetConnector(new TcpSocket("127.0.0.1", 10623));
            using var mockServer = new MockServer(new TcpServer(10623));
            mockServer.Start();

            //Act
            mockServer.Mock.Send("").Receive(x =>
            {
                var response = new List<byte>();
                response.AddRange(Encoding.ASCII.GetBytes("KDT_PLC_S"));
                response.Add((byte)(x[9] + 128));
                response.Add(0x41);
                response.Add(0);
                response.AddRange(new byte[] { 0, 2, 0, 0 });
                response.AddRange(response.Sum(x => x).ToDualByte());
                return response.ToArray();
            });
            var result = await connector.WriteBitAsync(memoryType, address, data);
            mockServer.Stop();

            //Assert
            Assert.Equal(ResponseCode.Success, result);
        }

        [Theory]
        [InlineData(MemoryType.X, "00FF001", (byte)1, (byte)1, (byte)0, (byte)1)]
        [InlineData(MemoryType.D, "000x5", (byte)1)]
        [InlineData(MemoryType.D, "000F5", (byte)2)]
        [InlineData(MemoryType.Y, "0")]
        public async Task WriteBitAsync_Should_Return_Error_On_Incorrect_Data(MemoryType memoryType, string address, params byte[] data)
        {
            //Arrange
            var connector = new EthernetConnector(new TcpSocket("127.0.0.1", 10623));

            //Assert
            await Assert.ThrowsAnyAsync<ArgumentException>(() => connector.WriteBitAsync(memoryType, address, data));
        }
    }
}

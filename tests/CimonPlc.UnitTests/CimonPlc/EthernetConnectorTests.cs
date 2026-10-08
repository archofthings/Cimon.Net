using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CimonPlc.Enums;
using CimonPlc.PlcConnectors;
using CimonPlc.Sockets;
using CimonPlc.UnitTests.Simulators;
using Rony.Net;
using Xunit;
using Xunit.Abstractions;

namespace CimonPlc.UnitTests
{
    /// <summary>
    /// Reads and writes of <see cref="EthernetConnector"/> against a PLC simulated with a Rony.Net mock server.
    /// </summary>
    public class EthernetConnectorTests : PlcMockServerTest
    {
        public EthernetConnectorTests(ITestOutputHelper output) : base(output)
        {
        }

        [Theory]
        [InlineData(100, 100, 100)]
        [InlineData(1000, 1000, 500)]
        [InlineData(1000, 1000, 1000)]
        [InlineData(5000, 4000, 3000)]
        [InlineData(1000, 1000, 5000)]
        public async Task Connect_Should_Return_Connected_On_Valid_Data(int readTimeout, int writeTimeout, int pingTimeout)
        {
            //Arrange (ping enabled, the loopback address answers it)
            using var connector = new EthernetConnector(new TcpSocket("127.0.0.1", Server.Port));

            //Act
            var result = await connector.Connect(readTimeout, writeTimeout, pingTimeout);

            //Assert
            Assert.Equal(ConnectionStatus.Connected, result);
            await Server.WaitForConnectionAsync();
            Server.Should().HaveAcceptedConnections(Times.Once());
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
            using var connector = new EthernetConnector(new TcpSocket("127.0.0.1"));

            //Assert
            await Assert.ThrowsAnyAsync<ArgumentOutOfRangeException>(() => connector.Connect(readTimeout, writeTimeout, pingTimeout));
        }

        [Theory]
        [InlineData(MemoryType.X, "000F0", 10)]
        [InlineData(MemoryType.Y, "00010", 512)]
        [InlineData(MemoryType.D, "000F0", 6)]
        [InlineData(MemoryType.M, "0F010", 100)]
        [InlineData(MemoryType.L, "000F10", 100)]
        public async Task ReadWordAsync_Should_Return_Plc_Memory(MemoryType memoryType, string address, int length)
        {
            //Arrange
            var start = Convert.ToInt32(address, 16);
            var expected = Enumerable.Range(0, length).Select(x => (x * 257 + 1) & 0xFFFF).ToArray();
            for (var i = 0; i < length; i++)
                Plc.SetWord(memoryType, start + i, expected[i]);
            Plc.Attach(Server);
            using var connector = CreateConnector();

            //Act
            var (responseCode, data) = await connector.ReadWordAsync(memoryType, address, length);

            //Assert
            Assert.Equal(ResponseCode.Success, responseCode);
            Assert.Equal(expected, data);
            Server.Should().HaveReceived(EthernetConnector.BuildFrame(0, (byte)ReadCommand.WordBlockRead, memoryType, address, length), Times.Once());
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
            using var connector = CreateConnector();

            //Assert
            await Assert.ThrowsAnyAsync<ArgumentException>(() => connector.ReadWordAsync(memoryType, address, length));
            Assert.Empty(Server.ReceivedRequests);
        }

        [Theory]
        [InlineData(MemoryType.X, "000F1", 10)]
        [InlineData(MemoryType.Y, "0", 1024)]
        [InlineData(MemoryType.D, "000F5", 6)]
        [InlineData(MemoryType.M, "0F01", 100)]
        [InlineData(MemoryType.L, "000F1", 100)]
        public async Task ReadBitAsync_Should_Return_Plc_Memory(MemoryType memoryType, string address, int length)
        {
            //Arrange
            var start = Convert.ToInt32(address, 16);
            var expected = Enumerable.Range(0, length).Select(x => (byte)(x % 3 == 0 ? 1 : 0)).ToArray();
            for (var i = 0; i < length; i++)
                Plc.SetBit(memoryType, start + i, expected[i]);
            Plc.Attach(Server);
            using var connector = CreateConnector();

            //Act
            var (responseCode, data) = await connector.ReadBitAsync(memoryType, address, length);

            //Assert
            Assert.Equal(ResponseCode.Success, responseCode);
            Assert.Equal(expected, data);
            Server.Should().HaveReceived(EthernetConnector.BuildFrame(0, (byte)ReadCommand.BitBlockRead, memoryType, address, length), Times.Once());
        }

        [Theory]
        [InlineData(MemoryType.X, "00FF0F1", 10)]
        [InlineData(MemoryType.D, "000x5", 6)]
        [InlineData(MemoryType.M, "0F01", 1060)]
        [InlineData(MemoryType.Y, "000F1", 0)]
        public async Task ReadBitAsync_Should_Return_Error_On_Incorrect_Data(MemoryType memoryType, string address, int length)
        {
            //Arrange
            using var connector = CreateConnector();

            //Assert
            await Assert.ThrowsAnyAsync<ArgumentException>(() => connector.ReadBitAsync(memoryType, address, length));
            Assert.Empty(Server.ReceivedRequests);
        }

        [Theory]
        [InlineData(MemoryType.X, "000F0", 10, 100, 1000)]
        [InlineData(MemoryType.Y, "0", 1024, 35000)]
        [InlineData(MemoryType.D, "000F0", 16050)]
        [InlineData(MemoryType.M, "0F010", 100, 100, 10000, 1200, 1400)]
        [InlineData(MemoryType.L, "000F0", 1010, 65000, 3403, 2302)]
        public async Task WriteWordAsync_Should_Store_Data_In_Plc_Memory(MemoryType memoryType, string address, params int[] data)
        {
            //Arrange
            Plc.Attach(Server);
            using var connector = CreateConnector();

            //Act
            var result = await connector.WriteWordAsync(memoryType, address, data);

            //Assert
            Assert.Equal(ResponseCode.Success, result);
            var start = Convert.ToInt32(address, 16);
            Assert.Equal(data, Enumerable.Range(start, data.Length).Select(x => Plc.GetWord(memoryType, x)));
            Server.Should().HaveReceived(r => r.Body[10] == (byte)WriteCommands.WordBlockWrite, Times.Once());
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
            using var connector = CreateConnector();

            //Assert
            await Assert.ThrowsAnyAsync<ArgumentException>(() => connector.WriteWordAsync(memoryType, address, data));
            Assert.Empty(Server.ReceivedRequests);
        }

        [Theory]
        [InlineData(MemoryType.X, "000F0", (byte)1, (byte)1, (byte)1)]
        [InlineData(MemoryType.Y, "0", (byte)1, (byte)1, (byte)1, (byte)1)]
        [InlineData(MemoryType.D, "000F1", (byte)1, (byte)1)]
        [InlineData(MemoryType.M, "0F011", (byte)1, (byte)1, (byte)0, (byte)1, (byte)1)]
        [InlineData(MemoryType.L, "000F0", (byte)1, (byte)1, (byte)1, (byte)0)]
        public async Task WriteBitAsync_Should_Store_Data_In_Plc_Memory(MemoryType memoryType, string address, params byte[] data)
        {
            //Arrange
            Plc.Attach(Server);
            using var connector = CreateConnector();

            //Act
            var result = await connector.WriteBitAsync(memoryType, address, data);

            //Assert
            Assert.Equal(ResponseCode.Success, result);
            var start = Convert.ToInt32(address, 16);
            Assert.Equal(data, Enumerable.Range(start, data.Length).Select(x => Plc.GetBit(memoryType, x)));
            Server.Should().HaveReceived(r => r.Body[10] == (byte)WriteCommands.BitBlockWrite, Times.Once());
        }

        [Theory]
        [InlineData(MemoryType.X, "00FF001", (byte)1, (byte)1, (byte)0, (byte)1)]
        [InlineData(MemoryType.D, "000x5", (byte)1)]
        [InlineData(MemoryType.D, "000F5", (byte)2)]
        [InlineData(MemoryType.Y, "0")]
        public async Task WriteBitAsync_Should_Return_Error_On_Incorrect_Data(MemoryType memoryType, string address, params byte[] data)
        {
            //Arrange
            using var connector = CreateConnector();

            //Assert
            await Assert.ThrowsAnyAsync<ArgumentException>(() => connector.WriteBitAsync(memoryType, address, data));
            Assert.Empty(Server.ReceivedRequests);
        }

        [Fact]
        public async Task Written_Words_Should_Be_Read_Back()
        {
            //Arrange
            Plc.Attach(Server);
            using var connector = CreateConnector();
            await connector.Connect();

            //Act
            var writeResult = await connector.WriteWordAsync(MemoryType.D, "100", 1, 2, 0xFFFF, 0);
            var (readResult, data) = await connector.ReadWordAsync(MemoryType.D, "100", 4);

            //Assert
            Assert.Equal(ResponseCode.Success, writeResult);
            Assert.Equal(ResponseCode.Success, readResult);
            Assert.Equal(new[] { 1, 2, 0xFFFF, 0 }, data);
            Server.Should().HaveReceivedInOrder(
                r => r.Body[10] == (byte)WriteCommands.WordBlockWrite,
                r => r.Body[10] == (byte)ReadCommand.WordBlockRead);
        }

        [Fact]
        public async Task Written_Bits_Should_Be_Read_Back()
        {
            //Arrange
            Plc.Attach(Server);
            using var connector = CreateConnector();
            await connector.Connect();

            //Act
            var writeResult = await connector.WriteBitAsync(MemoryType.M, "10", 1, 0, 1, 1);
            var (readResult, data) = await connector.ReadBitAsync(MemoryType.M, "10", 4);

            //Assert
            Assert.Equal(ResponseCode.Success, writeResult);
            Assert.Equal(ResponseCode.Success, readResult);
            Assert.Equal(new byte[] { 1, 0, 1, 1 }, data);
        }

        [Fact]
        public async Task Requests_Should_Carry_Normalized_Address()
        {
            //Arrange
            Plc.Attach(Server);
            using var connector = CreateConnector();

            //Act
            await connector.ReadWordAsync(MemoryType.D, "a0", 1);

            //Assert : 6 upper case hex chars, after the memory type and a 0 byte
            var request = Assert.Single(Server.ReceivedRequests);
            Assert.Equal((byte)MemoryType.D, request.Body[14]);
            Assert.Equal("0000A0", Encoding.ASCII.GetString(request.Body, 16, 6));
        }

        [Fact]
        public async Task Frame_Numbers_Should_Increase_With_Every_Request()
        {
            //Arrange
            Plc.Attach(Server);
            using var connector = CreateConnector();
            await connector.Connect();

            //Act
            for (var i = 0; i < 5; i++)
                Assert.Equal(ResponseCode.Success, await connector.WriteWordAsync(MemoryType.D, "0", i));

            //Assert
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, Server.ReceivedRequests.Select(r => r.Body[9]));
        }

        [Theory]
        [InlineData(ResponseCode.InvalidDevicePrefix)]
        [InlineData(ResponseCode.InvalidDeviceAddress)]
        [InlineData(ResponseCode.ChecksumError)]
        [InlineData(ResponseCode.CpuError)]
        public async Task Operations_Should_Return_Error_Code_Sent_By_Plc(ResponseCode code)
        {
            //Arrange
            Server.Mock.Send("").Receive(request => CimonPlcSimulator.Ack(request, code));
            using var connector = CreateConnector();

            //Act
            var (readResult, data) = await connector.ReadWordAsync(MemoryType.D, "0", 1);
            var writeResult = await connector.WriteBitAsync(MemoryType.M, "0", 1);

            //Assert
            Assert.Equal(code, readResult);
            Assert.Null(data);
            Assert.Equal(code, writeResult);
        }

        [Fact]
        public async Task ReadWordAsync_Should_Return_Error_When_Plc_Acknowledges_Without_Data()
        {
            //Arrange : 0x41 with Success is a write acknowledge, not an answer to a read
            Server.Mock.Send("").Receive(request => CimonPlcSimulator.Ack(request));
            using var connector = CreateConnector();

            //Act
            var (responseCode, data) = await connector.ReadWordAsync(MemoryType.D, "0", 1);

            //Assert
            Assert.Equal(ResponseCode.WritingError, responseCode);
            Assert.Null(data);
        }

        [Fact]
        public async Task Operations_Should_Return_Error_On_Wrong_Frame_Number()
        {
            //Arrange : the PLC answers another request
            Server.Mock.Send("").Receive(request => CimonPlcSimulator.SlaveFrame((byte)(request[9] + 1), EthernetFrame.AckCommand, new byte[] { 0, 0 }));
            using var connector = CreateConnector();

            //Act
            var result = await connector.WriteWordAsync(MemoryType.D, "0", 1);

            //Assert
            Assert.Equal(ResponseCode.WritingError, result);
        }

        [Fact]
        public async Task ReadWordAsync_Should_Return_Error_On_Answer_To_Another_Command()
        {
            //Arrange : a bit read answer for a word read request
            Server.Mock.SendMatchingBytes(CimonPlcSimulator.IsCommand(ReadCommand.WordBlockRead))
                .Receive(request => CimonPlcSimulator.SlaveFrame(request[9], (byte)ReadCommand.BitBlockRead, new byte[11]));
            using var connector = CreateConnector();

            //Act
            var (responseCode, data) = await connector.ReadWordAsync(MemoryType.D, "0", 1);

            //Assert
            Assert.Equal(ResponseCode.WritingError, responseCode);
            Assert.Null(data);
        }
    }
}

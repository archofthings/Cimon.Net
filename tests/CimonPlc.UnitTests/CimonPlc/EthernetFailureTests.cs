using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CimonPlc.Enums;
using CimonPlc.UnitTests.Simulators;
using Rony.Net;
using Xunit;
using Xunit.Abstractions;

namespace CimonPlc.UnitTests
{
    /// <summary>
    /// Network and PLC failures simulated with Rony.Net: slow, partial, broken and missing responses.
    /// </summary>
    public class EthernetFailureTests : PlcMockServerTest
    {
        private const int ReadTimeout = 300;

        public EthernetFailureTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task Response_Sent_In_Pieces_Should_Be_Reassembled()
        {
            //Arrange
            Plc.SetWord(MemoryType.D, 0, 1234);
            Server.Mock.SendMatchingBytes(CimonPlcSimulator.IsCommand(ReadCommand.WordBlockRead))
                .Receive(Plc.ReadWords)
                .InChunks(3, TimeSpan.FromMilliseconds(5));
            using var connector = CreateConnector();

            //Act
            var (responseCode, data) = await connector.ReadWordAsync(MemoryType.D, "0", 1);

            //Assert
            Assert.Equal(ResponseCode.Success, responseCode);
            Assert.Equal(new[] { 1234 }, data);
        }

        [Fact]
        public async Task Slow_Response_Within_Read_Timeout_Should_Succeed()
        {
            //Arrange
            Server.Mock.Send("").Receive(Plc.WriteWords).After(TimeSpan.FromMilliseconds(100));
            using var connector = CreateConnector();
            await connector.Connect(readTimeout: 2000);

            //Act
            var result = await connector.WriteWordAsync(MemoryType.D, "0", 1);

            //Assert
            Assert.Equal(ResponseCode.Success, result);
        }

        [Fact]
        public async Task Response_After_Read_Timeout_Should_Return_SystemError()
        {
            //Arrange
            Server.Mock.Send("").Receive(Plc.WriteWords).After(TimeSpan.FromSeconds(2));
            using var connector = CreateConnector();
            await connector.Connect(readTimeout: ReadTimeout);

            //Act
            var result = await connector.WriteWordAsync(MemoryType.D, "0", 1);

            //Assert
            Assert.Equal(ResponseCode.SystemError, result);
        }

        [Fact]
        public async Task No_Response_Should_Return_SystemError()
        {
            //Arrange
            Server.Mock.Send("").NoReply();
            using var connector = CreateConnector();
            await connector.Connect(readTimeout: ReadTimeout);

            //Act
            var (responseCode, data) = await connector.ReadWordAsync(MemoryType.D, "0", 1);

            //Assert
            Assert.Equal(ResponseCode.SystemError, responseCode);
            Assert.Null(data);
            Server.Should().HaveReceived(r => true, Times.Once());
        }

        [Fact]
        public async Task Truncated_Response_Should_Return_WritingError()
        {
            //Arrange : only the header arrives, then the PLC closes the connection
            Server.Mock.Send("").Receive(Plc.ReadWords).Truncated(EthernetFrame.HeaderLength).AndDisconnect();
            using var connector = CreateConnector();

            //Act
            var (responseCode, data) = await connector.ReadWordAsync(MemoryType.D, "0", 4);

            //Assert
            Assert.Equal(ResponseCode.WritingError, responseCode);
            Assert.Null(data);
        }

        [Fact]
        public async Task Truncated_Response_On_Open_Connection_Should_Return_WritingError_After_Read_Timeout()
        {
            //Arrange
            Server.Mock.Send("").Receive(Plc.ReadWords).Truncated(20);
            using var connector = CreateConnector();
            await connector.Connect(readTimeout: ReadTimeout);

            //Act
            var (responseCode, data) = await connector.ReadWordAsync(MemoryType.D, "0", 4);

            //Assert
            Assert.Equal(ResponseCode.WritingError, responseCode);
            Assert.Null(data);
        }

        [Fact]
        public async Task Corrupted_Checksum_Should_Return_WritingError()
        {
            //Arrange
            Server.Mock.Send("").Receive(Plc.WriteWords).Corrupted(bytes =>
            {
                bytes[bytes.Length - 1] ^= 0xFF;
                return bytes;
            });
            using var connector = CreateConnector();

            //Act
            var result = await connector.WriteWordAsync(MemoryType.D, "0", 1);

            //Assert
            Assert.Equal(ResponseCode.WritingError, result);
        }

        [Fact]
        public async Task Corrupted_Header_Should_Return_WritingError()
        {
            //Arrange : "KDT_PLC_S" becomes "XDT_PLC_S"
            Server.Mock.Send("").Receive(Plc.ReadBits).Corrupted(bytes =>
            {
                bytes[0] = (byte)'X';
                return bytes;
            });
            using var connector = CreateConnector();

            //Act
            var (responseCode, data) = await connector.ReadBitAsync(MemoryType.M, "0", 8);

            //Assert
            Assert.Equal(ResponseCode.WritingError, responseCode);
            Assert.Null(data);
        }

        [Fact]
        public async Task Dropped_Connection_Should_Return_SystemError()
        {
            //Arrange
            Server.Mock.Send("").Disconnect();
            using var connector = CreateConnector();

            //Act
            var result = await connector.WriteBitAsync(MemoryType.M, "0", 1);

            //Assert
            Assert.Equal(ResponseCode.SystemError, result);
        }

        [Fact]
        public async Task Reset_Connection_Should_Return_SystemError()
        {
            //Arrange
            Server.Mock.Send("").ResetConnection();
            using var connector = CreateConnector();

            //Act
            var result = await connector.WriteBitAsync(MemoryType.M, "0", 1);

            //Assert
            Assert.Equal(ResponseCode.SystemError, result);
        }

        [Fact]
        public async Task Connection_Closed_While_Waiting_Should_Return_Before_Read_Timeout()
        {
            //Arrange
            Server.Mock.Send("").NoReply();
            using var connector = CreateConnector();
            await connector.Connect(readTimeout: 10000);
            var stopwatch = Stopwatch.StartNew();

            //Act : the PLC goes away while the request is pending
            var pending = connector.ReadWordAsync(MemoryType.D, "0", 1);
            await Server.Mock.WaitForRequestAsync();
            await Server.Connections[0].CloseAsync();
            var (responseCode, _) = await pending;

            //Assert
            Assert.Equal(ResponseCode.SystemError, responseCode);
            Assert.True(stopwatch.ElapsedMilliseconds < 5000, $"Took {stopwatch.ElapsedMilliseconds} ms");
        }

        [Fact]
        public async Task AutoConnect_Should_Recover_After_Dropped_Connection()
        {
            //Arrange : the first request loses the connection, the next ones are answered
            Server.Mock.Send("").Disconnect().Then(Plc.WriteWords);
            using var connector = CreateConnector();

            //Act
            var first = await connector.WriteWordAsync(MemoryType.D, "0", 1);
            var second = await connector.WriteWordAsync(MemoryType.D, "0", 2);

            //Assert
            Assert.Equal(ResponseCode.SystemError, first);
            Assert.Equal(ResponseCode.Success, second);
            Assert.Equal(2, Plc.GetWord(MemoryType.D, 0));
            Server.Should().HaveAcceptedConnections(Times.Exactly(2));
        }

        [Fact]
        public async Task Busy_Plc_Should_Succeed_On_Retry()
        {
            //Arrange : a response sequence, the PLC reports an error once and then works
            Server.Mock.Send("")
                .Receive(request => CimonPlcSimulator.Ack(request, ResponseCode.CpuError))
                .Then(Plc.WriteWords);
            using var connector = CreateConnector();
            await connector.Connect();

            //Act
            var first = await connector.WriteWordAsync(MemoryType.D, "0", 7);
            var second = await connector.WriteWordAsync(MemoryType.D, "0", 7);

            //Assert
            Assert.Equal(ResponseCode.CpuError, first);
            Assert.Equal(ResponseCode.Success, second);
            Assert.Equal(7, Plc.GetWord(MemoryType.D, 0));
            Server.Should().HaveAcceptedConnections(Times.Once());
        }
    }
}

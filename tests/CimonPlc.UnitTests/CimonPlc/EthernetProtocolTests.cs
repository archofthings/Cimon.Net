using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CimonPlc.Enums;
using CimonPlc.PlcConnectors;
using CimonPlc.UnitTests.FakeClasses;
using Xunit;

namespace CimonPlc.UnitTests
{
    public class EthernetProtocolTests
    {
        private static byte[] WriteAck(byte[] request, int code = 0)
        {
            var response = new List<byte>();
            response.AddRange(Encoding.ASCII.GetBytes("KDT_PLC_S"));
            response.Add((byte)(request[9] + 128));
            response.Add(0x41);
            response.Add(0);
            response.AddRange(new byte[] { 0, 2 });
            response.AddRange(code.ToDualByte());
            response.AddRange(response.Sum(x => x).ToDualByte());
            return response.ToArray();
        }

        [Fact]
        public void FrameNo_Should_Cycle_From_0_To_127()
        {
            //Arrange
            var connector = new EthernetConnector(new FakeEthernetSocket(x => WriteAck(x)));

            //Act
            var frameNumbers = Enumerable.Range(0, 300).Select(_ => (int)connector.FrameNo).ToArray();

            //Assert
            var expected = Enumerable.Range(0, 300).Select(x => x % 128).ToArray();
            Assert.Equal(expected, frameNumbers);
        }

        [Fact]
        public void BuildFrame_Should_Use_Two_Bytes_Length_For_Read_Requests()
        {
            //Act
            var frame = EthernetConnector.BuildFrame(5, (byte)ReadCommand.WordBlockRead, MemoryType.D, "100", 3);

            //Assert
            var expected = new List<byte>();
            expected.AddRange(Encoding.ASCII.GetBytes("KDT_PLC_M"));
            expected.AddRange(new byte[] { 5, 0x52, 0, 0, 10, (byte)MemoryType.D, 0 });
            expected.AddRange(Encoding.ASCII.GetBytes("000100"));
            expected.AddRange(new byte[] { 0, 3 });
            expected.AddRange(expected.Sum(x => x).ToDualByte());
            Assert.Equal(expected.ToArray(), frame);
        }

        [Fact]
        public void BuildFrame_Should_Append_Word_Data_For_Write_Requests()
        {
            //Act
            var frame = EthernetConnector.BuildFrame(0, (byte)WriteCommands.WordBlockWrite, MemoryType.D, "0", 2, new byte[] { 0x12, 0x34, 0xAB, 0xCD });

            //Assert
            Assert.Equal(14 + 10 + 4 + 2, frame.Length);
            Assert.Equal(new byte[] { 0, 14 }, frame.Skip(12).Take(2));
            Assert.Equal(new byte[] { 0x12, 0x34, 0xAB, 0xCD }, frame.Skip(24).Take(4));
        }

        [Fact]
        public async Task WriteWordAsync_Should_Return_PLC_Error_Code()
        {
            //Arrange
            var connector = new EthernetConnector(new FakeEthernetSocket(x => WriteAck(x, (int)ResponseCode.InvalidDeviceAddress)));

            //Act
            var result = await connector.WriteWordAsync(MemoryType.D, "0", 1);

            //Assert
            Assert.Equal(ResponseCode.InvalidDeviceAddress, result);
        }

        [Fact]
        public async Task ReadWordAsync_Should_Return_Error_On_Truncated_Response()
        {
            //Arrange
            var connector = new EthernetConnector(new FakeEthernetSocket(x => x.Take(10).ToArray()));

            //Act
            var (responseCode, data) = await connector.ReadWordAsync(MemoryType.D, "0", 2);

            //Assert
            Assert.Equal(ResponseCode.WritingError, responseCode);
            Assert.Null(data);
        }

        [Fact]
        public async Task ReadWordAsync_Should_Return_Error_On_Wrong_Checksum()
        {
            //Arrange
            var connector = new EthernetConnector(new FakeEthernetSocket(x =>
            {
                var response = WriteAck(x);
                response[response.Length - 1]++;
                return response;
            }));

            //Act
            var result = await connector.WriteWordAsync(MemoryType.D, "0", 1);

            //Assert
            Assert.Equal(ResponseCode.WritingError, result);
        }

        [Fact]
        public async Task ReadWordAsync_Should_Return_SystemError_On_No_Response()
        {
            //Arrange
            var connector = new EthernetConnector(new FakeEthernetSocket(x => null));

            //Act
            var (responseCode, data) = await connector.ReadWordAsync(MemoryType.D, "0", 2);

            //Assert
            Assert.Equal(ResponseCode.SystemError, responseCode);
            Assert.Null(data);
        }

        [Fact]
        public async Task Operations_Should_Return_SystemError_When_Not_Connected_Without_AutoConnect()
        {
            //Arrange
            var connector = new EthernetConnector(new FakeEthernetSocket(x => WriteAck(x)), autoConnect: false);

            //Act
            var result = await connector.WriteWordAsync(MemoryType.D, "0", 1);

            //Assert
            Assert.Equal(ResponseCode.SystemError, result);
        }

        [Fact]
        public async Task AutoConnect_Should_Keep_Explicitly_Opened_Connection()
        {
            //Arrange
            var socket = new FakeEthernetSocket(x => WriteAck(x));
            var connector = new EthernetConnector(socket);
            await connector.Connect();

            //Act
            await connector.WriteWordAsync(MemoryType.D, "0", 1);
            await connector.WriteWordAsync(MemoryType.D, "0", 2);

            //Assert
            Assert.True(connector.IsConnected);
            Assert.Equal(1, socket.ConnectCount);
        }

        [Fact]
        public async Task AutoConnect_Should_Close_Automatically_Opened_Connection()
        {
            //Arrange
            var socket = new FakeEthernetSocket(x => WriteAck(x));
            var connector = new EthernetConnector(socket);

            //Act
            await connector.WriteWordAsync(MemoryType.D, "0", 1);

            //Assert
            Assert.False(connector.IsConnected);
        }
    }
}

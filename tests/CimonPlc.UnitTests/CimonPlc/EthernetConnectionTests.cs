using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using CimonPlc.Enums;
using CimonPlc.UnitTests.Simulators;
using Rony.Net;
using Xunit;
using Xunit.Abstractions;

namespace CimonPlc.UnitTests
{
    /// <summary>
    /// How <see cref="PlcConnectors.EthernetConnector"/> opens, reuses and closes TCP connections,
    /// checked on the mock server side with Rony.Net connection records.
    /// </summary>
    public class EthernetConnectionTests : PlcMockServerTest
    {
        public EthernetConnectionTests(ITestOutputHelper output) : base(output)
        {
            Plc.Attach(Server);
        }

        [Fact]
        public async Task Explicit_Connection_Should_Be_Reused_For_Every_Request()
        {
            //Arrange
            using var connector = CreateConnector();
            await connector.Connect();

            //Act
            await connector.WriteWordAsync(MemoryType.D, "0", 1);
            await connector.ReadWordAsync(MemoryType.D, "0", 1);
            await connector.WriteBitAsync(MemoryType.M, "0", 1);

            //Assert
            Assert.True(connector.IsConnected);
            Server.Should().HaveAcceptedConnections(Times.Once());
            Server.Connections[0].Should()
                .HaveReceived(r => true, Times.Exactly(3))
                .And.HaveReceivedInOrder(
                    r => r.Body[10] == (byte)WriteCommands.WordBlockWrite,
                    r => r.Body[10] == (byte)ReadCommand.WordBlockRead,
                    r => r.Body[10] == (byte)WriteCommands.BitBlockWrite);
        }

        [Fact]
        public async Task Disconnect_Should_Close_The_Connection()
        {
            //Arrange
            using var connector = CreateConnector();
            await connector.Connect();
            await Server.WaitForConnectionAsync();

            //Act
            var result = connector.Disconnect();

            //Assert
            Assert.Equal(ConnectionStatus.DisConnected, result);
            await Server.Connections[0].WaitForCloseAsync();
            Server.Should().HaveNoOpenConnections();
        }

        [Fact]
        public async Task Dispose_Should_Close_The_Connection()
        {
            //Arrange
            var connector = CreateConnector();
            await connector.Connect();
            await Server.WaitForConnectionAsync();

            //Act
            connector.Dispose();

            //Assert
            await Server.WaitForAllConnectionsClosedAsync();
            Server.Should().HaveNoOpenConnections();
        }

        [Fact]
        public async Task AutoConnect_Should_Open_And_Close_A_Connection_For_Every_Request()
        {
            //Arrange
            using var connector = CreateConnector();

            //Act
            var first = await connector.WriteWordAsync(MemoryType.D, "0", 1);
            var second = await connector.WriteWordAsync(MemoryType.D, "0", 2);

            //Assert
            Assert.Equal(ResponseCode.Success, first);
            Assert.Equal(ResponseCode.Success, second);
            Assert.False(connector.IsConnected);
            await Server.WaitForAllConnectionsClosedAsync();
            Server.Should().HaveAcceptedConnections(Times.Exactly(2)).And.HaveNoOpenConnections();
            Assert.All(Server.Connections, c => c.Should().HaveReceived(r => true, Times.Once()));
        }

        [Fact]
        public async Task Operations_Should_Not_Connect_Without_AutoConnect()
        {
            //Arrange
            using var connector = CreateConnector(autoConnect: false);

            //Act
            var result = await connector.WriteWordAsync(MemoryType.D, "0", 1);

            //Assert
            Assert.Equal(ResponseCode.SystemError, result);
            Server.Should().HaveAcceptedConnections(Times.Never());
        }

        [Fact]
        public async Task Concurrent_Requests_Should_Be_Sent_One_At_A_Time()
        {
            //Arrange
            // Word addresses must end with 0, so request i reads address i * 16
            for (var i = 0; i < 20; i++)
                Plc.SetWord(MemoryType.D, i * 16, i);
            using var connector = CreateConnector();
            await connector.Connect();

            //Act
            var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(i => connector.ReadWordAsync(MemoryType.D, $"{i:X}0", 1)));

            //Assert : every answer belongs to its own request
            Assert.All(results, x => Assert.Equal(ResponseCode.Success, x.responseCode));
            Assert.Equal(Enumerable.Range(0, 20), results.Select(x => x.data.Single()));
            Server.Should().HaveAcceptedConnections(Times.Once());
            Assert.Equal(20, Server.ReceivedRequests.Select(r => r.Body[9]).Distinct().Count());
        }

        [Fact]
        public async Task Connect_Should_Throw_While_Plc_Refuses_Connections()
        {
            //Arrange
            using var connector = CreateConnector();
            Server.RefuseConnections();

            //Assert
            await Assert.ThrowsAnyAsync<SocketException>(() => connector.Connect());

            //Act : the PLC is back
            Server.AcceptConnections();
            var result = await connector.Connect();

            //Assert
            Assert.Equal(ConnectionStatus.Connected, result);
        }
    }
}

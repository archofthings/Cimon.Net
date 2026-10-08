using CimonPlc.PlcConnectors;
using CimonPlc.Sockets;
using Rony.Interfaces;
using Rony.Listeners;
using Rony.Net.Xunit;
using Xunit.Abstractions;

namespace CimonPlc.UnitTests.Simulators
{
    /// <summary>
    /// Base class for tests that talk to a simulated PLC over a real TCP socket.
    /// <see cref="MockServerTest"/> (Rony.Net.Xunit) gives every test its own mock server on a free port,
    /// writes the server log to the test output and disposes the server after the test.
    /// </summary>
    public abstract class PlcMockServerTest : MockServerTest
    {
        protected PlcMockServerTest(ITestOutputHelper output) : base(output)
        {
            // Strict mode: a request without a configured response fails the test.
            VerifyAllRequestsMatchedAfterTest = true;
        }

        /// <summary>
        /// PLC memory and frame helpers. Attach it to <see cref="MockServerTest.Server"/> to answer like a PLC.
        /// </summary>
        protected CimonPlcSimulator Plc { get; } = new CimonPlcSimulator();

        // A free port, and requests split by the length field of Cimon frames.
        protected override IListener CreateListener() => new TcpServer(0) { Framing = CimonFraming.Instance };

        /// <summary>
        /// A connector for the mock server. Ping is disabled, the server is on the loopback address.
        /// </summary>
        protected EthernetConnector CreateConnector(bool autoConnect = true)
        {
            return new EthernetConnector(new TcpSocket("127.0.0.1", Server.Port, usePing: false), autoConnect);
        }
    }
}

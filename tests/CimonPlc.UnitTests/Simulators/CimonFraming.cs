using System;
using System.Collections.Generic;
using Rony.Interfaces;

namespace CimonPlc.UnitTests.Simulators
{
    /// <summary>
    /// Rony.Net message framing for Cimon Ethernet frames. TCP does not keep message boundaries,
    /// so the mock server uses the length field of the frame header to find where every request
    /// ends: header (14 bytes) + data (length field) + checksum (2 bytes).
    /// Requests that arrive in pieces, or several in one burst, are still matched one frame at a time.
    /// </summary>
    public sealed class CimonFraming : IMessageFraming
    {
        public static readonly CimonFraming Instance = new CimonFraming();

        private CimonFraming()
        {
        }

        public IReadOnlyList<byte[]> Decode(ReadOnlySpan<byte> data, bool endOfBurst, out int consumed)
        {
            var messages = new List<byte[]>();
            consumed = 0;

            while (data.Length - consumed >= EthernetFrame.HeaderLength)
            {
                var frame = data.Slice(consumed);
                var length = EthernetFrame.HeaderLength + Tools.ToInt(frame[12], frame[13]) + EthernetFrame.CheckSumLength;
                if (frame.Length < length)
                    break;

                messages.Add(frame.Slice(0, length).ToArray());
                consumed += length;
            }

            return messages;
        }

        // Responses built by the simulator are complete frames already.
        public byte[] Encode(byte[] message) => message;
    }
}

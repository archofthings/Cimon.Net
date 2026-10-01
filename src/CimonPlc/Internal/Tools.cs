using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;

namespace CimonPlc
{
    internal static class Tools
    {
        public static IPStatus Ping(IPAddress ip, int pingTimeout = 300)
        {
            try
            {
                using var pingSender = new Ping();
                var reply = pingSender.Send(ip, pingTimeout);
                return reply.Status;
            }
            catch
            {
                return IPStatus.BadDestination;
            }
        }

        public static int ToInt(byte byte1, byte byte2)
        {
            return (byte1 << 8) + byte2;
        }

        public static byte ToByte(char char1, char char2)
        {
            return byte.Parse(string.Concat(char1, char2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        public static byte[] ToDualByte(this int number)
        {
            number &= 0xFFFF;
            return new byte[] { (byte)(number >> 8), (byte)(number & 0xFF) };
        }

        public static char[] ToDualChar(this byte number)
        {
            return number.ToString("X2", CultureInfo.InvariantCulture).ToCharArray();
        }

        public static char[] ToQuadChar(this int number)
        {
            number &= 0xFFFF;
            return number.ToString("X4", CultureInfo.InvariantCulture).ToCharArray();
        }

        /// <summary>
        /// Calculates the BCC of a serial frame and appends it as two hex chars.
        /// The first 3 chars (header + station number) are excluded from the calculation.
        /// </summary>
        public static void AddBCC(this List<char> input)
        {
            if (input == null || input.Count == 0)
                return;

            var sum = input.Skip(3).Sum(x => Convert.ToByte(x));
            sum %= 256;

            input.AddRange(((byte)sum).ToDualChar());
        }

        /// <summary>
        /// Two-byte checksum used in Ethernet frames: the lower 2 bytes of the byte-wise sum.
        /// </summary>
        public static byte[] CheckSum(IEnumerable<byte> frame)
        {
            return frame.Sum(x => x).ToDualByte();
        }

        /// <summary>
        /// Validates the header and checksum of an Ethernet response frame.
        /// </summary>
        public static bool IsValidResponse(byte[] buffer, byte frameNo)
        {
            if (buffer == null || buffer.Length < EthernetFrame.MinimumLength)
                return false;

            //[0-8] ID : This is a 9 - byte string "KDT_PLC_S".
            if (Encoding.ASCII.GetString(buffer, 0, 9) != EthernetFrame.SlaveId)
                return false;

            //[9] Frame No : The value that 128 are added to the number of the
            //      command frame received from the Master is used
            if (buffer[9] - 128 != frameNo)
                return false;

            //[12-13] Length : The frame must be long enough to hold the declared data
            if (buffer.Length < EthernetFrame.ExpectedLength(buffer))
                return false;

            //Check Sum : This is 2-byte value. After the entire frame is binary-summed by the byte,
            //the lower 2 - byte in the result value is used.
            var checkSum = CheckSum(buffer.Take(buffer.Length - 2));
            return checkSum[0] == buffer[buffer.Length - 2] && checkSum[1] == buffer[buffer.Length - 1];
        }

        /// <summary>
        /// Validates the framing (STX ... ETX) and command of a serial response frame.
        /// </summary>
        public static bool IsValidSerialResponse(char[] receivedFrame, byte ackCommand)
        {
            if (receivedFrame == null || receivedFrame.Length < SerialFrame.MinimumLength)
                return false;

            if (receivedFrame[0] != SerialFrame.Stx || receivedFrame[receivedFrame.Length - 1] != SerialFrame.Etx)
                return false;

            //[3] Cmd : In Slave, 1 - byte command must be equal to sent command
            return receivedFrame[3] == (char)ackCommand;
        }

        public static bool IsHex(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }
    }

    internal static class EthernetFrame
    {
        public const string MasterId = "KDT_PLC_M";
        public const string SlaveId = "KDT_PLC_S";

        /// <summary>ID(9) + Frame No(1) + Cmd(1) + Res(1) + Length(2)</summary>
        public const int HeaderLength = 14;

        public const int CheckSumLength = 2;

        public const int MinimumLength = HeaderLength + CheckSumLength;

        /// <summary>Command used by the PLC to acknowledge writes and to report errors.</summary>
        public const byte AckCommand = 0x41;

        /// <summary>
        /// Total length of a frame according to the length field in its header,
        /// or -1 if the header is not complete yet.
        /// </summary>
        public static int ExpectedLength(IReadOnlyList<byte> buffer)
        {
            if (buffer.Count < HeaderLength)
                return -1;

            return HeaderLength + Tools.ToInt(buffer[12], buffer[13]) + CheckSumLength;
        }
    }

    internal static class SerialFrame
    {
        public const char Enq = (char)0x05;
        public const char Eot = (char)0x04;
        public const char Stx = (char)0x02;
        public const char Etx = (char)0x03;
        public const char ErrorCommand = 'E';

        /// <summary>STX(1) + Station(2) + Cmd(1) + Length/Code(2) + ETX(1)</summary>
        public const int MinimumLength = 7;
    }
}

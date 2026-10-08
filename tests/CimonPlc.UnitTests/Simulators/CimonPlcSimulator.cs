using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CimonPlc.Enums;
using Rony.Net;

namespace CimonPlc.UnitTests.Simulators
{
    /// <summary>
    /// A Cimon PLC simulated with a Rony.Net mock server. It keeps word and bit memory, so values
    /// written by a connector can be read back, and it builds the slave frames used to script
    /// custom responses (errors, wrong frame numbers, ...).
    /// </summary>
    public class CimonPlcSimulator
    {
        // Offsets of the master frame: header, then memory type (1) + 0 (1) + address (6) + count (2) + payload
        private const int MemoryTypeOffset = EthernetFrame.HeaderLength;
        private const int AddressOffset = EthernetFrame.HeaderLength + 2;
        private const int CountOffset = EthernetFrame.HeaderLength + 8;
        private const int PayloadOffset = EthernetFrame.HeaderLength + 10;

        private readonly ConcurrentDictionary<(MemoryType, int), int> _words = new ConcurrentDictionary<(MemoryType, int), int>();
        private readonly ConcurrentDictionary<(MemoryType, int), byte> _bits = new ConcurrentDictionary<(MemoryType, int), byte>();

        /// <summary>
        /// Adds one rule per command to the server, answering like a PLC that keeps its memory.
        /// </summary>
        public CimonPlcSimulator Attach(MockServer server)
        {
            server.Mock.SendMatchingBytes(IsCommand(ReadCommand.WordBlockRead)).Receive(ReadWords);
            server.Mock.SendMatchingBytes(IsCommand(ReadCommand.BitBlockRead)).Receive(ReadBits);
            server.Mock.SendMatchingBytes(IsCommand(WriteCommands.WordBlockWrite)).Receive(WriteWords);
            server.Mock.SendMatchingBytes(IsCommand(WriteCommands.BitBlockWrite)).Receive(WriteBits);
            return this;
        }

        public int GetWord(MemoryType memoryType, int address) => _words.TryGetValue((memoryType, address), out var value) ? value : 0;

        public void SetWord(MemoryType memoryType, int address, int value) => _words[(memoryType, address)] = value;

        public byte GetBit(MemoryType memoryType, int address) => _bits.TryGetValue((memoryType, address), out var value) ? value : (byte)0;

        public void SetBit(MemoryType memoryType, int address, byte value) => _bits[(memoryType, address)] = value;

        public byte[] ReadWords(byte[] request)
        {
            var (memoryType, address, count) = ParseRequest(request);
            var data = Enumerable.Range(address, count).SelectMany(x => GetWord(memoryType, x).ToDualByte());
            return DataResponse(request, data);
        }

        public byte[] ReadBits(byte[] request)
        {
            var (memoryType, address, count) = ParseRequest(request);
            var data = Enumerable.Range(address, count).Select(x => GetBit(memoryType, x));
            return DataResponse(request, data);
        }

        public byte[] WriteWords(byte[] request)
        {
            var (memoryType, address, count) = ParseRequest(request);
            for (var i = 0; i < count; i++)
                SetWord(memoryType, address + i, Tools.ToInt(request[PayloadOffset + i * 2], request[PayloadOffset + i * 2 + 1]));

            return Ack(request);
        }

        public byte[] WriteBits(byte[] request)
        {
            var (memoryType, address, count) = ParseRequest(request);
            for (var i = 0; i < count; i++)
                SetBit(memoryType, address + i, request[PayloadOffset + i]);

            return Ack(request);
        }

        public static Func<byte[], bool> IsCommand(ReadCommand command) => frame => frame[10] == (byte)command;

        public static Func<byte[], bool> IsCommand(WriteCommands command) => frame => frame[10] == (byte)command;

        /// <summary>
        /// The answer to a write, or the error answer to any request: command 0x41 and a response code.
        /// </summary>
        public static byte[] Ack(byte[] request, ResponseCode code = ResponseCode.Success)
        {
            return SlaveFrame(request[9], EthernetFrame.AckCommand, ((int)code).ToDualByte());
        }

        /// <summary>
        /// The answer to a read: the request command, the memory address block and the read data.
        /// </summary>
        public static byte[] DataResponse(byte[] request, IEnumerable<byte> data)
        {
            // Memory address block (9 bytes): memory type + 0 + address (6) + 0
            var block = request.Skip(MemoryTypeOffset).Take(8).Concat(new byte[] { 0 });
            return SlaveFrame(request[9], request[10], block.Concat(data));
        }

        public static byte[] SlaveFrame(byte frameNo, byte command, IEnumerable<byte> data)
        {
            var body = data.ToArray();
            var frame = new List<byte>();
            frame.AddRange(Encoding.ASCII.GetBytes(EthernetFrame.SlaveId));
            frame.Add((byte)(frameNo + 128));
            frame.Add(command);
            frame.Add(0);
            frame.AddRange(body.Length.ToDualByte());
            frame.AddRange(body);
            frame.AddRange(Tools.CheckSum(frame));
            return frame.ToArray();
        }

        public static (MemoryType memoryType, int address, int count) ParseRequest(byte[] request)
        {
            var memoryType = (MemoryType)request[MemoryTypeOffset];
            var address = int.Parse(Encoding.ASCII.GetString(request, AddressOffset, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var count = Tools.ToInt(request[CountOffset], request[CountOffset + 1]);
            return (memoryType, address, count);
        }
    }
}

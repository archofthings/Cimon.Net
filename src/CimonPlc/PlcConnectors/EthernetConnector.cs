using System;
using CimonPlc.Interfaces;
using CimonPlc.Enums;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using CimonPlc.Models;

namespace CimonPlc.PlcConnectors
{
    public class EthernetConnector : PlcConnector
    {
        private const int MaxFrameNo = 127;
        private readonly object _frameNoLock = new object();
        private byte _frameNo;

        public EthernetConnector(IEthernetSocket socket, bool autoConnect = true) : base(socket, autoConnect)
        {
        }

        /// <summary>
        /// Number of the next frame sent to PLC, in the range from 0 to 127.
        /// Reading this property consumes the number.
        /// </summary>
        public byte FrameNo
        {
            get
            {
                lock (_frameNoLock)
                {
                    var frameNo = _frameNo;
                    _frameNo = (byte)(_frameNo >= MaxFrameNo ? 0 : _frameNo + 1);
                    return frameNo;
                }
            }
        }

        /// <summary>
        ///     This function is to assign PLC device memory directly to read according to
        ///     memory data type.The data can be assigned up to 16 pieces repeatedly.The
        ///     total sum of the word data should be not over 512 words.
        /// </summary>
        /// <param name="memoryType">PLC memory which required to read such as X or D</param>
        /// <param name="address">The word address or the card number of a corresponding device is used. That is, in case of bit device such as X/Y, the last number should be '0' and should contains 6 character, such as '000010'</param>
        /// <param name="length">Requested length for reading memory. Length must be in the range from 1 to 512</param>
        /// <returns>Returns a tuple includes PLC response code and an array of words contains read data</returns>
        public override async Task<(ResponseCode responseCode, int[] data)> ReadWordAsync(MemoryType memoryType, string address, int length)
        {
            Guard.Against.UndefinedEnum(memoryType, nameof(memoryType));
            Guard.Against.OutOfRange(length, nameof(length), 1, 512);
            Guard.Against.BadFormat(address, nameof(address), "[0-9a-fA-F]{0,5}0");

            var (error, response) = await SendCommandAsync((byte)ReadCommand.WordBlockRead, memoryType, address, length).ConfigureAwait(false);
            if (error != ResponseCode.Success)
                return (error, null);

            var data = ReadData(response);
            var words = new int[data.Length / 2];
            for (var i = 0; i < words.Length; i++)
                words[i] = Tools.ToInt(data[i * 2], data[i * 2 + 1]);

            return (ResponseCode.Success, words);
        }

        /// <summary>
        ///     This function is to assign PLC device memory directly to read bit block. The data
        ///     can be assigned up to 16 pieces repeatedly.
        ///     But, The total sum of the word data is not to be over 1024 bits.
        /// </summary>
        /// <param name="memoryType">PLC memory which required to read such as X or D</param>
        /// <param name="address">The word address or the card number of a corresponding device is used, it should contains 6 characters, such as '0000A1'</param>
        /// <param name="length">Requested length for reading memory. Length must be in the range from 1 to 1024</param>
        /// <returns>Returns a tuple includes PLC response code and an array of byte contains read data</returns>
        public override async Task<(ResponseCode responseCode, byte[] data)> ReadBitAsync(MemoryType memoryType, string address, int length)
        {
            Guard.Against.UndefinedEnum(memoryType, nameof(memoryType));
            Guard.Against.OutOfRange(length, nameof(length), 1, 1024);
            Guard.Against.BadFormat(address, nameof(address), "[0-9a-fA-F]{1,6}");

            var (error, response) = await SendCommandAsync((byte)ReadCommand.BitBlockRead, memoryType, address, length).ConfigureAwait(false);
            if (error != ResponseCode.Success)
                return (error, null);

            return (ResponseCode.Success, ReadData(response));
        }

        /// <summary>
        ///     This function is to assign PLC device memory directly to write according to
        ///     memory data type.The data can be assigned up to 16 pieces repeatedly.
        ///     But, The total sum of word data is not to be over 64 words.
        /// </summary>
        /// <param name="memoryType">PLC memory which required to write such as Y or D</param>
        /// <param name="address">The word address or the card number of a corresponding device is used. That is, in case of bit device such as X/Y, the last number should be '0' and should contains 6 characters, such as '000010'</param>
        /// <param name="data">Data needed to write on PLC memory, The total sum of word data is not to be over 64 words</param>
        /// <returns>Returns a code which shows PLC response code</returns>
        public override async Task<ResponseCode> WriteWordAsync(MemoryType memoryType, string address, params int[] data)
        {
            Guard.Against.NullOrEmpty(data, nameof(data));
            Guard.Against.OutOfRange(data, nameof(data), 0, 0xFFFF);
            Guard.Against.OutOfRange(data.Length, nameof(data), 1, 64);
            Guard.Against.UndefinedEnum(memoryType, nameof(memoryType));
            Guard.Against.BadFormat(address, nameof(address), "[0-9a-fA-F]{0,5}0");

            var payload = data.SelectMany(x => x.ToDualByte());
            var (error, _) = await SendCommandAsync((byte)WriteCommands.WordBlockWrite, memoryType, address, data.Length, payload).ConfigureAwait(false);
            return error;
        }

        /// <summary>
        ///     This function is to assign PLC device memory directly to write according to
        ///     memory data type.The data can be assigned up to 16 pieces repeatedly.
        ///     But,  The total sum of the data is not to be over 256 bits.
        /// </summary>
        /// <param name="memoryType">PLC memory which required to write such as Y or D</param>
        /// <param name="address">The word address or the card number of a corresponding device is used, it should contains 6 characters, such as '0000D1'</param>
        /// <param name="data">Data needed to write on PLC memory, each item must be 0 or 1. The total sum of data is not to be over 256 bits</param>
        /// <returns>Returns a code which shows PLC response code</returns>
        public override async Task<ResponseCode> WriteBitAsync(MemoryType memoryType, string address, params byte[] data)
        {
            Guard.Against.NullOrEmpty(data, nameof(data));
            Guard.Against.OutOfRange<byte>(data, nameof(data), 0, 1);
            Guard.Against.OutOfRange(data.Length, nameof(data), 1, 256);
            Guard.Against.UndefinedEnum(memoryType, nameof(memoryType));
            Guard.Against.BadFormat(address, nameof(address), "[0-9a-fA-F]{1,6}");

            var (error, _) = await SendCommandAsync((byte)WriteCommands.BitBlockWrite, memoryType, address, data.Length, data).ConfigureAwait(false);
            return error;
        }

        /// <summary>
        /// Builds a master frame, sends it and validates the PLC response.
        /// </summary>
        /// <returns>Success and the response frame, or the error code returned by PLC or detected locally</returns>
        private async Task<(ResponseCode error, byte[] response)> SendCommandAsync(byte command, MemoryType memoryType, string address, int count, IEnumerable<byte> payload = null)
        {
            var frameNo = FrameNo;
            var frame = BuildFrame(frameNo, command, memoryType, address, count, payload);

            var (error, response) = await ExchangeAsync(frame).ConfigureAwait(false);
            if (error != ResponseCode.Success)
                return (error, null);

            if (!Tools.IsValidResponse(response, frameNo))
                return (ResponseCode.WritingError, null);

            //[10] Cmd : For writes the PLC always answers with 0x41 and a response code.
            //For reads, 0x41 means the request failed and a response code is returned instead of data.
            var isWrite = command == (byte)WriteCommands.WordBlockWrite || command == (byte)WriteCommands.BitBlockWrite;
            if (response[10] == EthernetFrame.AckCommand)
            {
                if (response.Length < EthernetFrame.HeaderLength + 2 + EthernetFrame.CheckSumLength)
                    return (ResponseCode.WritingError, null);

                var code = (ResponseCode)Tools.ToInt(response[14], response[15]);
                if (!isWrite && code == ResponseCode.Success)
                    return (ResponseCode.WritingError, null);

                return (code, response);
            }

            if (isWrite || response[10] != command)
                return (ResponseCode.WritingError, null);

            return (ResponseCode.Success, response);
        }

        internal static byte[] BuildFrame(byte frameNo, byte command, MemoryType memoryType, string address, int count, IEnumerable<byte> payload = null)
        {
            //[14-n] Data : This is n block and contains up to 3 parts :
            //      1- Memory Address : 8 bytes, device type + 0 + 6 chars address
            //      2- Read/Write Size : 2 bytes
            //      3- Data to write : n bytes (write commands only)
            var data = new List<byte> { (byte)memoryType, 0 };
            data.AddRange(Encoding.ASCII.GetBytes(NormalizeAddress(address)));
            data.AddRange(count.ToDualByte());
            if (payload != null)
                data.AddRange(payload);

            var frame = new List<byte>(EthernetFrame.MinimumLength + data.Count);

            //[0-8] ID : This is a 9 - byte string "KDT_PLC_M".
            frame.AddRange(Encoding.ASCII.GetBytes(EthernetFrame.MasterId));

            //[9] Frame No : This, 1 - byte data with the range from 0 to 127
            frame.Add(frameNo);

            //[10] Cmd : In Master, 1 - byte command can be used and the
            //format of 'Data' field is selected according to each command
            frame.Add(command);

            //[11] Res: Reserved. (1 Byte, 00h)
            frame.Add(0);

            //[12-13] Length : This is the 2 - byte value indicating the size of 'Data' field.
            frame.AddRange(data.Count.ToDualByte());

            frame.AddRange(data);

            //Check Sum : This is 2-byte value. After the entire frame is binary-summed by the byte,
            //the lower 2 - byte in the result value is used.
            frame.AddRange(Tools.CheckSum(frame));

            return frame.ToArray();
        }

        /// <summary>
        /// Extracts read data from a response frame. The data field starts with the 9 bytes
        /// memory address block, followed by the read values.
        /// </summary>
        private static byte[] ReadData(byte[] response)
        {
            const int addressLength = 9;
            var dataLength = Tools.ToInt(response[12], response[13]) - addressLength;
            if (dataLength <= 0)
                return new byte[0];

            var data = new byte[dataLength];
            Array.Copy(response, EthernetFrame.HeaderLength + addressLength, data, 0, dataLength);
            return data;
        }
    }
}

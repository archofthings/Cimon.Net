using CimonPlc.Interfaces;
using CimonPlc.Enums;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using CimonPlc.Models;

namespace CimonPlc.PlcConnectors
{
    public class SerialConnector : PlcConnector
    {
        public SerialConnector(ISerialSocket socket, bool autoConnect = true) : base(socket, autoConnect)
        {
        }

        /// <summary>
        ///     This function is to assign PLC device memory directly to read according to
        ///     memory data type.The total sum of the word data should be not over 63 words.
        /// </summary>
        /// <param name="memoryType">PLC memory which required to read. Valid symbols are X, Y, M, L, K, F, T, C, D, S</param>
        /// <param name="address">The word address or the card number of a corresponding device is used. That is, in case of bit device such as X/Y, the last number should be '0' and should contains 6 character, such as '000010'</param>
        /// <param name="length">Requested length for reading memory. Length must be in the range from 1 to 63</param>
        /// <returns>Returns a tuple includes PLC response code and an array of words contains read data</returns>
        public override async Task<(ResponseCode responseCode, int[] data)> ReadWordAsync(MemoryType memoryType, string address, int length)
        {
            Guard.Against.UndefinedEnum(memoryType, nameof(memoryType));
            Guard.Against.OutOfRange(length, nameof(length), 1, 63);
            Guard.Against.BadFormat(address, nameof(address), "[0-9a-fA-F]{0,5}0");

            var (error, response) = await SendCommandAsync((byte)ReadCommand.WordBlockRead, memoryType, address, length).ConfigureAwait(false);
            if (error != ResponseCode.Success)
                return (error, null);

            // Each word is sent as 4 hex chars
            var data = ReadData(response);
            var words = new int[data.Length / 4];
            for (var i = 0; i < words.Length; i++)
            {
                var x = i * 4;
                words[i] = Tools.ToInt(Tools.ToByte(data[x], data[x + 1]), Tools.ToByte(data[x + 2], data[x + 3]));
            }

            return (ResponseCode.Success, words);
        }

        /// <summary>
        ///     This function is to assign PLC device memory directly to read bit block.
        ///     The total sum of the data is not to be over 126 bits.
        /// </summary>
        /// <param name="memoryType">PLC memory which required to read such as X or D</param>
        /// <param name="address">The word address or the card number of a corresponding device is used, it should contains 6 characters, such as '0000A1'</param>
        /// <param name="length">Requested length for reading memory. Length must be in the range from 1 to 126</param>
        /// <returns>Returns a tuple includes PLC response code and an array of byte contains read data</returns>
        public override async Task<(ResponseCode responseCode, byte[] data)> ReadBitAsync(MemoryType memoryType, string address, int length)
        {
            Guard.Against.UndefinedEnum(memoryType, nameof(memoryType));
            Guard.Against.OutOfRange(length, nameof(length), 1, 126);
            Guard.Against.BadFormat(address, nameof(address), "[0-9a-fA-F]{1,6}");

            var (error, response) = await SendCommandAsync((byte)ReadCommand.BitBlockRead, memoryType, address, length).ConfigureAwait(false);
            if (error != ResponseCode.Success)
                return (error, null);

            // Each bit is sent as 2 hex chars
            var data = ReadData(response);
            var bits = new byte[data.Length / 2];
            for (var i = 0; i < bits.Length; i++)
                bits[i] = Tools.ToByte(data[i * 2], data[i * 2 + 1]);

            return (ResponseCode.Success, bits);
        }

        /// <summary>
        ///     This function is to assign PLC device memory directly to write according to
        ///     memory data type. The total sum of word data is not to be over 61 words.
        /// </summary>
        /// <param name="memoryType">PLC memory which required to write such as Y or D</param>
        /// <param name="address">The word address or the card number of a corresponding device is used. That is, in case of bit device such as X/Y, the last number should be '0' and should contains 6 characters, such as '000010'</param>
        /// <param name="data">Data needed to write on PLC memory. The frame length field is limited to 255 chars, so up to 61 words can be written at once</param>
        /// <returns>Returns a code which shows PLC response code</returns>
        public override async Task<ResponseCode> WriteWordAsync(MemoryType memoryType, string address, params int[] data)
        {
            Guard.Against.NullOrEmpty(data, nameof(data));
            Guard.Against.OutOfRange(data, nameof(data), 0, 0xFFFF);
            Guard.Against.OutOfRange(data.Length, nameof(data), 1, 61);
            Guard.Against.UndefinedEnum(memoryType, nameof(memoryType));
            Guard.Against.BadFormat(address, nameof(address), "[0-9a-fA-F]{0,5}0");

            // 4 chars per word
            var payload = data.SelectMany(x => x.ToQuadChar());
            var (error, _) = await SendCommandAsync((byte)WriteCommands.WordBlockWrite, memoryType, address, data.Length, payload).ConfigureAwait(false);
            return error;
        }

        /// <summary>
        ///     This function is to assign PLC device memory directly to write according to
        ///     memory data type.The total sum of the data is not to be over 126 bits.
        /// </summary>
        /// <param name="memoryType">PLC memory which required to write such as Y or D</param>
        /// <param name="address">The word address or the card number of a corresponding device is used, it should contains 6 characters, such as '0000D1'</param>
        /// <param name="data">Data needed to write on PLC memory, each item must be 0 or 1. The total sum of data is not to be over 126 bits</param>
        /// <returns>Returns a code which shows PLC response code</returns>
        public override async Task<ResponseCode> WriteBitAsync(MemoryType memoryType, string address, params byte[] data)
        {
            Guard.Against.NullOrEmpty(data, nameof(data));
            Guard.Against.OutOfRange<byte>(data, nameof(data), 0, 1);
            Guard.Against.OutOfRange(data.Length, nameof(data), 1, 126);
            Guard.Against.UndefinedEnum(memoryType, nameof(memoryType));
            Guard.Against.BadFormat(address, nameof(address), "[0-9a-fA-F]{1,6}");

            // 1 char per bit
            var payload = data.Select(x => (char)x);
            var (error, _) = await SendCommandAsync((byte)WriteCommands.BitBlockWrite, memoryType, address, data.Length, payload).ConfigureAwait(false);
            return error;
        }

        /// <summary>
        /// Builds a master frame, sends it and validates the PLC response.
        /// </summary>
        /// <returns>Success and the response frame, or the error code returned by PLC or detected locally</returns>
        private async Task<(ResponseCode error, char[] response)> SendCommandAsync(byte command, MemoryType memoryType, string address, int count, IEnumerable<char> payload = null)
        {
            var frame = BuildFrame(command, memoryType, address, count, payload);

            var (error, rawResponse) = await ExchangeAsync(frame).ConfigureAwait(false);
            if (error != ResponseCode.Success)
                return (error, null);

            var response = rawResponse.Select(x => (char)x).ToArray();

            //If response's cmd equals to E(0x45), then the error code is in [6-7]
            if (response.Length >= 8 && response[3] == SerialFrame.ErrorCommand)
                return (ParseCode(response[6], response[7]), null);

            if (!Tools.IsValidSerialResponse(response, command))
                return (ResponseCode.WritingError, null);

            var isWrite = command == (byte)WriteCommands.WordBlockWrite || command == (byte)WriteCommands.BitBlockWrite;
            if (isWrite)
                return (ParseCode(response[4], response[5]), response);

            //[4-5] Length : number of data chars, which must all be present before ETX
            if (!Tools.IsHex(response[4]) || !Tools.IsHex(response[5]))
                return (ResponseCode.WritingError, null);

            var dataLength = Tools.ToByte(response[4], response[5]);
            if (response.Length < 6 + dataLength + 1 || response.Skip(6).Take(dataLength).Any(x => !Tools.IsHex(x)))
                return (ResponseCode.WritingError, null);

            return (ResponseCode.Success, response);
        }

        internal static byte[] BuildFrame(byte command, MemoryType memoryType, string address, int count, IEnumerable<char> payload = null)
        {
            //[6-n] Data : This is n block and contains up to 3 parts :
            //      1- Memory Address : 8 chars, device symbol + 0 + 6 chars address
            //      2- Read/Write Size : 2 chars
            //      3- Data to write : n chars (write commands only)
            var data = new List<char> { memoryType.ToString()[0], '0' };
            data.AddRange(NormalizeAddress(address));
            data.AddRange(((byte)count).ToDualChar());
            if (payload != null)
                data.AddRange(payload);

            var frame = new List<char>(data.Count + 9);

            //[0] HEADER : This is 1-byte control letter of ASCII code "ENQ".
            frame.Add(SerialFrame.Enq);

            //[1-2] PLC Station Number : multi-drop communication is available if station numbers are assigned
            frame.Add('0');
            frame.Add('0');

            //[3] Cmd : In Master, 1 - byte command can be used and the
            //format of 'Data' field is selected according to each command
            frame.Add((char)command);

            //[4-5] Length : Total number of the chars of the data field
            frame.AddRange(((byte)data.Count).ToDualChar());

            frame.AddRange(data);

            //BCC : is the remainder value when dividing the binary-sum from Cmd to the end of data by 256.
            frame.AddBCC();

            //End : This is 1-byte control letter of ASCII code "EOT".
            frame.Add(SerialFrame.Eot);

            return frame.Select(Convert.ToByte).ToArray();
        }

        private static char[] ReadData(char[] response)
        {
            var dataLength = Tools.ToByte(response[4], response[5]);
            var data = new char[dataLength];
            Array.Copy(response, 6, data, 0, dataLength);
            return data;
        }

        private static ResponseCode ParseCode(char char1, char char2)
        {
            if (!Tools.IsHex(char1) || !Tools.IsHex(char2))
                return ResponseCode.WritingError;

            return (ResponseCode)Tools.ToByte(char1, char2);
        }
    }
}

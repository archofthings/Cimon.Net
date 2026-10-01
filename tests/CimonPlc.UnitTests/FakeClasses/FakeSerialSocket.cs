using CimonPlc.Enums;
using CimonPlc.Interfaces;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;

namespace CimonPlc.UnitTests.FakeClasses
{
    public class FakeSerialSocket : ISerialSocket
    {
        public bool IsConnected => true;

        public byte Command { get; private set; }
        public int Length { get; private set; }

        public async Task<ConnectionStatus> Connect(int readTimeout = 1000, int writeTimeout = 1000, int pingTimeout = 3000)
        {
            await Task.Delay(100);
            return ConnectionStatus.Connected;
        }

        public ConnectionStatus Disconnect()
        {
            return ConnectionStatus.DisConnected;
        }

        public async Task<byte[]> ReceiveData()
        {
            await Task.Delay(100);
            var frame = new List<char> { (char)0x2, '0', '0', (char)Command };

            switch (Command)
            {
                case (byte)WriteCommands.BitBlockWrite:
                    frame.Add('0');
                    frame.Add('0');
                    break;
                case (byte)WriteCommands.WordBlockWrite:
                    frame.Add('0');
                    frame.Add('0');
                    break;
                case (byte)ReadCommand.BitBlockRead:
                    frame.AddRange(((byte)(Length * 2)).ToDualChar());
                    for (var i = 0; i < Length * 2; i++)
                        frame.Add('F');
                    break;
                case (byte)ReadCommand.WordBlockRead:
                    frame.AddRange(((byte)(Length * 4)).ToDualChar());
                    for (var i = 0; i < Length * 4; i++)
                        frame.Add('F');
                    break;
            }
            frame.AddBCC();
            frame.Add((char)3);

            return frame.Select(Convert.ToByte).ToArray();
        }

        public async Task<bool> SendData(byte[] frame)
        {
            await Task.Delay(100);
            Command = frame[3];
            Length = Tools.ToByte((char)frame[14], (char)frame[15]);
            return true;
        }
    }
}

﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ionic.Crc;

namespace Telegram.Net.Core.Network
{
    public class TcpMessage
    {
        public readonly int sequneceNumber;
        public readonly byte[] body;
        public CancellationToken CancellationToken;
        public TaskCompletionSource<bool> TaskCompletionSource;

        public TcpMessage(int sequneceNumber, byte[] body)
        {
            if (body == null)
                throw new ArgumentNullException(nameof(body));

            this.sequneceNumber = sequneceNumber;
            this.body = body;
        }

        public TcpMessage(int sequneceNumber, byte[] body, TaskCompletionSource<bool> tcs, CancellationToken token)
            : this(sequneceNumber, body)
        {
            TaskCompletionSource = tcs;
            CancellationToken = token;
        }

        public byte[] Encode()
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var binaryWriter = new BinaryWriter(memoryStream))
                {
                    // https://core.telegram.org/mtproto#tcp-transport
                    /*
                        4 length bytes are added at the front 
                        (to include the length, the sequence number, and CRC32; always divisible by 4)
                        and 4 bytes with the packet sequence number within this TCP connection 
                        (the first packet sent is numbered 0, the next one 1, etc.),
                        and 4 CRC32 bytes at the end (length, sequence number, and payload together).
                    */
                    binaryWriter.Write(body.Length + 12);
                    binaryWriter.Write(sequneceNumber);
                    binaryWriter.Write(body);
                    var crc32 = new CRC32();
                    crc32.SlurpBlock(memoryStream.GetBuffer(), 0, 8 + body.Length);
                    binaryWriter.Write(crc32.Crc32Result);

                    var transportPacket = memoryStream.ToArray();

                    //					Debug.WriteLine("Tcp packet #{0}\n{1}", SequneceNumber, BitConverter.ToString(transportPacket));

                    return transportPacket;
                }
            }
        }

        public static TcpMessage Decode(byte[] body)
        {
            if (body == null)
                throw new ArgumentNullException(nameof(body));

            if (body.Length < 12)
                throw new InvalidOperationException("Ops, wrong size of input packet");

            using (var memoryStream = new MemoryStream(body))
            {
                using (var binaryReader = new BinaryReader(memoryStream))
                {
                    var packetLength = binaryReader.ReadInt32();

                    if (packetLength < 12)
                        throw new InvalidOperationException($"invalid packet length: {packetLength}");

                    var seq = binaryReader.ReadInt32();
                    byte[] packet = binaryReader.ReadBytes(packetLength - 12);
                    var checksum = binaryReader.ReadInt32();

                    var crc32 = new CRC32();
                    crc32.SlurpBlock(body, 0, packetLength - 4);
                    var validChecksum = crc32.Crc32Result;

                    if (checksum != validChecksum)
                    {
                        throw new InvalidOperationException("invalid checksum! skip");
                    }

                    return new TcpMessage(seq, packet);
                }
            }
        }
    }
}

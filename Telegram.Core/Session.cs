using System;
using System.IO;
using Telegram.Net.Core.MTProto;
using Telegram.Net.Core.MTProto.Crypto;
using Telegram.Net.Core.Requests;

#pragma warning disable 675

namespace Telegram.Net.Core
{
    public interface ISessionStore
    {
        void Save(Session session);
        Session Load();
    }

    public class Session : ISession
    {
        public string ServerAddress { get; set; }
        public int Port { get; set; }
        public User User { get; set; }
        public int SessionExpires { get; set; }
        public AuthKey AuthKey { get; set; }
        public ulong Salt { get; set; }
        public int TimeOffset { get; set; }
        public ulong Id { get; set; }
        public int sequence;
        public long lastMessageId;
        private readonly Random _random;

        private readonly ISessionStore _store;

        private readonly object generateSyncRoot = new object();

        private Session(ISessionStore store)
        {
            _random = new Random();
            _store = store;
        }

        public byte[] ToBytes()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Id);
                writer.Write(sequence);
                writer.Write(Salt);
                writer.Write(lastMessageId);
                writer.Write(TimeOffset);
                Serializers.String.Write(writer, ServerAddress);
                writer.Write(Port);

                if (User != null)
                {
                    writer.Write(1);
                    writer.Write(SessionExpires);
                    User.Write(writer);
                }
                else
                {
                    writer.Write(0);
                }

                if (AuthKey?.Data != null)
                {
                    Serializers.Bytes.Write(writer, AuthKey.Data);
                }

                return stream.ToArray();
            }
        }

        public static Session FromBytes(byte[] buffer, ISessionStore store)
        {
            using (var stream = new MemoryStream(buffer))
            using (var reader = new BinaryReader(stream))
            {
                var id = reader.ReadUInt64();
                var sequence = reader.ReadInt32();
                var salt = reader.ReadUInt64();
                var lastMessageId = reader.ReadInt64();
                var timeOffset = reader.ReadInt32();
                var serverAddress = Serializers.String.Read(reader);
                var port = reader.ReadInt32();

                var isAuthExsist = reader.ReadInt32() == 1;
                int sessionExpires = 0;
                User user = null;
                if (isAuthExsist)
                {
                    sessionExpires = reader.ReadInt32();
                    user = TLObject.Read<User>(reader);
                }

                var authData = Serializers.Bytes.Read(reader);

                return new Session(store)
                {
                    AuthKey = new AuthKey(authData),
                    Id = id,
                    Salt = salt,
                    sequence = sequence,
                    lastMessageId = lastMessageId,
                    TimeOffset = timeOffset,
                    SessionExpires = sessionExpires,
                    User = user,
                    ServerAddress = serverAddress,
                    Port = port
                };
            }
        }

        public void Save()
        {
            _store?.Save(this);
        }

        public static Session TryLoadOrCreateNew(string serverAddress, int port, ISessionStore store = null, string sessionUserId = "session")
        {
            return store?.Load() ?? new Session(store)
            {
                Id = GenerateRandomUlong(),
                ServerAddress = serverAddress,
                Port = port
            };
        }

        private static ulong GenerateRandomUlong()
        {
            var random = new Random();
            ulong rand = (((ulong)random.Next()) << 32) | ((ulong)random.Next());
            return rand;
        }

        public long GetNewMessageId()
        {
            lock (generateSyncRoot)
            {
                long time = Convert.ToInt64(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds);

                long newMessageId = ((time / 1000 + TimeOffset) << 32) |
                                    ((time % 1000) << 22) |
                                    (_random.Next(524288) << 2); // 2^19
                                                                 // [ unix timestamp : 32 bit] [ milliseconds : 10 bit ] [ buffer space : 1 bit ] [ random : 19 bit ] [ msg_id type : 2 bit ] = [ msg_id : 64 bit ]

                if (lastMessageId >= newMessageId)
                {
                    newMessageId = lastMessageId + 4;
                }

                lastMessageId = newMessageId;
                return newMessageId;
            }
        }

        public int GetNextSequenceNumber(MtProtoRequest request)
        {
            lock (generateSyncRoot)
            {
                return request.isContentMessage ? sequence++ * 2 + 1 : sequence * 2;
            }
        }

        public void ResetAuth()
        {
            lock (generateSyncRoot)
            {
                AuthKey = null;
                User = null;
                SessionExpires = 0;
            }
        }

        public void Reset()
        {
            lock (generateSyncRoot)
            {
                Id = GenerateRandomUlong();
                lastMessageId = 0;
                sequence = 0;
                TimeOffset = 0;
            }
        }
    }
}

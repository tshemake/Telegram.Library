using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Net.Core.MTProto;

namespace Telegram.Models
{
    public class Message : Serializable
    {
        public int Id { get; set; }
        public int FromId { get; set; }
        public int ToId { get; set; }
        public DateTime Date { get; set; }
        public string Body { get; set; }

        public static explicit operator Message(Net.Core.MTProto.Message message)
        {
            if (message is MessageConstructor)
            {
                MessageConstructor messageConstructor = message.As<MessageConstructor>();
                PeerUserConstructor peerUser = messageConstructor.toId.As<PeerUserConstructor>();
                return new Message
                {
                    Id = messageConstructor.id,
                    FromId = messageConstructor.fromId,
                    ToId = peerUser.userId,
                    Date = Client.UnixEpoch.AddSeconds(messageConstructor.date),
                    Body = messageConstructor.message
                };
            }

            return null;
        }

        public static explicit operator Message(Updates message)
        {
            if (message is UpdateShortMessageConstructor)
            {
                UpdateShortMessageConstructor messageConstructor = message.As<UpdateShortMessageConstructor>();
                return new Message
                {
                    Id = messageConstructor.id,
                    FromId = messageConstructor.fromId,
                    Date = Client.UnixEpoch.AddSeconds(messageConstructor.date),
                    Body = messageConstructor.message
                };
            }
            return null;
        }
    }
}

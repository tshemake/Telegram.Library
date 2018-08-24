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
        public bool? IsUnread { get; set; }
        public bool? IsSentByCurrentUser { get; set; }
        public Type Contructor { get; set; }

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
                    Body = messageConstructor.message,
                    IsUnread = messageConstructor.unread,
                    IsSentByCurrentUser = messageConstructor.sentByCurrentUser,
                    Contructor = typeof (MessageConstructor)
                };
            }
            else if (message is MessageForwardedConstructor)
            {
                MessageForwardedConstructor messageForwardedConstructor = message.As<MessageForwardedConstructor>();
                PeerUserConstructor peerUser = messageForwardedConstructor.toId.As<PeerUserConstructor>();
                return new Message
                {
                    Id = messageForwardedConstructor.id,
                    FromId = messageForwardedConstructor.fromId,
                    ToId = peerUser.userId,
                    Date = Client.UnixEpoch.AddSeconds(messageForwardedConstructor.date),
                    Body = messageForwardedConstructor.message,
                    IsUnread = messageForwardedConstructor.unread,
                    IsSentByCurrentUser = messageForwardedConstructor.sentByCurrentUser,
                    Contructor = typeof(MessageForwardedConstructor)
                };
            }
            else if (message is MessageEmptyConstructor)
            {
                MessageEmptyConstructor messageConstructor = message.As<MessageEmptyConstructor>();
                return new Message
                {
                    Id = messageConstructor.id,
                    Contructor = typeof(MessageEmptyConstructor)
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

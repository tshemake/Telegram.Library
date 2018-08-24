using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Net.Core.MTProto;

namespace Telegram.Models
{
    public class SentMessage : Serializable
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public int Pts { get; set; }
        public int Seq { get; set; }

        public static explicit operator SentMessage(Net.Core.MTProto.SentMessage message)
        {
            if (message is SentMessageConstructor)
            {
                SentMessageConstructor messageConstructor = message.As<SentMessageConstructor>();
                return new SentMessage
                {
                    Id = messageConstructor.id,
                    Date = Client.UnixEpoch.AddSeconds(messageConstructor.date),
                    Pts = messageConstructor.pts,
                    Seq = messageConstructor.seq
                };
            }
            else if (message is SentMessageLinkConstructor)
            {
                SentMessageLinkConstructor messageConstructor = message.As<SentMessageLinkConstructor>();
                return new SentMessage
                {
                    Id = messageConstructor.id,
                    Date = Client.UnixEpoch.AddSeconds(messageConstructor.date),
                    Pts = messageConstructor.pts,
                    Seq = messageConstructor.seq
                };
            }

            return null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Net.Core.MTProto;

namespace Telegram.Models
{
    public class Chat : Serializable
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public DateTime Date { get; set; }
        public Type Contructor { get; set; }

        public static explicit operator Chat(Net.Core.MTProto.Chat chat)
        {
            if (chat is ChatEmptyConstructor)
            {
                ChatEmptyConstructor chatEmptyConstructor = chat.As<ChatEmptyConstructor>();
                return new Chat
                {
                    Id = chatEmptyConstructor.id,
                    Contructor = typeof(ChatEmptyConstructor)
                };
            }
            else if (chat is ChatConstructor)
            {
                ChatConstructor chatConstructor = chat.As<ChatConstructor>();
                return new Chat
                {
                    Id = chatConstructor.id,
                    Title = chatConstructor.title,
                    Date = Client.UnixEpoch.AddSeconds(chatConstructor.date),
                    Contructor = typeof(ChatConstructor)
                };
            }
            else if (chat is ChatForbiddenConstructor)
            {
                ChatForbiddenConstructor chatForbiddeConstructor = chat.As<ChatForbiddenConstructor>();
                return new Chat
                {
                    Id = chatForbiddeConstructor.id,
                    Title = chatForbiddeConstructor.title,
                    Date = Client.UnixEpoch.AddSeconds(chatForbiddeConstructor.date),
                    Contructor = typeof(ChatForbiddenConstructor)
                };
            }
            else if (chat is GeoChatConstructor)
            {
                GeoChatConstructor geoChatConstructor = chat.As<GeoChatConstructor>();
                return new Chat
                {
                    Id = geoChatConstructor.id,
                    Title = geoChatConstructor.title,
                    Date = Client.UnixEpoch.AddSeconds(geoChatConstructor.date),
                    Contructor = typeof(GeoChatConstructor)
                };
            }

            return null;
        }
    }
}

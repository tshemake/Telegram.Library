using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Models
{
    public class MessagesDialog : Serializable
    {
        public List<Dialog> Dialogs { get; set; }
        public List<Chat> Chats { get; set; }
        public List<Message> Messages { get; set; }
        public List<Contact> Contacts { get; set; }
    }
}

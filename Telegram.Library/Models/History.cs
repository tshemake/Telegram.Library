using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Models
{
    public class History : Serializable
    {
        public List<Message> Messages { get; set; }
        private readonly List<Chat> _chats = new List<Chat>();
        public ReadOnlyCollection<Chat> Chats
        {
            get
            {
                return _chats.AsReadOnly();
            }
        }
        private readonly List<Contact> _contacts = new List<Contact>();
        public ReadOnlyCollection<Contact> Contacts
        {
            get
            {
                return _contacts.AsReadOnly();
            }
        }

        public void AddChats(IEnumerable<Chat> chats)
        {
            var existsChatIds = Chats.Select(c => c.Id).ToList();
            _chats.AddRange(chats.Where(c => !existsChatIds.Contains(c.Id)));
        }

        public void AddContacts(IEnumerable<Contact> contacts)
        {
            var existsContactIds = Contacts.Select(c => c.Id).ToList();
            _contacts.AddRange(contacts.Where(c => !existsContactIds.Contains(c.Id)));
        }
    }
}

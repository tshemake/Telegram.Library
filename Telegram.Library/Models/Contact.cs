using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Net.Core.MTProto;

namespace Telegram.Models
{
    public class Contact : Serializable
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Username { get; set; }
        public string Phone { get; set; } = string.Empty;
        public long AccessHash { get; set; }
        public bool IsForeign { get; set; }

        public static explicit operator Contact(User user)
        {
            if (user is UserSelfConstructor)
            {
                UserSelfConstructor userForeignContact = user.As<UserSelfConstructor>();
                return new Contact
                {
                    Id = userForeignContact.id,
                    FirstName = userForeignContact.firstName,
                    LastName = userForeignContact.lastName,
                    Username = userForeignContact.username,
                    IsForeign = false
                };
            }
            else if (user is UserContactConstructor)
            {
                UserContactConstructor userContact = user.As<UserContactConstructor>();
                return new Contact
                {
                    Id = userContact.id,
                    FirstName = userContact.firstName,
                    LastName = userContact.lastName,
                    Username = userContact.username,
                    Phone = userContact.phone,
                    AccessHash = userContact.accessHash,
                    IsForeign = false
                };
            }
            else if (user is UserForeignConstructor)
            {
                UserForeignConstructor userForeignContact = user.As<UserForeignConstructor>();
                return new Contact
                {
                    Id = userForeignContact.id,
                    FirstName = userForeignContact.firstName,
                    LastName = userForeignContact.lastName,
                    Username = userForeignContact.username,
                    AccessHash = userForeignContact.accessHash,
                    IsForeign = true
                };
            }
            return null;
        }
    }
}

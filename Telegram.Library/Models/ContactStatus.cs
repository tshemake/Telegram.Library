using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Net.Core.MTProto;

namespace Telegram.Models
{
    public class ContactStatus : Serializable
    {
        public int ContactId { get; set; }
        public DateTime? Expires { get; set; }
        public DateTime? WasOnline { get; set; }
        public Status Status { get; set; }

        public static explicit operator ContactStatus(Net.Core.MTProto.ContactStatus contactStatus)
        {
            Status status = Status.EMPTY;
            DateTime? expires = null;
            DateTime? wasInline = null;
            if (contactStatus is ContactStatusConstructor contactStatusConstructor)
            {
                switch (contactStatusConstructor.userStatus.GetType().Name)
                {
                    case "UserStatusOnlineConstructor":
                        status = Status.ONLINE;
                        UserStatus userStatusOnline = contactStatusConstructor.userStatus;
                        UserStatusOnlineConstructor userStatusOnlineConstructor = (UserStatusOnlineConstructor)userStatusOnline;
                        expires = Client.UnixEpoch.AddSeconds(userStatusOnlineConstructor.expires);
                        break;
                    case "UserStatusOfflineConstructor":
                        status = Status.OFFLINE;
                        UserStatus userStatusOffline = contactStatusConstructor.userStatus;
                        UserStatusOfflineConstructor userStatusOfflineConstructor = (UserStatusOfflineConstructor)userStatusOffline;
                        wasInline = Client.UnixEpoch.AddSeconds(userStatusOfflineConstructor.wasOnline);
                        break;
                    case "UserStatusRecentlyConstructor":
                        status = Status.RECENTLY;
                        break;
                    case "UserStatusLastWeekConstructor":
                        status = Status.LAST_WEEK;
                        break;
                    case "UserStatusLastMonthConstructor":
                        status = Status.LAST_MONTH;
                        break;
                    default:
                        status = Status.EMPTY;
                        break;
                }
                return new ContactStatus
                {
                    ContactId = contactStatusConstructor.userId,
                    Status = status,
                    Expires = expires,
                    WasOnline = wasInline
                };
            }
            return null;
        }
    }

    public enum Status : byte
    {
        EMPTY, // Status has not yet been updated
        ONLINE, // User online
        OFFLINE, // User offline
        RECENTLY, LAST_WEEK, LAST_MONTH
    }
}

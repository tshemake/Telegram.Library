using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Net.Core.MTProto;

namespace Telegram.Models
{
    public class Dialog : Serializable
    {
        public int TopMessage { get; set; }
        public int UnreadCount { get; set; }
        public int ClientId { get; set; }
        public Type Contructor { get; set; }

        public static explicit operator Dialog(Net.Core.MTProto.Dialog dialog)
        {
            if (dialog is DialogConstructor)
            {
                DialogConstructor dialogConstructor = dialog.As<DialogConstructor>();
                PeerUserConstructor peerUser = dialogConstructor.peer.As<PeerUserConstructor>();
                return new Dialog
                {
                    TopMessage = dialogConstructor.topMessage,
                    UnreadCount = dialogConstructor.unreadCount,
                    ClientId = peerUser.userId,
                    Contructor = typeof(DialogConstructor)
                };
            }

            return null;
        }
    }
}

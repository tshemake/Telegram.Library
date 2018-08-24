using System.IO;
using Telegram.Net.Core.MTProto;

namespace Telegram.Net.Core.Requests
{
    public class DeleteContactRequest : MtProtoRequest
    {
        public readonly InputUser user;

        public ContactsLink contactsLink { get; private set; }

        public DeleteContactRequest(InputUser user)
        {
            this.user = user;
        }

        protected override uint requestCode => 0x8e953744;

        public override void OnSend(BinaryWriter writer)
        {
            writer.Write(requestCode);

            user.Write(writer);
        }

        public override void OnResponse(BinaryReader reader)
        {
            contactsLink = TLObject.Read<ContactsLink>(reader);
        }

        public override bool isContentMessage => true;
    }
}

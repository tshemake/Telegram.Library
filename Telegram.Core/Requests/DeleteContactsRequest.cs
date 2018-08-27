using System.Collections.Generic;
using System.IO;
using Telegram.Net.Core.MTProto;

namespace Telegram.Net.Core.Requests
{
    public class DeleteContactsRequest : MtProtoRequest
    {
        public readonly List<InputUser> users;

        public bool Done { get; private set; }

        public DeleteContactsRequest(List<InputUser> users)
        {
            this.users = users;
        }

        protected override uint requestCode => 0x59ab389e;

        public override void OnSend(BinaryWriter writer)
        {
            writer.Write(requestCode);

            TLObject.WriteVector(writer, users);
        }

        public override void OnResponse(BinaryReader reader)
        {
            Done = Serializers.Bool.Read(reader);
        }

        public override bool isContentMessage => true;
    }
}

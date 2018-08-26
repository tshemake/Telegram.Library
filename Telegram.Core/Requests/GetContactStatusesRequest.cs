using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Telegram.Net.Core.MTProto;

namespace Telegram.Net.Core.Requests
{
    public class GetContactStatusesRequest : MtProtoRequest
    {
        public List<ContactStatus> ContactStatuses { get; private set; }

        public GetContactStatusesRequest()
        {
        }

        protected override uint requestCode => 0xc4a353ee;

        public override void OnSend(BinaryWriter writer)
        {
            writer.Write(requestCode);
        }

        public override void OnResponse(BinaryReader reader)
        {
            ContactStatuses = TLObject.ReadVector<ContactStatus>(reader);
        }

        public override bool isContentMessage => true;
    }
}

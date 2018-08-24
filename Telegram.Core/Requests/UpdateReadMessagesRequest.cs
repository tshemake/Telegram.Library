using System.Collections.Generic;
using System.IO;
using Telegram.Net.Core.MTProto;

namespace Telegram.Net.Core.Requests
{
    public class UpdateReadMessagesRequest : MtProtoRequest
    {
        public List<int> messages;
        public int pts;

        public Update eventsOccured;

        protected override uint requestCode => 0xc6649e31;

        public UpdateReadMessagesRequest(List<int> messages, int pts)
        {
            this.messages = messages;
            this.pts = pts;
        }

        public override void OnSend(BinaryWriter writer)
        {
            writer.Write(requestCode);

            TLObject.WriteVector(writer, messages, writer.Write);
            writer.Write(pts);
        }

        public override void OnResponse(BinaryReader reader)
        {
            eventsOccured = TLObject.Read<Update>(reader);
        }

        public override bool isContentMessage => true;
    }
}

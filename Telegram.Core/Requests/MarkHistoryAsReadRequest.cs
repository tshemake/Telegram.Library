using System.IO;
using Telegram.Net.Core.MTProto;

namespace Telegram.Net.Core.Requests
{
    public class MarkHistoryAsReadRequest : MtProtoRequest
    {
        public readonly InputPeer peer;
        public readonly int offset;
        public readonly int maxId;
        public readonly bool readContents;

        public MessagesAffectedHistory affectedHistory;

        public MarkHistoryAsReadRequest(InputPeer peer, int offset, int maxId, bool readContents)
        {
            this.peer = peer;
            this.offset = offset;
            this.maxId = maxId;
            this.readContents = readContents;
        }

        protected override uint requestCode => 0xeed884c6;

        public override void OnSend(BinaryWriter writer)
        {
            writer.Write(requestCode);

            peer.Write(writer);
            writer.Write(offset);
            writer.Write(maxId);
            Serializers.Bool.Write(writer, readContents);
        }

        public override void OnResponse(BinaryReader reader)
        {
            affectedHistory = TLObject.Read<MessagesAffectedHistory>(reader);
        }

        public override bool isContentMessage => true;
    }
}

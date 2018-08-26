using System.IO;
using Telegram.Net.Core.MTProto;

namespace Telegram.Net.Core.Requests
{
    public class AuthLogOutRequest : MtProtoRequest
    {
        public bool IsLogOut { get; private set; }

        public AuthLogOutRequest()
        {
        }

        protected override uint requestCode => 0x5717da40;

        public override void OnSend(BinaryWriter writer)
        {
            writer.Write(requestCode);
        }

        public override void OnResponse(BinaryReader reader)
        {
            IsLogOut = Serializers.Bool.Read(reader);
        }
        
        public override bool isContentMessage => true;
    }
}

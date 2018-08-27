using System.IO;
using Telegram.Net.Core.MTProto;

namespace Telegram.Net.Core.Requests
{
    public class AuthResetAuthorizationRequest : MtProtoRequest
    {
        public bool IsDone { get; private set; }

        public AuthResetAuthorizationRequest()
        {
        }

        protected override uint requestCode => 0x9fab0d1a;

        public override void OnSend(BinaryWriter writer)
        {
            writer.Write(requestCode);
        }

        public override void OnResponse(BinaryReader reader)
        {
            IsDone = Serializers.Bool.Read(reader);
        }
        
        public override bool isContentMessage => true;
    }
}

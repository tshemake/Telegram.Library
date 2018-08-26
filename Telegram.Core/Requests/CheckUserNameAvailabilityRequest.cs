using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Net.Core.MTProto;

namespace Telegram.Net.Core.Requests
{
    class CheckUserNameAvailabilityRequest : MtProtoRequest
    {
        private readonly string username;

        public bool isAvailable { get; private set; }

        protected override uint requestCode => 0x2714d86c;

        public CheckUserNameAvailabilityRequest(string username)
        {
            this.username = username;
        }

        public override void OnSend(BinaryWriter writer)
        {
            writer.Write(requestCode);
            Serializers.String.Write(writer, username);
        }

        public override void OnResponse(BinaryReader reader)
        {
            isAvailable = TLObject.Read<bool>(reader);
        }

        public override bool isContentMessage => true;
    }
}

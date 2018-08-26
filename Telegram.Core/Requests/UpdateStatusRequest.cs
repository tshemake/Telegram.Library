using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Net.Core.MTProto;

namespace Telegram.Net.Core.Requests
{
    class UpdateStatusRequest : MtProtoRequest
    {
        private readonly bool _offline;

        public bool IsUserStatusOffline { get; private set; }

        protected override uint requestCode => 0x6628562c;

        public UpdateStatusRequest(bool offline)
        {
            _offline = offline;
        }

        public override void OnSend(BinaryWriter writer)
        {
            writer.Write(requestCode);
            Serializers.Bool.Write(writer, _offline);
        }

        public override void OnResponse(BinaryReader reader)
        {
            IsUserStatusOffline = Serializers.Bool.Read(reader);
        }

        public override bool isContentMessage => true;
    }
}

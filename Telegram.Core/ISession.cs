using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Net.Core.MTProto;
using Telegram.Net.Core.MTProto.Crypto;
using Telegram.Net.Core.Requests;

namespace Telegram.Net.Core
{
    public interface ISession
    {
        ulong Id { get; set; }
        string ServerAddress { get; set; }
        int Port { get; set; }
        User User { get; set; }
        int SessionExpires { get; set; }
        AuthKey AuthKey { get; set; }
        ulong Salt { get; set; }
        int TimeOffset { get; set; }
        void ResetAuth();
        void Reset();
        void Save();
        long GetNewMessageId();
        int GetNextSequenceNumber(MtProtoRequest request);
    }
}

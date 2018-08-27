using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Telegram.Net.Core.Network
{
    public interface ITcpTransport : IDisposable
    {
        void Disconnect();

        Task<TcpMessage> Receieve();

        Task Send(byte[] packet, CancellationToken cancellationToken);
    }
}

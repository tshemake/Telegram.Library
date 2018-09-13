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

        Task<Task<bool>> Send(byte[] packet, CancellationToken cancellationToken);
    }
}

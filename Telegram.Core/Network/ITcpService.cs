using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Telegram.Net.Core.Network
{
    public interface ITcpService : IDisposable
    {
        Task Connect();

        void Disconnect();

        Task<int> Read(byte[] buffer, int offset, int count, CancellationToken cancellationToken);

        Task Send(byte[] encodedMessage, CancellationToken cancellationToken);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Net.Core.Network.Exceptions
{
    public class NetworkMigrationException : DataCenterMigrationException
    {
        internal NetworkMigrationException(int dc)
            : base($"Network located on a different DC: {dc}.", dc)
        {
        }
    }
}

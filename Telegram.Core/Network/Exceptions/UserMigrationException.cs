using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Net.Core.Network.Exceptions
{
    public class UserMigrationException : DataCenterMigrationException
    {
        internal UserMigrationException(int dc)
            : base($"User located on a different DC: {dc}.", dc)
        {
        }
    }
}

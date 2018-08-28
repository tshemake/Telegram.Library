using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Net.Core.Network.Exceptions
{
    public sealed class PhoneMigrationException : DataCenterMigrationException
    {
        internal PhoneMigrationException(int dc)
            : base($"Phone number registered to a different DC: {dc}.", dc)
        {
        }
    }
}

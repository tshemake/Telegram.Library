using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Net.Core.Network.Exceptions
{
    public class FileMigrationException : DataCenterMigrationException
    {
        internal FileMigrationException(int dc)
            : base($"File located on a different DC: {dc}.", dc)
        {
        }
    }
}

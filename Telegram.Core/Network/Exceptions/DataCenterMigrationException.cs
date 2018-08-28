using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Net.Core.Network.Exceptions
{
    public abstract class DataCenterMigrationException : Exception
    {
        internal int Dc { get; }

        protected DataCenterMigrationException(string msg, int dc) : base(msg)
        {
            Dc = dc;
        }
    }
}

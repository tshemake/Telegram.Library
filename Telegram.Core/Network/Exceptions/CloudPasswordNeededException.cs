using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Net.Core.Network.Exceptions
{
    public sealed class CloudPasswordNeededException : Exception
    {
        internal CloudPasswordNeededException(string msg) : base(msg)
        {
        }
    }
}

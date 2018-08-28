using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Net.Core.Network.Exceptions
{
    public class FloodWaitException : Exception
    {
        public TimeSpan TimeToWait { get; }

        internal FloodWaitException(TimeSpan timeToWait)
            : base($"Flood prevention. Telegram now requires your program to do requests again only after {timeToWait.TotalSeconds} seconds have passed ({nameof(TimeToWait)} property).")
        {
            TimeToWait = timeToWait;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Net.Core.Network.Exceptions
{
    public sealed class PhoneCodeInvalidException : Exception
    {
        internal PhoneCodeInvalidException(string msg) : base(msg)
        {
        }
    }
}

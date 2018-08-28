using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Net.Core.Network.Exceptions
{
    public sealed class PhoneNumberInvalidException : Exception
    {
        internal PhoneNumberInvalidException() : base("Phone number is invalid or not registered on the server")
        {
        }
    }
}

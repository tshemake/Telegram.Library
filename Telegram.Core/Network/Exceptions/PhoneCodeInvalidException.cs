using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Net.Core.Network.Exceptions
{
    public class PhoneCodeInvalidException : Exception
    {
        internal PhoneCodeInvalidException() : base("The numeric code used to authenticate does not match the numeric code sent by SMS/Telegram")
        {
        }
    }
}

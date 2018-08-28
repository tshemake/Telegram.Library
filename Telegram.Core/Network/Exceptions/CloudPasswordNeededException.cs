﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Net.Core.Network.Exceptions
{
    public class CloudPasswordNeededException : Exception
    {
        internal CloudPasswordNeededException() : base("This Account has Cloud Password!")
        {
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram
{
    interface ILogManagerFacade
    {
        ILogger GetLogger(Type type);
        ILogger GetLogger(string name);
    }
}

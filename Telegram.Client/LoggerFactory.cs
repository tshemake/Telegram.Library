using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram
{
    static class LoggerFactory
    {
        private static ILogger s_logger;

        public static ILogger Default
        {
            get
            {
                return s_logger ?? (s_logger = LogManagerFacade.GetLogger());
            }
        }

        public static ILogger GetLogger<T>()
        {
            return LogManagerFacade.GetLogger<T>();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net.Config;

namespace Telegram
{
    class LogManagerFacade : ILogManagerFacade
    {
        private static readonly ILogManagerFacade s_logManager;

        static LogManagerFacade()
        {
            XmlConfigurator.Configure();
            s_logManager = new LogManagerFacade();
        }

        public static ILogger GetLogger<T>()
        {
            return s_logManager.GetLogger(typeof(T));
        }

        public static ILogger GetLogger()
        {
            log4net.ILog logger = log4net.LogManager.GetLogger("LOGGER");
            return new Logger(logger);
        }

        public ILogger GetLogger(Type type)
        {
            log4net.ILog logger = log4net.LogManager.GetLogger(type);
            return new Logger(logger);
        }

        public ILogger GetLogger(string name)
        {
            log4net.ILog logger = log4net.LogManager.GetLogger(name);
            return new Logger(logger);
        }
    }
}

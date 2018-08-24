using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;

namespace Telegram
{
    class Logger : ILogger
    {
        private readonly ILog _log;

        public Logger(ILog log)
        {
            _log = log;
        }

        public void Debug(object message)
        {
            if (!IsDebug)
            {
                return;
            }
            _log.Debug(message);
        }

        public void Debug(object message, Exception exception)
        {
            if (!IsDebug)
            {
                return;
            }
            _log.Debug(message, exception);
        }

        public void DebugFormat(string format, params object[] args)
        {
            if (!IsDebug)
            {
                return;
            }
            _log.DebugFormat(format, args);
        }

        public void Info(object message)
        {
            if (!IsInfo)
            {
                return;
            }
            _log.Info(message);
        }

        public void Info(object message, Exception exception)
        {
            if (!IsInfo)
            {
                return;
            }
            _log.Info(message, exception);
        }

        public void InfoFormat(string format, params object[] args)
        {
            if (!IsInfo)
            {
                return;
            }
            _log.InfoFormat(format, args);
        }

        public void Warn(object message)
        {
            if (!IsWarn)
            {
                return;
            }
            _log.Warn(message);
        }

        public void Warn(object message, Exception exception)
        {
            if (!IsWarn)
            {
                return;
            }
            _log.Warn(message, exception);
        }

        public void WarnFormat(string format, params object[] args)
        {
            if (!IsWarn)
            {
                return;
            }
            _log.WarnFormat(format, args);
        }

        public void Error(object message)
        {
            if (!IsError)
            {
                return;
            }
            _log.Error(message);
        }

        public void Error(object message, Exception exception)
        {
            if (!IsError)
            {
                return;
            }
            _log.Error(message, exception);
        }

        public void ErrorFormat(string format, params object[] args)
        {
            if (!IsError)
            {
                return;
            }
            _log.ErrorFormat(format, args);
        }

        public void Fatal(object message)
        {
            if (!_log.IsFatalEnabled)
            {
                return;
            }
            _log.Fatal(message);
        }

        public void Fatal(object message, Exception exception)
        {
            if (!_log.IsFatalEnabled)
            {
                return;
            }
            _log.Fatal(message, exception);
        }

        public void FatalFormat(string format, params object[] args)
        {
            if (!_log.IsFatalEnabled)
            {
                return;
            }
            _log.FatalFormat(format, args);
        }

        public bool IsDebug
        {
            get { return _log.IsDebugEnabled; }
        }

        public bool IsInfo
        {
            get { return _log.IsInfoEnabled; }
        }

        public bool IsWarn
        {
            get { return _log.IsWarnEnabled; }
        }

        public bool IsError
        {
            get { return _log.IsErrorEnabled; }
        }

        public bool IsFatal
        {
            get { return _log.IsFatalEnabled; }
        }
    }
}

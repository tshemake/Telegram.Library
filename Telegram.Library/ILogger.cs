using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram
{
    public interface ILogger
    {
        void Debug(object message);

        void Debug(object message, Exception exception);

        void DebugFormat(string format, params object[] args);

        void Info(object message);

        void Info(object message, Exception exception);

        void InfoFormat(string format, params object[] args);

        void Warn(object message);

        void Warn(object message, Exception exception);

        void WarnFormat(string format, params object[] args);

        void Error(object message);

        void Error(object message, Exception exception);

        void ErrorFormat(string format, params object[] args);

        void Fatal(object message);

        void Fatal(object message, Exception exception);

        void FatalFormat(string format, params object[] args);

        bool IsDebug { get; }

        bool IsInfo { get; }

        bool IsWarn { get; }

        bool IsError { get; }

        bool IsFatal { get; }
    }
}

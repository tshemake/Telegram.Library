using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Telegram.Core.Utils
{
    public static class AsyncHelper
    {
        public static ThreadPoolRedirector RedirectToThreadPool() => new ThreadPoolRedirector();

        public static async Task ProcessCollectionAsync<T>(IEnumerable<T> coll, Func<T, Task> method)
        {
            List<Task> tasks = coll.Select(method).ToList();
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }

    public struct ThreadPoolRedirector : ICriticalNotifyCompletion
    {
        // awaiter и awaitable в одном флаконе
        public ThreadPoolRedirector GetAwaiter() => this;

        // true означает выполнять продолжение немедленно 
        public bool IsCompleted => Thread.CurrentThread.IsThreadPoolThread;

        public void OnCompleted(Action continuation)
        {
            ThreadPool.QueueUserWorkItem(state => continuation());
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            ThreadPool.UnsafeQueueUserWorkItem(state => continuation(), null);
        }

        public void GetResult() { }
    }
}

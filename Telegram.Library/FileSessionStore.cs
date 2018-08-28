using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Net.Core;

namespace Telegram
{
    class FileSessionStore : ISessionStore, IDisposable
    {
        public string SessionUserId { get; set; }
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public void Save(Session session)
        {
            var sessionFileName = $"{SessionUserId}.dat";
            Debug.WriteLine($"Save session into {sessionFileName}");

            _semaphore.Wait();
            using (var stream = new FileStream(sessionFileName, FileMode.OpenOrCreate))
            {
                var result = session.ToBytes();
                stream.Write(result, 0, result.Length);
            }
            _semaphore.Release();
        }

        public Session Load()
        {
            var sessionFileName = $"{SessionUserId}.dat";
            Debug.WriteLine($"Load session for sessionTag = {sessionFileName}");

            if (!File.Exists(sessionFileName))
                return null;

            using (var stream = new FileStream(sessionFileName, FileMode.Open))
            {
                var buffer = new byte[2048];
                stream.Read(buffer, 0, 2048);

                return Session.FromBytes(buffer, this);
            }
        }

        public void Dispose()
        {
            _semaphore?.Dispose();
        }
    }
}

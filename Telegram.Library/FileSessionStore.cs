using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Net.Core;

namespace Telegram
{
    class FileSessionStore : ISessionStore
    {
        public string SessionUserId { get; set; }
        public void Save(Session session)
        {
            using (var stream = new FileStream($"{SessionUserId}.dat", FileMode.OpenOrCreate))
            {
                var result = session.ToBytes();
                stream.Write(result, 0, result.Length);
            }
        }

        public Session Load()
        {
            var sessionFileName = $"{SessionUserId}.dat";
            if (!File.Exists(sessionFileName))
                return null;

            using (var stream = new FileStream(sessionFileName, FileMode.Open))
            {
                var buffer = new byte[2048];
                stream.Read(buffer, 0, 2048);

                return Session.FromBytes(buffer, this);
            }
        }
    }
}

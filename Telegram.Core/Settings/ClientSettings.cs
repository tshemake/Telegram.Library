using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Net.Core.Settings
{
    public class ClientSettings : IClientSettings
    {
        private static readonly Lazy<ClientSettings> s_lazy =
            new Lazy<ClientSettings>(() => new ClientSettings());
        public static ClientSettings Instance { get { return s_lazy.Value; } }

        public int AppId { get; set; }

        public string AppHash { get; set; }

        public ISession Session { get; set; }
    }
}

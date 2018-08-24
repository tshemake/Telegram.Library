using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram
{
    static class Setting
    {
        public static ConfigurationManager ConfigurationManager { get; } = new ConfigurationManager();

        public static int ApiId
        {
            get => int.Parse(ConfigurationManager["TelegramApiId"].ToString());
        }

        public static string ApiHash
        {
            get => ConfigurationManager["TelegramApiHash"].ToString();
        }

        public static string ClientPhoneNumber
        {
            get => ConfigurationManager["TelegramClientPhoneNumber"].ToString();
        }

        public static void CreateOrUpdate(string key, object value)
        {
            ConfigurationManager[key] = value;
        }
    }
}

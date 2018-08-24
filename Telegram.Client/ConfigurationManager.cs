using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram
{
    public interface IConfigurationManager
    {
        object this[string index] { get; set; }
    }

    public class ConfigurationManager : IConfiguration, IConfigurationManager
    {
        public int ApiId { get => int.Parse(this["TelegramApiId"].ToString()); }

        public string ApiHash { get => this["TelegramApiHash"].ToString(); }

        public string PhoneNumber { get => this["TelegramClientPhoneNumber"].ToString(); }

        public string PhoneCodeHash
        {
            get => this["TelegramPhoneCodeHash"].ToString();
            set => this["TelegramPhoneCodeHash"] = value;
        }

        public string ServerHost { get => this["TelegramMTProtoServerHost"].ToString(); }

        public int ServerPort
        {
            get
            {
                int.TryParse(this["TelegramMTProtoServerPort"].ToString(), out int port);
                return port;
            }
        }

        public object this[string index]
        {
            get
            {
                return System.Configuration.ConfigurationManager.AppSettings[index];
            }
            set
            {
                CreateOrUpdate(index, value);
            }
        }

        private void CreateOrUpdate(string key, object value)
        {
            System.Configuration.Configuration config = System.Configuration.ConfigurationManager.OpenExeConfiguration(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (this[key] != null)
            {
                config.AppSettings.Settings[key].Value = value.ToString();
                config.Save(System.Configuration.ConfigurationSaveMode.Modified);
                System.Configuration.ConfigurationManager.RefreshSection(config.AppSettings.SectionInformation.Name);
            }
            else
            {
                config.AppSettings.Settings.Add(key, value.ToString());
                config.Save(System.Configuration.ConfigurationSaveMode.Minimal);
            }
        }
    }
}

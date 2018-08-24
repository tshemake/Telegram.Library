namespace Telegram.Net.Core
{
    public interface IDeviceInfoService
    {
        string DeviceModel { get; }
        string SystemVersion { get; }
        string AppVersion { get; }
        string LangCode { get; }
    }

    public class DeviceInfoService : IDeviceInfoService
    {
        public string DeviceModel { get; protected set; }
        public string SystemVersion { get; protected set; }
        public string AppVersion { get; protected set; }
        public string LangCode { get; protected set; }

        public DeviceInfoService(string model, string systemVersion, string appVersion, string langCode)
        {
            DeviceModel = model;
            SystemVersion = systemVersion;
            AppVersion = appVersion;
            LangCode = langCode;
        }
    }
}

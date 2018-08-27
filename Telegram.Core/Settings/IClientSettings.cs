namespace Telegram.Net.Core.Settings
{
    public interface IClientSettings
    {
        int AppId { get; set; }

        string AppHash { get; set; }

        ISession Session { get; set; }
    }
}

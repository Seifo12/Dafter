namespace CourtDesk.Application.Ports
{
    public interface ISettingsStore
    {
        string Get(ISession s, string key);
        void Set(ISession s, string key, string value);
    }
}

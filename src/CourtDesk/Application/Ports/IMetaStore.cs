namespace CourtDesk.Application.Ports
{
    public interface IMetaStore
    {
        string GetMeta(ISession s, string key);
        void SetMeta(ISession s, string key, string value);
    }
}

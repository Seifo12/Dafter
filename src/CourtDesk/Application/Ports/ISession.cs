namespace CourtDesk.Application.Ports
{
    public interface ISession
    {
        string UtcNowIso { get; }
        string LocalDateIso { get; }
    }
}

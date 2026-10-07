namespace CourtDesk.Application.Ports
{
    public enum PrintOutcome
    {
        Printed,
        PreviewRequested,
        NoPrinter,
        Failed,
        EmptyRoster
    }

    public interface IPrintService
    {
        PrintOutcome PrintRoster(CourtDesk.Application.RosterModel model, bool direct);

        object BuildDocument(CourtDesk.Application.RosterModel model);

        int PageCount(CourtDesk.Application.RosterModel model);

        bool HasDefaultPrinter { get; }

        string LastError { get; }
    }
}
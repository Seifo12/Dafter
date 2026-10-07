using System;

namespace CourtDesk.Domain
{
    public interface IClock
    {
        LocalDate Today { get; }
        DateTime UtcNow { get; }
    }
}

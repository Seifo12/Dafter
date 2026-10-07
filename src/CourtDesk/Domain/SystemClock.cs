using System;

namespace CourtDesk.Domain
{
    public sealed class SystemClock : IClock
    {
        public LocalDate Today
        {
            get { return LocalDate.FromDateTime(DateTime.Now); }
        }

        public DateTime UtcNow
        {
            get { return DateTime.UtcNow; }
        }
    }
}

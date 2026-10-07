using System.Collections.Generic;
using CourtDesk.Domain;

namespace CourtDesk.Application.Ports
{
    public sealed class DefendantRow
    {
        public Defendant D;
        public string CourtName;
        public string CircuitName;
        public string CustodyName;
    }

    public sealed class DecidedRow
    {
        public Defendant D;
        public long EventId;
        public string DecisionCode;
        public string NextSessionDate;
        public string CourtNameAtEvent;
        public string CircuitNameAtEvent;
    }
}

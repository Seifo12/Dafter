using System.Collections.Generic;
using CourtDesk.Domain;

namespace CourtDesk.Application.Ports
{
    public sealed class DefendantRow
    {
        public Defendant D { get; set; }
        public string CourtName { get; set; }
        public string CircuitName { get; set; }
        public string CustodyName { get; set; }
    }

    public sealed class DecidedRow
    {
        public Defendant D { get; set; }
        public long EventId { get; set; }
        public string DecisionCode { get; set; }
        public string NextSessionDate { get; set; }
        public string CourtNameAtEvent { get; set; }
        public string CircuitNameAtEvent { get; set; }
        public int? BailAmount { get; set; }
    }
}

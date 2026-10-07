namespace CourtDesk.Domain
{
    public sealed class DecisionRequest
    {
        public string RequestUuid;
        public int DefendantId;
        public int ExpectedVersion;
        public string Code;
        public LocalDate? NextDate;
        public string ReasonText;
        public string Note;
        public int? BailAmount;
        public int? CourtId;
        public int? CircuitId;
        public bool ConfirmPast;
        public bool ConfirmWeekend;
        public bool ConfirmFar;
    }
}

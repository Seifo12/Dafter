namespace CourtDesk.Domain
{
    public static class EventTypes
    {
        public const string Created = "CREATED";
        public const string Edited = "EDITED";
        public const string Decision = "DECISION";
        public const string Undo = "UNDO";
        public const string Deleted = "DELETED";
        public const string Restored = "RESTORED";
        public const string Imported = "IMPORTED";
    }

    public sealed class SessionEvent
    {
        public long Id;
        public string RequestUuid;
        public int DefendantId;
        public string EventType;
        public string DecisionCode;
        public string SessionDateHeld;
        public string NextSessionDate;
        public string ReasonText;
        public int? BailAmount;
        public string Note;
        public string CourtNameAtEvent;
        public string CircuitNameAtEvent;
        public string BeforeJson;
        public string AfterJson;
        public long? UndoesEventId;
        public long? UndoneByEventId;
        public string OperatorName;
        public string OccurredAtUtc;
        public string OccurredLocalDate;
    }
}

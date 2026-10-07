namespace CourtDesk.Domain
{
    public static class DefendantStatuses
    {
        public const string Detained = "DETAINED";
        public const string Released = "RELEASED";
        public const string Judged = "JUDGED";

        public static bool IsValid(string status)
        {
            return status == Detained || status == Released || status == Judged;
        }
    }
}

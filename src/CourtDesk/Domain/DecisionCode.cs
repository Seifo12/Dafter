namespace CourtDesk.Domain
{
    public static class DecisionCodes
    {
        public const string RenewDetention = "RENEW_DETENTION";
        public const string Postpone = "POSTPONE";
        public const string Release = "RELEASE";
        public const string ReleaseBail = "RELEASE_BAIL";
        public const string Judgment = "JUDGMENT";
        public const string Reopen = "REOPEN";

        public static bool IsValid(string code)
        {
            return code == RenewDetention || code == Postpone || code == Release
                || code == ReleaseBail || code == Judgment || code == Reopen;
        }

        public static bool RequiresNextDate(string code)
        {
            return code == RenewDetention || code == Postpone || code == Reopen;
        }

        public static bool AllowsReason(string code)
        {
            return code == RenewDetention || code == Postpone;
        }
    }
}

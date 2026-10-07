namespace CourtDesk.Domain
{
    public static class DisplayStates
    {
        public const string Today = "TODAY";
        public const string Overdue = "OVERDUE";
        public const string Upcoming = "UPCOMING";
        public const string NoDate = "NO_DATE";
        public const string Released = "RELEASED";
        public const string Judged = "JUDGED";
        public const string Hidden = "HIDDEN";
    }

    public static class DisplayState
    {
        public static string FromRow(bool isDeleted, string status, LocalDate? sessionDate, LocalDate today)
        {
            if (isDeleted)
            {
                return DisplayStates.Hidden;
            }
            if (status == DefendantStatuses.Released)
            {
                return DisplayStates.Released;
            }
            if (status == DefendantStatuses.Judged)
            {
                return DisplayStates.Judged;
            }
            if (!sessionDate.HasValue)
            {
                return DisplayStates.NoDate;
            }
            if (sessionDate.Value < today)
            {
                return DisplayStates.Overdue;
            }
            if (sessionDate.Value == today)
            {
                return DisplayStates.Today;
            }
            return DisplayStates.Upcoming;
        }
    }
}

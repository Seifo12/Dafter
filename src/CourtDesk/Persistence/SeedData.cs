namespace CourtDesk.Persistence
{
    public static class SeedData
    {
        public static void SeedLookups(DbContext ctx, string[] courts, string[] custody, string[] reasons)
        {
            var lookups = new SqliteLookupStore();
            if (lookups.Courts(ctx, false).Count == 0)
            {
                foreach (string c in courts)
                {
                    lookups.InsertCourt(ctx, c);
                }
            }
            if (lookups.CustodyLocations(ctx, false).Count == 0)
            {
                foreach (string c in custody)
                {
                    lookups.InsertCustody(ctx, c);
                }
            }
            if (lookups.Reasons(ctx, false).Count == 0)
            {
                foreach (string r in reasons)
                {
                    lookups.InsertReason(ctx, r);
                }
            }
        }
    }
}

using System;
using System.Globalization;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;

namespace CourtDesk.Application
{
    public static class ClockReasons
    {
        public const string Regression = "REGRESSION";
        public const string BeforeBuild = "BEFORE_BUILD";
    }

    public sealed class ClockVerdict
    {
        public bool Suspect;
        public string Reason;
        public LocalDate CurrentDate;
        public DateTime MaxSeenUtc;
        public bool HasMaxSeen;
    }

    public sealed class ClockSanityService
    {
        public const string MaxSeenKey = "max_seen_utc";

        private readonly IClock _clock;
        private readonly IDatabase _db;
        private readonly IMetaStore _meta;
        private readonly LocalDate _buildDate;

        public ClockSanityService(IDatabase db, IClock clock, IMetaStore meta, LocalDate buildDate)
        {
            _db = db;
            _clock = clock;
            _meta = meta;
            _buildDate = buildDate;
        }

        public ClockVerdict Check()
        {
            LocalDate today = _clock.Today;
            DateTime utc = _clock.UtcNow;
            string raw = _db.Read(s => _meta.GetMeta(s, MaxSeenKey));
            DateTime maxSeenUtc;
            bool haveMax = DateTime.TryParseExact(raw ?? "", "yyyy-MM-ddTHH:mm:ss'Z'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out maxSeenUtc);
            if (haveMax && utc < maxSeenUtc.AddMinutes(-5))
            {
                return new ClockVerdict
                {
                    Suspect = true,
                    Reason = ClockReasons.Regression,
                    CurrentDate = today,
                    MaxSeenUtc = maxSeenUtc,
                    HasMaxSeen = true
                };
            }
            if (today < _buildDate.AddDays(-1))
            {
                return new ClockVerdict
                {
                    Suspect = true,
                    Reason = ClockReasons.BeforeBuild,
                    CurrentDate = today,
                    MaxSeenUtc = maxSeenUtc,
                    HasMaxSeen = haveMax
                };
            }
            return new ClockVerdict
            {
                Suspect = false,
                CurrentDate = today,
                MaxSeenUtc = maxSeenUtc,
                HasMaxSeen = haveMax
            };
        }

        public static bool IsUtcFormat(string s)
        {
            DateTime ignored;
            return DateTime.TryParseExact(s ?? "", "yyyy-MM-ddTHH:mm:ss'Z'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out ignored);
        }
    }
}

using System;
using System.Collections.Generic;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;

namespace CourtDesk.Application
{
    public sealed class TodaySnapshot
    {
        public LocalDate Date;
        public readonly List<DefendantRow> Pending = new List<DefendantRow>();
        public readonly List<DefendantRow> Overdue = new List<DefendantRow>();
        public readonly List<DecidedRow> Decided = new List<DecidedRow>();
        public readonly List<DefendantRow> Tomorrow = new List<DefendantRow>();
        public int TomorrowCount;
        public int NoDateCount;
        public long ChangeSeq;

        public int PendingCount { get { return Pending.Count; } }
        public int DecidedCount { get { return Decided.Count; } }
        public int Total { get { return Decided.Count + Pending.Count; } }
    }

    public sealed class TodayService
    {
        private readonly IDatabase _db;
        private readonly IDefendantStore _defendants;
        private readonly IClock _clock;
        private readonly SettingsService _settings;
        private TodaySnapshot _cached;
        private long _cachedSeq = -1;

        public TodayService(IDatabase db, IDefendantStore defendants, IClock clock, SettingsService settings)
        {
            _db = db;
            _defendants = defendants;
            _clock = clock;
            _settings = settings;
        }

        public event Action Changed;

        public void Invalidate()
        {
            _cached = null;
            _cachedSeq = -1;
        }

        public TodaySnapshot Snapshot()
        {
            LocalDate today = _clock.Today;
            long seq = CurrentSeq();
            TodaySnapshot c = _cached;
            if (c != null && c.Date == today && c.ChangeSeq == seq)
            {
                return c;
            }
            string iso = today.ToIso();
            string tomorrow = today.AddDays(1).ToIso();
            TodaySnapshot built = _db.Read(s =>
            {
                var snap = new TodaySnapshot { Date = today };
                snap.Pending.AddRange(_defendants.ListPendingOn(s, iso));
                snap.Overdue.AddRange(_defendants.ListOverdueRows(s, iso));
                snap.Decided.AddRange(_defendants.ListDecidedOn(s, iso));
                snap.TomorrowCount = _defendants.CountOn(s, tomorrow);
                if (_defendants.ListPendingOn != null)
                {
                    snap.Tomorrow.AddRange(_defendants.ListPendingOn(s, tomorrow));
                }
                snap.NoDateCount = _defendants.CountNoDate(s);
                return snap;
            });
            built.ChangeSeq = seq;
            _cached = built;
            _cachedSeq = seq;
            Action h = Changed;
            if (h != null)
            {
                h();
            }
            return built;
        }

        private long CurrentSeq()
        {
            var src = _db as IChangeSeqSource;
            return src == null ? 0 : src.CurrentChangeSeq();
        }
    }

    public sealed class ScheduleService
    {
        private readonly IDatabase _db;
        private readonly IDefendantStore _defendants;
        private readonly IClock _clock;

        public ScheduleService(IDatabase db, IDefendantStore defendants, IClock clock)
        {
            _db = db;
            _defendants = defendants;
            _clock = clock;
        }

        public List<DefendantRow> Day(LocalDate date)
        {
            string iso = date.ToIso();
            return _db.Read(s => _defendants.ListPendingOn(s, iso));
        }

        public int CountForDay(LocalDate date)
        {
            string iso = date.ToIso();
            return _db.Read(s => _defendants.CountOn(s, iso));
        }

        public List<DateCount> NextDays(LocalDate from, int count)
        {
            var out_ = new List<DateCount>();
            for (int i = 0; i < count; i++)
            {
                LocalDate d = from.AddDays(i);
                out_.Add(new DateCount { Date = d, Count = CountForDay(d) });
            }
            return out_;
        }
    }

    public sealed class DateCount
    {
        public LocalDate Date;
        public int Count;
    }
}

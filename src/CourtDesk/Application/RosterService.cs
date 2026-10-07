using System;
using System.Collections.Generic;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;

namespace CourtDesk.Application
{
    public sealed class RosterEntry
    {
        public int DefendantId;
        public string FullName;
        public string CaseNumber;
        public int CaseYear;
        public string Charges;
        public string CourtName;
        public string CircuitName;
        public string CustodyName;
        public bool FromDecision;
        public string DecisionCode;
    }

    public sealed class RosterModel
    {
        public LocalDate Date;
        public string HeaderLine1;
        public string DirectorateName;
        public string StationName;
        public string OperatorName;
        public string PrintedAtDateIso;
        public string PrintedAtTimeHm;
        public readonly List<RosterEntry> Entries = new List<RosterEntry>();
        public readonly Dictionary<string, int> PerCourtCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        public int Count { get { return Entries.Count; } }
    }

    public sealed class RosterService
    {
        private readonly IDatabase _db;
        private readonly IDefendantStore _defendants;
        private readonly IEventStore _events;
        private readonly IClock _clock;
        private readonly SettingsService _settings;

        public RosterService(IDatabase db, IDefendantStore defendants, IEventStore events,
            IClock clock, SettingsService settings)
        {
            _db = db;
            _defendants = defendants;
            _events = events;
            _clock = clock;
            _settings = settings;
        }

        public RosterModel Build(LocalDate date)
        {
            string iso = date.ToIso();
            string header1 = _settings.Get(SettingKeys.RosterHeaderLine1);
            string directorate = _settings.Get(SettingKeys.DirectorateName);
            string station = _settings.Get(SettingKeys.StationName);
            string operatorName = _settings.OperatorOrUnknown();
            RosterModel model = _db.Read(s =>
            {
                var m = new RosterModel
                {
                    Date = date,
                    HeaderLine1 = header1,
                    DirectorateName = directorate,
                    StationName = station,
                    OperatorName = operatorName
                };
                var seen = new HashSet<int>();
                foreach (DefendantRow row in _defendants.ListPendingOn(s, iso))
                {
                    seen.Add(row.D.Id);
                    m.Entries.Add(new RosterEntry
                    {
                        DefendantId = row.D.Id,
                        FullName = row.D.FullName,
                        CaseNumber = row.D.CaseNumber,
                        CaseYear = row.D.CaseYear,
                        Charges = row.D.Charges,
                        CourtName = row.CourtName,
                        CircuitName = row.CircuitName,
                        CustodyName = row.CustodyName,
                        FromDecision = false
                    });
                }
                foreach (DecidedRow row in _defendants.ListDecidedOn(s, iso))
                {
                    if (!seen.Add(row.D.Id))
                    {
                        continue;
                    }
                    m.Entries.Add(new RosterEntry
                    {
                        DefendantId = row.D.Id,
                        FullName = row.D.FullName,
                        CaseNumber = row.D.CaseNumber,
                        CaseYear = row.D.CaseYear,
                        Charges = row.D.Charges,
                        CourtName = row.CourtNameAtEvent,
                        CircuitName = row.CircuitNameAtEvent,
                        CustodyName = _defendants.CustodyName(s, row.D.CustodyLocationId),
                        FromDecision = true,
                        DecisionCode = row.DecisionCode
                    });
                }
                return m;
            });
            Sort(model);
            foreach (RosterEntry e in model.Entries)
            {
                string key = string.IsNullOrEmpty(e.CourtName) ? "" : e.CourtName;
                int n;
                model.PerCourtCounts.TryGetValue(key, out n);
                model.PerCourtCounts[key] = n + 1;
            }
            return model;
        }

        public List<int> DefendantIds(RosterModel model)
        {
            var ids = new List<int>();
            foreach (RosterEntry e in model.Entries)
            {
                ids.Add(e.DefendantId);
            }
            return ids;
        }

        private static void Sort(RosterModel m)
        {
            m.Entries.Sort(delegate(RosterEntry a, RosterEntry b)
            {
                int c = string.CompareOrdinal(
                    ArabicText.Normalize(a.CourtName ?? ""),
                    ArabicText.Normalize(b.CourtName ?? ""));
                if (c != 0)
                {
                    return c;
                }
                c = string.CompareOrdinal(
                    ArabicText.Normalize(a.CircuitName ?? ""),
                    ArabicText.Normalize(b.CircuitName ?? ""));
                if (c != 0)
                {
                    return c;
                }
                return string.CompareOrdinal(
                    ArabicText.Normalize(a.FullName ?? ""),
                    ArabicText.Normalize(b.FullName ?? ""));
            });
        }
    }
}

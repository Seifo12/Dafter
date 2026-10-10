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
        public string DecisionLabel;
        public string BailAmount;
        public string DetainedSince;
    }

    public sealed class RosterModel
    {
        public LocalDate Date;
        public bool AllDates;
        public string HeaderLine1;
        public string DirectorateName;
        public string StationName;
        public string OperatorName;
        public string OfficerName;
        public string PreparerName;
        public string CustodyOfficerName;
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
            return Build(date, null);
        }

        public RosterModel Build(LocalDate date, string scope)
        {
            bool scoped = !string.IsNullOrEmpty(scope)
                && !string.Equals(scope, Filters.All, StringComparison.Ordinal);
            RosterModel model = _db.Read(s => BuildEntries(s, date, scope, scoped));
            Finish(model);
            return model;
        }

        public RosterModel BuildAll(LocalDate date, string scope)
        {
            bool scoped = !string.IsNullOrEmpty(scope)
                && !string.Equals(scope, Filters.All, StringComparison.Ordinal);
            RosterModel model = _db.Read(s => BuildEntriesAll(s, date, scope, scoped));
            Finish(model);
            return model;
        }

        public RosterModel BuildSelected(LocalDate date, int[] ids)
        {
            var m = NewModel(date, false);
            if (ids == null || ids.Length == 0)
            {
                Finish(m);
                return m;
            }
            m.AllDates = true;
            var want = new HashSet<int>(ids);
            _db.Read(s =>
            {
                foreach (DefendantRow row in _defendants.ListRowsByIds(s, ids))
                {
                    if (!want.Contains(row.D.Id))
                    {
                        continue;
                    }
                    AddPending(m, row, null, false, new HashSet<int>());
                }
                foreach (DecidedRow row in _defendants.ListDecidedAll(s))
                {
                    if (!want.Contains(row.D.Id))
                    {
                        continue;
                    }
                    AddDecided(m, s, row, null, false, new HashSet<int>());
                }
                return 0;
            });
            var seen = new HashSet<int>();
            foreach (RosterEntry e in m.Entries)
            {
                seen.Add(e.DefendantId);
            }
            Finish(m);
            return m;
        }

        private static void Finish(RosterModel model)
        {
            Sort(model);
            foreach (RosterEntry e in model.Entries)
            {
                string key = string.IsNullOrEmpty(e.CourtName) ? "" : e.CourtName;
                int n;
                model.PerCourtCounts.TryGetValue(key, out n);
                model.PerCourtCounts[key] = n + 1;
            }
        }

        private RosterModel NewModel(LocalDate date, bool allDates)
        {
            return new RosterModel
            {
                Date = date,
                AllDates = allDates,
                HeaderLine1 = _settings.Get(SettingKeys.RosterHeaderLine1),
                DirectorateName = _settings.Get(SettingKeys.DirectorateName),
                StationName = _settings.Get(SettingKeys.StationName),
                OperatorName = _settings.OperatorOrUnknown(),
                OfficerName = _settings.Get(SettingKeys.OperatorName) ?? "",
                PreparerName = _settings.Get(SettingKeys.PreparerName) ?? "",
                CustodyOfficerName = _settings.Get(SettingKeys.CustodyOfficerName) ?? ""
            };
        }

        private RosterModel BuildEntries(ISession s, LocalDate date, string scope, bool scoped)
        {
            string iso = date.ToIso();
            var m = NewModel(date, false);
            var seen = new HashSet<int>();
            foreach (DefendantRow row in _defendants.ListPendingOn(s, iso))
            {
                AddPending(m, row, scope, scoped, seen);
            }
            foreach (DecidedRow row in _defendants.ListDecidedOn(s, iso))
            {
                AddDecided(m, s, row, scope, scoped, seen);
            }
            return m;
        }

        private RosterModel BuildEntriesAll(ISession s, LocalDate date, string scope, bool scoped)
        {
            var m = NewModel(date, true);
            var seen = new HashSet<int>();
            foreach (DefendantRow row in _defendants.ListPendingAll(s))
            {
                AddPending(m, row, scope, scoped, seen);
            }
            foreach (DecidedRow row in _defendants.ListDecidedAll(s))
            {
                AddDecided(m, s, row, scope, scoped, seen);
            }
            return m;
        }

        private static void AddPending(RosterModel m, DefendantRow row, string scope, bool scoped, HashSet<int> seen)
        {
            if (scoped && !InScope(scope, row.D.Status))
            {
                return;
            }
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

        private void AddDecided(RosterModel m, ISession s, DecidedRow row, string scope, bool scoped, HashSet<int> seen)
        {
            if (scoped && !InScope(scope, row.D.Status))
            {
                return;
            }
            if (!seen.Add(row.D.Id))
            {
                return;
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
                DecisionCode = row.DecisionCode,
                DecisionLabel = CourtDesk.UI.ViewModels.DecisionWizardViewModel
                    .LabelOf(row.DecisionCode),
                BailAmount = row.BailAmount.HasValue
                    ? row.BailAmount.Value.ToString("0.##",
                        System.Globalization.CultureInfo.InvariantCulture) : "",
                DetainedSince = row.D.DetainedSince.HasValue
                    ? ArabicDateFormat.FormatShort(row.D.DetainedSince.Value) : ""
            });
        }

        private static bool InScope(string scope, string status)
        {
            if (string.Equals(scope, Filters.Detained, StringComparison.Ordinal))
            {
                return string.Equals(status, DefendantStatuses.Detained, StringComparison.Ordinal);
            }
            if (string.Equals(scope, Filters.Released, StringComparison.Ordinal))
            {
                return string.Equals(status, DefendantStatuses.Released, StringComparison.Ordinal);
            }
            if (string.Equals(scope, Filters.Judged, StringComparison.Ordinal))
            {
                return string.Equals(status, DefendantStatuses.Judged, StringComparison.Ordinal);
            }
            if (string.Equals(scope, Filters.NoDate, StringComparison.Ordinal))
            {
                return false;
            }
            return true;
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

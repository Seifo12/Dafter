using System;
using System.Collections.Generic;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;

namespace CourtDesk.Application
{
    public static class FieldErrors
    {
        public const string Name = "val.name";
        public const string NationalId = "val.national_id";
        public const string CaseNumber = "val.case_number";
        public const string CaseYear = "val.case_year";
        public const string DateInvalid = "val.date_invalid";
        public const string CircuitCourt = "val.circuit_court";
        public const string SessionNotWhileNotDetained = "val.session_not_allowed";
    }

    public sealed class DefendantInput
    {
        public string FullName;
        public string NationalId;
        public string CaseNumber;
        public string CaseYear;
        public string CaseType;
        public string Charges;
        public int? CourtId;
        public int? CircuitId;
        public int? CustodyLocationId;
        public LocalDate? SessionDate;
        public LocalDate? DetainedSince;
        public string Notes;
    }

    public sealed class CreateResult
    {
        public int Id;
        public List<DuplicateHit> Duplicates = new List<DuplicateHit>();
    }

    public sealed class DefendantService
    {
        private readonly IDatabase _db;
        private readonly IDefendantStore _defendants;
        private readonly IEventStore _events;
        private readonly IClock _clock;
        private readonly SettingsService _settings;
        private readonly ILogger _log;

        public DefendantService(IDatabase db, IDefendantStore defendants, IEventStore events,
            IClock clock, SettingsService settings, ILogger log)
        {
            _db = db;
            _defendants = defendants;
            _events = events;
            _clock = clock;
            _settings = settings;
            _log = log;
        }

        public static List<string> Validate(DefendantInput input, LocalDate today, string currentStatus)
        {
            var errs = new List<string>();
            string name = Clean(input.FullName);
            if (name.Length < 2 || name.Length > 200)
            {
                errs.Add(FieldErrors.Name);
            }
            string nat = Clean(input.NationalId);
            if (nat.Length != 0 && nat.Length != 14)
            {
                errs.Add(FieldErrors.NationalId);
            }
            string cn = Clean(input.CaseNumber);
            if (cn.Length < 1 || cn.Length > 12 || !AllDigits(cn))
            {
                errs.Add(FieldErrors.CaseNumber);
            }
            int year;
            if (!int.TryParse(Clean(input.CaseYear), out year) || year < 1950 || year > today.Year + 1)
            {
                errs.Add(FieldErrors.CaseYear);
            }
            if (input.SessionDate.HasValue && currentStatus != null
                && currentStatus != DefendantStatuses.Detained)
            {
                errs.Add(FieldErrors.SessionNotWhileNotDetained);
            }
            if (input.CircuitId.HasValue && !input.CourtId.HasValue)
            {
                errs.Add(FieldErrors.CircuitCourt);
            }
            return errs;
        }

        public CreateResult Create(DefendantInput input, string requestUuid, bool forceDespiteDuplicates)
        {
            LocalDate today = _clock.Today;
            List<string> errs = Validate(input, today, DefendantStatuses.Detained);
            if (errs.Count > 0)
            {
                throw new FieldValidationException(errs);
            }
            Defendant d = ToRow(input, today);
            CreateResult result = _db.Write(s =>
            {
                DefendantSnapshot after = DefendantSnapshot.FromDefendant(d);
                string nameNorm = ArabicText.Normalize(d.FullName);
                List<DuplicateHit> dups = _defendants.FindDuplicates(s, nameNorm, d.CaseNumber, d.CaseYear, 0);
                int id = _defendants.Insert(s, d);
                after.Id = id;
                _events.Insert(s, new SessionEvent
                {
                    RequestUuid = requestUuid,
                    DefendantId = id,
                    EventType = EventTypes.Created,
                    BeforeJson = null,
                    AfterJson = SnapshotJson.Write(after),
                    OperatorName = _settings.OperatorOrUnknown(),
                    OccurredAtUtc = s.UtcNowIso,
                    OccurredLocalDate = today.ToIso()
                });
                if (!string.IsNullOrEmpty(d.Charges))
                {
                    _log.Info("E-DB-010", "charges recorded id=" + id);
                }
                var r = new CreateResult { Id = id };
                r.Duplicates.AddRange(dups);
                return r;
            });
            if (!forceDespiteDuplicates && result.Duplicates.Count > 0)
            {
                return result;
            }
            return result;
        }

        public bool Edit(int id, int expectedVersion, DefendantInput input, string requestUuid)
        {
            LocalDate today = _clock.Today;
            return _db.Write(s =>
            {
                Defendant row = _defendants.GetById(s, id);
                if (row == null || row.IsDeleted || row.RowVersion != expectedVersion)
                {
                    throw new StaleRecordException();
                }
                List<string> errs = Validate(input, today, row.Status);
                if (errs.Count > 0)
                {
                    throw new FieldValidationException(errs);
                }
                DefendantSnapshot before = DefendantSnapshot.FromDefendant(row);
                Defendant edited = ToRow(input, today);
                edited.Id = row.Id;
                edited.RowVersion = row.RowVersion;
                edited.IsDeleted = row.IsDeleted;
                edited.Status = row.Status;
                edited.DetainedSince = input.DetainedSince.HasValue
                    ? input.DetainedSince : row.DetainedSince;
                edited.SessionDate = row.Status == DefendantStatuses.Detained
                    ? (input.SessionDate.HasValue ? input.SessionDate : row.SessionDate)
                    : row.SessionDate;
                if (SameState(before, DefendantSnapshot.FromDefendant(edited)))
                {
                    return true;
                }
                string beforeJson = SnapshotJson.Write(before);
                if (!_defendants.UpdateFull(s, edited))
                {
                    throw new StaleRecordException();
                }
                _events.Insert(s, new SessionEvent
                {
                    RequestUuid = requestUuid,
                    DefendantId = id,
                    EventType = EventTypes.Edited,
                    BeforeJson = beforeJson,
                    AfterJson = SnapshotJson.Write(DefendantSnapshot.FromDefendant(edited)),
                    OperatorName = _settings.OperatorOrUnknown(),
                    OccurredAtUtc = s.UtcNowIso,
                    OccurredLocalDate = today.ToIso()
                });
                return true;
            });
        }

        public bool Void(int id, int expectedVersion, string requestUuid)
        {
            LocalDate today = _clock.Today;
            return _db.Write(s =>
            {
                Defendant row = _defendants.GetById(s, id);
                if (row == null || row.IsDeleted || row.RowVersion != expectedVersion)
                {
                    throw new StaleRecordException();
                }
                DefendantSnapshot before = DefendantSnapshot.FromDefendant(row);
                if (!_defendants.SetDeleted(s, id, expectedVersion, true))
                {
                    throw new StaleRecordException();
                }
                DefendantSnapshot after = SnapshotJson.Read(beforeJson(before));
                after.IsDeleted = true;
                row.IsDeleted = true;
                _events.Insert(s, new SessionEvent
                {
                    RequestUuid = requestUuid,
                    DefendantId = id,
                    EventType = EventTypes.Deleted,
                    BeforeJson = SnapshotJson.Write(before),
                    AfterJson = SnapshotJson.Write(after),
                    OperatorName = _settings.OperatorOrUnknown(),
                    OccurredAtUtc = s.UtcNowIso,
                    OccurredLocalDate = today.ToIso()
                });
                return true;
            });
        }

        public bool Restore(int id, int expectedVersion, string requestUuid)
        {
            LocalDate today = _clock.Today;
            return _db.Write(s =>
            {
                Defendant row = _defendants.GetById(s, id);
                if (row == null || !row.IsDeleted || row.RowVersion != expectedVersion)
                {
                    throw new StaleRecordException();
                }
                DefendantSnapshot before = DefendantSnapshot.FromDefendant(row);
                if (!_defendants.SetDeleted(s, id, expectedVersion, false))
                {
                    throw new StaleRecordException();
                }
                row.IsDeleted = false;
                _events.Insert(s, new SessionEvent
                {
                    RequestUuid = requestUuid,
                    DefendantId = id,
                    EventType = EventTypes.Restored,
                    BeforeJson = SnapshotJson.Write(before),
                    AfterJson = SnapshotJson.Write(DefendantSnapshot.FromDefendant(row)),
                    OperatorName = _settings.OperatorOrUnknown(),
                    OccurredAtUtc = s.UtcNowIso,
                    OccurredLocalDate = today.ToIso()
                });
                return true;
            });
        }

        public List<DuplicateHit> FindDuplicates(string fullName, string caseNumber, int caseYear, int excludeId)
        {
            return _db.Read(s => _defendants.FindDuplicates(s, ArabicText.Normalize(fullName), caseNumber, caseYear, excludeId));
        }

        public Defendant Get(int id)
        {
            return _db.Read(s => _defendants.GetById(s, id));
        }

        public List<Defendant> Trash()
        {
            return _db.Read(s => _defendants.ListDeleted(s));
        }

        public List<SessionEvent> History(int id)
        {
            return _db.Read(s => _events.ListByDefendant(s, id));
        }

        private static string beforeJson(DefendantSnapshot s)
        {
            return SnapshotJson.Write(s);
        }

        private static bool SameState(DefendantSnapshot a, DefendantSnapshot b)
        {
            return SnapshotJson.Write(a) == SnapshotJson.Write(b);
        }

        private static Defendant ToRow(DefendantInput input, LocalDate today)
        {
            return new Defendant
            {
                FullName = Clean(input.FullName),
                NationalId = NullIfEmpty(Clean(input.NationalId)),
                CaseNumber = Clean(input.CaseNumber),
                CaseYear = int.Parse(Clean(input.CaseYear)),
                CaseType = Clean(input.CaseType),
                Charges = Clean(input.Charges),
                CourtId = input.CourtId,
                CircuitId = input.CircuitId,
                CustodyLocationId = input.CustodyLocationId,
                SessionDate = input.SessionDate,
                Status = DefendantStatuses.Detained,
                DetainedSince = input.DetainedSince.HasValue ? input.DetainedSince : today,
                Notes = Clean(input.Notes),
                IsDeleted = false,
                RowVersion = 0
            };
        }

        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            var sb = new System.Text.StringBuilder(s.Length);
            bool space = true;
            foreach (char c in s)
            {
                if (char.IsControl(c))
                {
                    continue;
                }
                if (c == ' ')
                {
                    if (space)
                    {
                        continue;
                    }
                    space = true;
                    sb.Append(' ');
                    continue;
                }
                space = false;
                sb.Append(c);
            }
            string outp = sb.ToString();
            if (outp.Length > 0 && outp[outp.Length - 1] == ' ')
            {
                outp = outp.Substring(0, outp.Length - 1);
            }
            return outp;
        }

        private static string NullIfEmpty(string s)
        {
            return s.Length == 0 ? null : s;
        }

        private static bool AllDigits(string s)
        {
            foreach (char c in s)
            {
                if (c < '0' || c > '9')
                {
                    return false;
                }
            }
            return s.Length > 0;
        }
    }

    public sealed class FieldValidationException : Exception
    {
        public readonly List<string> Errors;

        public FieldValidationException(List<string> errors)
            : base("fields: " + string.Join(",", errors.ToArray()))
        {
            Errors = errors;
        }
    }

    public sealed class StaleRecordException : Exception
    {
        public StaleRecordException() : base("stale") { }
    }
}

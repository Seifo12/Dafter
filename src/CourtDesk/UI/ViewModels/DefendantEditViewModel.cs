using System;
using System.Collections.Generic;
using System.Windows.Input;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;
using CourtDesk.Resources;

namespace CourtDesk.UI.ViewModels
{
    public sealed class FieldProblem
    {
        public string Key { get; set; }
        public string Field { get; set; }
    }

    public sealed class DefendantEditViewModel : ObservableObject
    {
        private readonly DefendantService _service;
        private readonly LookupService _lookups;
        private readonly IClock _clock;
        private readonly int _version;
        private readonly string _status;
        private string _name = "";
        private string _nationalId = "";
        private string _caseNumber = "";
        private string _caseYear = "";
        private string _caseType = "";
        private string _charges = "";
        private string _notes = "";
        private int? _courtId;
        private int? _circuitId;
        private int? _custodyId;
        private LocalDate? _sessionDate;
        private LocalDate? _detainedSince;
        private bool _noSessionDate;
        private string _summary = "";
        private readonly List<FieldProblem> _problems = new List<FieldProblem>();
        private bool _saved;
        private int _id;

        public DefendantEditViewModel(DefendantService service, LookupService lookups,
            IClock clock, Defendant existing)
        {
            _service = service;
            _lookups = lookups;
            _clock = clock;
            _id = existing == null ? 0 : existing.Id;
            _version = existing == null ? 0 : existing.RowVersion;
            _status = existing == null ? DefendantStatuses.Detained : existing.Status;
            if (existing != null)
            {
                Name = existing.FullName;
                NationalId = existing.NationalId ?? "";
                CaseNumber = existing.CaseNumber;
                CaseYear = existing.CaseYear.ToString();
                CaseType = existing.CaseType ?? "";
                Charges = existing.Charges ?? "";
                Notes = existing.Notes ?? "";
                CourtId = existing.CourtId;
                CircuitId = existing.CircuitId;
                CustodyId = existing.CustodyLocationId;
                SessionDate = existing.SessionDate;
                DetainedSince = existing.DetainedSince;
            }
            else
            {
                CaseYear = _clock.Today.Year.ToString();
                DetainedSince = _clock.Today;
            }
            NoSessionDate = !SessionDate.HasValue;
            SaveCommand = new RelayCommand(delegate { Save(false); });
            SaveAnywayCommand = new RelayCommand(delegate { Save(true); });
            SessionTodayCommand = new RelayCommand(delegate { SessionDate = _clock.Today; NoSessionDate = false; });
            SessionTomorrowCommand = new RelayCommand(delegate { SessionDate = _clock.Today.AddDays(1); NoSessionDate = false; });
            NoSessionCommand = new RelayCommand(delegate { SessionDate = null; NoSessionDate = true; });
        }

        public ICommand SaveCommand { get; private set; }
        public ICommand SaveAnywayCommand { get; private set; }
        public ICommand SessionTodayCommand { get; private set; }
        public ICommand SessionTomorrowCommand { get; private set; }
        public ICommand NoSessionCommand { get; private set; }

        public event Action<DuplicateHit> DuplicateFound;
        public event Action Saved;

        public int Id { get { return _id; } }
        public bool IsNew { get { return _id == 0; } }

        public int SavedId { get { return _saved ? _id : 0; } }
        public bool SessionEditable { get { return _status == DefendantStatuses.Detained; } }
        public string Title { get { return IsNew ? Strings.FormAddTitle : Strings.FormEditTitle; } }

        public string Name
        {
            get { return _name; }
            set { Set(ref _name, value ?? "", "Name"); }
        }

        public string NationalId
        {
            get { return _nationalId; }
            set { Set(ref _nationalId, value ?? "", "NationalId"); }
        }

        public string CaseNumber
        {
            get { return _caseNumber; }
            set { Set(ref _caseNumber, value ?? "", "CaseNumber"); }
        }

        public string CaseYear
        {
            get { return _caseYear; }
            set { Set(ref _caseYear, value ?? "", "CaseYear"); }
        }

        public string CaseType
        {
            get { return _caseType; }
            set { Set(ref _caseType, value ?? "", "CaseType"); }
        }

        public string Charges
        {
            get { return _charges; }
            set { Set(ref _charges, value ?? "", "Charges"); }
        }

        public string Notes
        {
            get { return _notes; }
            set { Set(ref _notes, value ?? "", "Notes"); }
        }

        public int? CourtId
        {
            get { return _courtId; }
            set
            {
                if (Set(ref _courtId, value, "CourtId"))
                {
                    _circuitId = null;
                    Raise("CircuitId");
                }
            }
        }

        public int? CircuitId
        {
            get { return _circuitId; }
            set { Set(ref _circuitId, value, "CircuitId"); }
        }

        public int? CustodyId
        {
            get { return _custodyId; }
            set { Set(ref _custodyId, value, "CustodyId"); }
        }

        public LocalDate? SessionDate
        {
            get { return _sessionDate; }
            set
            {
                if (Set(ref _sessionDate, value, "SessionDate"))
                {
                    Raise("SessionText");
                }
            }
        }

        public LocalDate? DetainedSince
        {
            get { return _detainedSince; }
            set { Set(ref _detainedSince, value, "DetainedSince"); }
        }

        public bool NoSessionDate
        {
            get { return _noSessionDate; }
            set { Set(ref _noSessionDate, value, "NoSessionDate"); }
        }

        public string SessionText
        {
            get
            {
                if (!SessionDate.HasValue)
                {
                    return Strings.FieldNoDate;
                }
                return ArabicDateFormat.FormatLong(SessionDate.Value, Strings.Weekdays, Strings.Months);
            }
        }

        public string Summary
        {
            get { return _summary; }
        }

        public bool HasProblems
        {
            get { return _problems.Count > 0; }
        }

        public string FirstProblemField
        {
            get { return _problems.Count == 0 ? null : _problems[0].Field; }
        }

        public bool WasSaved
        {
            get { return _saved; }
        }

        public DefendantInput BuildInput()
        {
            return new DefendantInput
            {
                FullName = Name,
                NationalId = NationalId,
                CaseNumber = WesternDigits(CaseNumber),
                CaseYear = WesternDigits(CaseYear),
                CaseType = CaseType,
                Charges = Charges,
                Notes = Notes,
                CourtId = CourtId,
                CircuitId = CircuitId,
                CustodyLocationId = CustodyId,
                SessionDate = NoSessionDate ? (LocalDate?)null : SessionDate,
                DetainedSince = DetainedSince
            };
        }

        public static string WesternDigits(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (c >= '\u0660' && c <= '\u0669')
                {
                    sb.Append((char)('0' + (c - '\u0660')));
                }
                else if (c >= '\u06F0' && c <= '\u06F9')
                {
                    sb.Append((char)('0' + (c - '\u06F0')));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static string FieldOf(string errorKey)
        {
            switch (errorKey)
            {
                case Application.FieldErrors.Name: return "Name";
                case Application.FieldErrors.NationalId: return "NationalId";
                case Application.FieldErrors.CaseNumber: return "CaseNumber";
                case Application.FieldErrors.CaseYear: return "CaseYear";
                case Application.FieldErrors.CircuitCourt: return "CircuitId";
                default: return "Name";
            }
        }

        private void Save(bool force)
        {
            _problems.Clear();
            DefendantInput input = BuildInput();
            List<string> errs = DefendantService.Validate(input, _clock.Today, _status);
            foreach (string e in errs)
            {
                _problems.Add(new FieldProblem { Key = e, Field = FieldOf(e) });
            }
            _summary = errs.Count == 0 ? "" : Strings.ValSummary + " " + errs[0];
            Raise("Summary");
            Raise("HasProblems");
            Raise("FirstProblemField");
            if (errs.Count > 0)
            {
                return;
            }
            try
            {
                if (IsNew)
                {
                    CreateResult r = _service.Create(input, Guid.NewGuid().ToString("N"), force);
                    _id = r.Id;
                    if (!force && r.Duplicates.Count > 0)
                    {
                        Action<DuplicateHit> h = DuplicateFound;
                        if (h != null)
                        {
                            h(r.Duplicates[0]);
                        }
                    }
                }
                else
                {
                    _service.Edit(_id, _version, input, Guid.NewGuid().ToString("N"));
                }
                _saved = true;
                _lookups.RecordChargeUse(input.Charges);
                Action s = Saved;
                if (s != null)
                {
                    s();
                }
            }
            catch (StaleRecordException)
            {
                _summary = Strings.ErrStale;
                Raise("Summary");
                Raise("HasProblems");
            }
        }
    }
}
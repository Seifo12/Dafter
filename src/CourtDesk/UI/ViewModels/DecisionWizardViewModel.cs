using System;
using System.Collections.Generic;
using System.Windows.Input;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;
using CourtDesk.Resources;

namespace CourtDesk.UI.ViewModels
{
    public sealed class DecisionTile
    {
        private readonly Action<string> _pick;

        public DecisionTile(string code, string label)
        {
            Code = code;
            Label = label;
            _pick = null;
        }

        public DecisionTile(string code, string label, Action<string> pick)
        {
            Code = code;
            Label = label;
            _pick = pick;
        }

        public string Code { get; private set; }
        public string Label { get; private set; }
        public bool Selected { get; set; }

        public void Pick()
        {
            if (_pick != null)
            {
                _pick(Code);
            }
        }
    }

    public sealed class OffsetButton
    {
        public int Days { get; set; }
        public string Label { get { return string.Format(Strings.WizOffset, Days); } }
    }

    public sealed class WizardStage
    {
        public string Number { get; set; }
        public string Sentence { get; set; }
        public string Value { get; set; }
    }

    public sealed class DecisionWizardViewModel : ObservableObject
    {
        private readonly DecisionService _decisions;
        private readonly UndoService _undo;
        private readonly DefendantService _defendants;
        private readonly LookupService _lookups;
        private readonly SettingsService _settings;
        private readonly IClock _clock;
        private readonly List<int> _queue;
        private readonly int _at;
        private readonly List<int> _decidedIds = new List<int>();
        private Defendant _defendant;
        private int _version;
        private string _code;
        private LocalDate? _nextDate;
        private string _reason;
        private string _note;
        private string _bail = "";
        private bool _confirmPast;
        private bool _confirmWeekend;
        private bool _confirmFar;
        private int _step = 1;
        private string _error = "";
        private bool _saving;
        private bool _done;
        private readonly List<DecisionTile> _tiles = new List<DecisionTile>();
        private readonly List<OffsetButton> _offsets = new List<OffsetButton>();
        private readonly List<WizardStage> _review = new List<WizardStage>();

        public DecisionWizardViewModel(DecisionService decisions, UndoService undo,
            DefendantService defendants, LookupService lookups, SettingsService settings,
            IClock clock, List<int> queue, int at)
        {
            _decisions = decisions;
            _undo = undo;
            _defendants = defendants;
            _lookups = lookups;
            _settings = settings;
            _clock = clock;
            _queue = queue ?? new List<int>();
            _at = at;
            PickCommand = new RelayCommand(delegate(object o)
            {
                DecisionTile t = o as DecisionTile;
                if (t != null)
                {
                    Choose(t.Code);
                }
            });
            NextCommand = new RelayCommand(delegate { GoNext(); });
            BackCommand = new RelayCommand(delegate { GoBack(); });
            SaveCommand = new RelayCommand(delegate { Save(); });
            CancelCommand = new RelayCommand(delegate
            {
                Action h = Cancelled;
                if (h != null)
                {
                    h();
                }
            });
            CloseCommand = new RelayCommand(delegate
            {
                Action h = Closed;
                if (h != null)
                {
                    h();
                }
            });
            UndoCommand = new RelayCommand(delegate
            {
                Action h = UndoRequested;
                if (h != null)
                {
                    h();
                }
            });
            NextQueueCommand = new RelayCommand(delegate
            {
                Action h = NextInQueue;
                if (h != null)
                {
                    h();
                }
            });
            StopQueueCommand = new RelayCommand(delegate
            {
                Action h = QueueStopped;
                if (h != null)
                {
                    h();
                }
            });
            foreach (int d in settings.QuickOffsets())
            {
                _offsets.Add(new OffsetButton { Days = d });
            }
            LoadDefendant();
        }

        public ICommand PickCommand { get; private set; }
        public ICommand NextCommand { get; private set; }
        public ICommand BackCommand { get; private set; }
        public ICommand SaveCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }
        public ICommand CloseCommand { get; private set; }
        public ICommand UndoCommand { get; private set; }
        public ICommand NextQueueCommand { get; private set; }
        public ICommand StopQueueCommand { get; private set; }

        public event Action Cancelled;
        public event Action Closed;
        public event Action UndoRequested;
        public event Action NextInQueue;
        public event Action QueueStopped;
        public event Action<Sentence> Saved;

        public List<DecisionTile> Tiles { get { return _tiles; } }
        public List<OffsetButton> Offsets { get { return _offsets; } }
        public List<WizardStage> Review { get { return _review; } }

        public string Title { get { return Strings.WizTitle; } }
        public bool InQueue { get { return _queue.Count > 1; } }
        public string QueueProgress { get { return string.Format(Strings.WizQueueProgress, _at + 1, _queue.Count); } }

        public int DefendantId { get { return _defendant.Id; } }

        public List<int> DecidedIds
        {
            get { return new List<int>(_decidedIds); }
        }
        public string IdentityName { get { return _defendant.FullName; } }
        public string IdentityCase
        {
            get { return string.Format(Strings.CaseFormat, _defendant.CaseNumber, _defendant.CaseYear); }
        }

        private string CourtName(int? id)
            {
                if (!id.HasValue)
                {
                    return "";
                }
                foreach (CourtRow c in _lookups.Courts(false))
                {
                    if (c.Id == id.Value)
                    {
                        return c.Name;
                    }
                }
                return "";
            }

        private string CircuitName(int? id)
            {
                if (!id.HasValue)
                {
                    return "";
                }
                foreach (CourtRow c in _lookups.Courts(false))
                {
                    foreach (CircuitRow r in _lookups.Circuits(c.Id, false))
                    {
                        if (r.Id == id.Value)
                        {
                            return r.Name;
                        }
                    }
                }
                return "";
            }

        public string IdentityCourt
        {
            get
            {
                string court = CourtName(_defendant.CourtId);
                string circuit = CircuitName(_defendant.CircuitId);
                if (circuit.Length == 0)
                {
                    return court;
                }
                return court.Length == 0 ? circuit : court + " / " + circuit;
            }
        }

        public string IdentitySession
        {
            get
            {
                if (_defendant.Status == DefendantStatuses.Detained && _defendant.SessionDate.HasValue)
                {
                    return string.Format(Strings.WizSessionHeld,
                        ArabicDateFormat.FormatShort(_defendant.SessionDate.Value));
                }
                return string.Format(Strings.WizSessionHeld, ArabicDateFormat.FormatShort(_clock.Today));
            }
        }

        public int Step
        {
            get { return _step; }
        }

        public string StepText
        {
            get { return string.Format(Strings.WizStep, _step, 3); }
        }

        public bool AtStep1 { get { return _step == 1; } }
        public bool AtStep2 { get { return _step == 2; } }
        public bool AtStep3 { get { return _step == 3; } }
        public bool Done { get { return _done; } }
        public bool Saving { get { return _saving; } }

        public string Code
        {
            get { return _code; }
        }

        public string CodeLabel
        {
            get { return LabelOf(_code); }
        }

        public LocalDate? NextDate
        {
            get { return _nextDate; }
            set
            {
                if (Set(ref _nextDate, value, "NextDate"))
                {
                    EvaluateWarnings();
                    Raise("ChosenLine");
                    Raise("SelectedOffsetDays");
                    Raise("Review");
                }
            }
        }

        public string ChosenLine
        {
            get
            {
                if (!NextDate.HasValue)
                {
                    return "";
                }
                int days = LocalDate.DaysBetween(HeldDate(), NextDate.Value);
                return string.Format(Strings.WizChosen,
                    ArabicDateFormat.FormatLong(NextDate.Value, Strings.Weekdays, Strings.Months), days);
            }
        }

        public string Reason
        {
            get { return _reason; }
            set { Set(ref _reason, value, "Reason"); }
        }

        public string Note
        {
            get { return _note; }
            set
            {
                if (Set(ref _note, value, "Note"))
                {
                    BuildReview();
                }
            }
        }

        public string Bail
        {
            get { return _bail; }
            set
            {
                if (Set(ref _bail, value, "Bail"))
                {
                    BuildReview();
                }
            }
        }

        public bool ConfirmPast
        {
            get { return _confirmPast; }
            set { Set(ref _confirmPast, value, "ConfirmPast"); }
        }

        public bool ConfirmWeekend
        {
            get { return _confirmWeekend; }
            set { Set(ref _confirmWeekend, value, "ConfirmWeekend"); }
        }

        public bool ConfirmFar
        {
            get { return _confirmFar; }
            set { Set(ref _confirmFar, value, "ConfirmFar"); }
        }

        public bool ShowPastWarning { get; private set; }
        public bool ShowWeekendWarning { get; private set; }
        public bool ShowFarWarning { get; private set; }

        public string Error
        {
            get { return _error; }
        }

        public bool HasError
        {
            get { return _error.Length > 0; }
        }

        public List<ReasonRow> Reasons { get; private set; }

        public string ReviewSentence
        {
            get { return Review.Count == 0 ? "" : Review[0].Sentence; }
        }

        public bool NeedsNextDate
        {
            get { return DecisionCodes.RequiresNextDate(_code); }
        }

        public bool NeedsReason
        {
            get { return DecisionCodes.AllowsReason(_code); }
        }

        public bool NeedsBail { get { return _code == DecisionCodes.ReleaseBail; } }
        public bool NeedsNote { get { return _code == DecisionCodes.Release || _code == DecisionCodes.Judgment; } }
        public bool NeedsCourtChange { get { return false; } }

        private LocalDate HeldDate()
        {
            if (_defendant.Status == DefendantStatuses.Detained && _defendant.SessionDate.HasValue)
            {
                return _defendant.SessionDate.Value;
            }
            return _clock.Today;
        }

        public List<LocalDate> AvailableOffsets()
        {
            var outp = new List<LocalDate>();
            foreach (OffsetButton b in _offsets)
            {
                outp.Add(HeldDate().AddDays(b.Days));
            }
            return outp;
        }

        public void PickOffset(int days)
        {
            NextDate = HeldDate().AddDays(days);
        }

        public int SelectedOffsetDays
        {
            get
            {
                if (!NextDate.HasValue)
                {
                    return -1;
                }
                int days = LocalDate.DaysBetween(HeldDate(), NextDate.Value);
                foreach (OffsetButton b in _offsets)
                {
                    if (b.Days == days)
                    {
                        return days;
                    }
                }
                return -1;
            }
        }

        public void PickDate(LocalDate d)
        {
            NextDate = d;
        }

        public void Choose(string code)
        {
            _code = code;
            foreach (DecisionTile t in _tiles)
            {
                t.Selected = t.Code == code;
            }
            _step = 2;
            Reasons = _code == DecisionCodes.RenewDetention || _code == DecisionCodes.Postpone
                ? _lookups.Reasons(true)
                : new List<ReasonRow>();
            if (DecisionCodes.RequiresNextDate(code))
            {
                NextDate = HeldDate().AddDays(7);
            }
            Raise("Step");
            Raise("StepText");
            Raise("AtStep1");
            Raise("AtStep2");
            Raise("AtStep3");
            Raise("NeedsNextDate");
            Raise("NeedsReason");
            Raise("NeedsBail");
            Raise("NeedsNote");
            Raise("Reasons");
            EvaluateWarnings();
            BuildReview();
        }

        private void GoNext()
        {
            if (_step == 2)
            {
                if (NeedsNextDate && !NextDate.HasValue)
                {
                    _error = Strings.ErrNextRequired;
                    Raise("Error");
                    Raise("HasError");
                    return;
                }
            }
            _error = "";
            Raise("Error");
            Raise("HasError");
            _step = _step + 1;
            Raise("Step");
            Raise("StepText");
            Raise("AtStep1");
            Raise("AtStep2");
            Raise("AtStep3");
        }

        private void GoBack()
        {
            _step = _step - 1;
            if (_step < 1)
            {
                _step = 1;
            }
            Raise("Step");
            Raise("StepText");
            Raise("AtStep1");
            Raise("AtStep2");
            Raise("AtStep3");
        }

        private void EvaluateWarnings()
        {
            ShowPastWarning = false;
            ShowWeekendWarning = false;
            ShowFarWarning = false;
            if (NextDate.HasValue && NeedsNextDate)
            {
                LocalDate next = NextDate.Value;
                LocalDate held = HeldDate();
                if (next < _clock.Today)
                {
                    ShowPastWarning = true;
                }
                if (next.IsWeekend)
                {
                    ShowWeekendWarning = true;
                }
                if (LocalDate.DaysBetween(held, next) > DecisionRules.FarDays
                    && LocalDate.DaysBetween(held, next) <= DecisionRules.MaxDays)
                {
                    ShowFarWarning = true;
                }
            }
            Raise("ShowPastWarning");
            Raise("ShowWeekendWarning");
            Raise("ShowFarWarning");
        }

        private void BuildReview()
        {
            _review.Clear();
            if (string.IsNullOrEmpty(_code))
            {
                return;
            }
            LocalDate? next = NextDate;
            string nextText = next.HasValue
                ? ArabicDateFormat.FormatLong(next.Value, Strings.Weekdays, Strings.Months)
                : "";
            string sentence;
            switch (_code)
            {
                case DecisionCodes.RenewDetention:
                    sentence = string.Format(Strings.WizSentenceRenew, IdentityName, IdentityCase, nextText);
                    break;
                case DecisionCodes.Postpone:
                    sentence = string.Format(Strings.WizSentencePostpone, IdentityName, IdentityCase, nextText);
                    break;
                case DecisionCodes.Release:
                    sentence = string.Format(Strings.WizSentenceRelease, IdentityName, IdentityCase);
                    break;
                case DecisionCodes.ReleaseBail:
                    int? bail = ParseBail();
                    sentence = bail.HasValue
                        ? string.Format(Strings.WizSentenceBail, IdentityName, IdentityCase, bail.Value)
                        : string.Format(Strings.WizSentenceBailNoMoney, IdentityName, IdentityCase);
                    break;
                case DecisionCodes.Judgment:
                    sentence = string.Format(Strings.WizSentenceJudgment, IdentityName, IdentityCase);
                    break;
                case DecisionCodes.Reopen:
                    sentence = string.Format(Strings.WizSentenceReopen, IdentityName, IdentityCase, nextText);
                    break;
                default:
                    sentence = "";
                    break;
            }
            _review.Add(new WizardStage { Sentence = sentence });
            _review.Add(new WizardStage { Number = Strings.GridName, Value = IdentityName });
            _review.Add(new WizardStage { Number = Strings.GridCase, Value = IdentityCase });
            if (next.HasValue)
            {
                _review.Add(new WizardStage
                {
                    Number = Strings.WizNextDateLine.Replace("{0}", ""),
                    Value = ArabicDateFormat.FormatShort(next.Value)
                });
            }
            if (!string.IsNullOrEmpty(Note))
            {
                _review.Add(new WizardStage { Number = Strings.WizNote, Value = Note });
            }
            Raise("Review");
            Raise("ReviewSentence");
        }

        private int? ParseBail()
        {
            string raw = DefendantEditViewModel.WesternDigits(Bail == null ? "" : Bail.Trim());
            if (raw.Length == 0)
            {
                return null;
            }
            int n;
            if (!int.TryParse(raw, out n))
            {
                return null;
            }
            return n;
        }

        public void Save()
        {
            if (_saving)
            {
                return;
            }
            BuildReview();
            _saving = true;
            Raise("Saving");
            var req = new DecisionRequest
            {
                RequestUuid = Guid.NewGuid().ToString("N"),
                DefendantId = _defendant.Id,
                ExpectedVersion = _version,
                Code = _code,
                NextDate = NextDate,
                ReasonText = Reason,
                Note = Note,
                BailAmount = ParseBail(),
                CourtId = _defendant.CourtId,
                CircuitId = _defendant.CircuitId,
                ConfirmPast = ConfirmPast,
                ConfirmWeekend = ConfirmWeekend,
                ConfirmFar = ConfirmFar
            };
            try
            {
                DecideResult r = _decisions.Decide(req);
                _saving = false;
                _done = true;
                _decidedIds.Add(_defendant.Id);
                Raise("Saving");
                Raise("Done");
                Action<Sentence> h = Saved;
                if (h != null)
                {
                    h(new Sentence(SuccessSentence(r)));
                }
            }
            catch (DecisionValidationException e)
            {
                _saving = false;
                Raise("Saving");
                _error = MessageOf(e.Result);
                Raise("Error");
                Raise("HasError");
            }
            catch (StaleRecordException)
            {
                _saving = false;
                Raise("Saving");
                _error = Strings.ErrStale;
                Raise("Error");
                Raise("HasError");
            }
        }

        private string SuccessSentence(DecideResult r)
        {
            if (r.NextDate.HasValue)
            {
                return string.Format(Strings.WizNextDateLine,
                    ArabicDateFormat.FormatLong(r.NextDate.Value, Strings.Weekdays, Strings.Months));
            }
            if (r.After != null && r.After.Status == DefendantStatuses.Released)
            {
                return string.Format(Strings.WizMovedTo, Strings.StatusReleased);
            }
            if (r.After != null && r.After.Status == DefendantStatuses.Judged)
            {
                return string.Format(Strings.WizMovedTo, Strings.FilterJudged);
            }
            return Strings.WizDone;
        }

        public static string MessageOf(ValidationResult vr)
        {
            foreach (string e in vr.Errors)
            {
                switch (e)
                {
                    case ValidationErrors.NextRequired:
                        return Strings.ErrNextRequired;
                    case ValidationErrors.NextAfter:
                        return Strings.ErrNextAfter;
                    case ValidationErrors.NoSession:
                        return Strings.ErrNoSession;
                    case ValidationErrors.FutureSession:
                        return Strings.ErrFutureSession;
                    case ValidationErrors.Stale:
                        return Strings.ErrStale;
                    case ValidationErrors.Bail:
                        return Strings.ValCaseNumber;
                    case ValidationErrors.CircuitCourt:
                        return Strings.ValCircuitCourt;
                }
            }
            return Strings.ErrGeneric.Replace("{0}", "E-DB-017");
        }

        public static string LabelOf(string code)
        {
            switch (code)
            {
                case DecisionCodes.RenewDetention: return Strings.DecRenew;
                case DecisionCodes.Postpone: return Strings.DecPostpone;
                case DecisionCodes.Release: return Strings.DecRelease;
                case DecisionCodes.ReleaseBail: return Strings.DecBail;
                case DecisionCodes.Judgment: return Strings.DecJudgment;
                case DecisionCodes.Reopen: return Strings.DecReopen;
                default: return "";
            }
        }

        private void LoadDefendant()
        {
            _defendant = _defendants.Get(_queue.Count > 0 ? _queue[_at] : 0);
            _version = _defendant.RowVersion;
            _tiles.Clear();
            if (_defendant.Status == DefendantStatuses.Detained)
            {
                _tiles.Add(new DecisionTile(DecisionCodes.RenewDetention, Strings.DecRenew));
                _tiles.Add(new DecisionTile(DecisionCodes.Postpone, Strings.DecPostpone));
                _tiles.Add(new DecisionTile(DecisionCodes.Release, Strings.DecRelease));
                _tiles.Add(new DecisionTile(DecisionCodes.ReleaseBail, Strings.DecBail));
                _tiles.Add(new DecisionTile(DecisionCodes.Judgment, Strings.DecJudgment));
            }
            else
            {
                _tiles.Add(new DecisionTile(DecisionCodes.Reopen, Strings.DecReopen));
            }
            Raise("Tiles");
            Raise("IdentityName");
            Raise("IdentityCase");
            Raise("IdentityCourt");
            Raise("IdentitySession");
            Raise("DefendantId");
            Raise("InQueue");
            Raise("QueueProgress");
        }

        public string PreCheckError
        {
            get
            {
                if (_defendant.Status != DefendantStatuses.Detained)
                {
                    return null;
                }
                if (!_defendant.SessionDate.HasValue)
                {
                    return Strings.ErrNoSession;
                }
                if (_defendant.SessionDate.Value > _clock.Today)
                {
                    return string.Format(Strings.ErrFutureSession,
                        ArabicDateFormat.FormatShort(_defendant.SessionDate.Value));
                }
                return null;
            }
        }
    }
}
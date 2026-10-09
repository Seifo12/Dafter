using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;

namespace CourtDesk.UI.ViewModels
{
    public sealed class SessionCard : ObservableObject
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Line2 { get; set; }
        public string Line3 { get; set; }
        public string ResultSentence { get; set; }
        public string AccentKey { get; set; }
        public System.Windows.Media.Brush Accent { get; set; }
        public long EventId { get; set; }
        public bool ShowUndo { get; set; }
    }

    public sealed class TodaySidebarViewModel : ObservableObject
    {
        private readonly TodayService _today;
        private string _dateLong = "";
        private string _dateLineText = "";
        private string _progress = "";
        private double _progressValue;
        private string _tomorrow = "";
        private bool _hasOverdue;
        private string _overdueHeader = "";
        private bool _empty = true;
        private string _tab = "today";
        private string _todayTabText = "";
        private string _overdueTabText = "";
        private readonly ObservableCollection<SessionCard> _overdue = new ObservableCollection<SessionCard>();
        private readonly ObservableCollection<SessionCard> _pending = new ObservableCollection<SessionCard>();
        private readonly ObservableCollection<SessionCard> _decided = new ObservableCollection<SessionCard>();
        private readonly ObservableCollection<SessionCard> _tomorrowCards = new ObservableCollection<SessionCard>();
        private readonly ObservableCollection<SessionCard> _decidedAll = new ObservableCollection<SessionCard>();
        private int _moreDecided;
        private bool _decidedExpanded;

        public TodaySidebarViewModel(TodayService today)
        {
            _today = today;
            RecordCommand = new RelayCommand(delegate(object o) { OnRecord(o); });
            UndoCommand = new RelayCommand(delegate(object o) { OnUndo(o); });
            QueueCommand = new RelayCommand(delegate(object o) { OnQueue(); });
            PrintCommand = new RelayCommand(delegate(object o) { OnPrint(false); });
            PreviewCommand = new RelayCommand(delegate(object o) { OnPrint(true); });
            CardCommand = new RelayCommand(delegate(object o) { OnCard(o); });
            TabCommand = new RelayCommand(delegate(object o)
            {
                string key = o as string;
                if (!string.IsNullOrEmpty(key))
                {
                    Tab = key;
                    Raise("TomorrowTabText");
                    Raise("TodayTabText");
                    Raise("OverdueTabText");
                }
            });
                        ToggleDecidedCommand = new RelayCommand(delegate(object o)
            {
                DecidedExpanded = !DecidedExpanded;
            });
            TodaySnapshot snap0 = _today.Snapshot();
            _scopeDate = snap0.Date;
            Refresh();
        }

        public ObservableCollection<SessionCard> OverdueCards { get { return _overdue; } }
        public ObservableCollection<SessionCard> PendingCards { get { return _pending; } }
        public ObservableCollection<SessionCard> DecidedCards { get { return _decided; } }
        public ObservableCollection<SessionCard> TomorrowCards { get { return _tomorrowCards; } }
        private string _printScope = "ALL";

        private CourtDesk.Domain.LocalDate _scopeDate;

        public CourtDesk.Domain.LocalDate ScopeDate
        {
            get { return _scopeDate; }
            set { Set(ref _scopeDate, value, "ScopeDate"); }
        }

        public void ShiftScopeDate(int days)
        {
            ScopeDate = _scopeDate.AddDays(days);
        }

        public string PrintScope
        {
            get { return _printScope; }
            set { Set(ref _printScope, value ?? "ALL", "PrintScope"); }
        }

        public ICommand RecordCommand { get; private set; }
        public ICommand UndoCommand { get; private set; }
        public ICommand QueueCommand { get; private set; }
        public ICommand PrintCommand { get; private set; }
        public ICommand PreviewCommand { get; private set; }
        public ICommand CardCommand { get; private set; }
        public ICommand TabCommand { get; private set; }
        public ICommand ToggleDecidedCommand { get; private set; }

        public bool DecidedExpanded
        {
            get { return _decidedExpanded; }
            set
            {
                if (_decidedExpanded == value)
                {
                    return;
                }
                _decidedExpanded = value;
                ApplyDecidedCollapse();
            }
        }

        public bool HasMoreDecided
        {
            get { return _decidedAll.Count > 5; }
        }

        public string DecidedToggleText
        {
            get
            {
                return _decidedExpanded
                    ? CourtDesk.Resources.Strings.SideShowLess
                    : CourtDesk.Resources.Strings.SideShowAll;
            }
        }

        public string Tab
        {
            get { return _tab; }
            set
            {
                Set(ref _tab, value ?? "today", "Tab");
            }
        }

        public string TodayTabText
        {
            get { return _todayTabText; }
        }

        public string DateLineText
        {
            get { return _dateLineText; }
        }

        public string OverdueTabText
        {
            get { return _overdueTabText; }
        }

        public string TomorrowTabText
        {
            get
            {
                return string.Format(CourtDesk.Resources.Strings.SideTabTomorrow,
                    _tomorrowCards.Count);
            }
        }

        public event Action<SessionCard> RecordRequested;
        public event Action<SessionCard> UndoRequested;
        public event Action QueueRequested;
        public event Action<bool> PrintRequested;
        public event Action<SessionCard> CardRequested;
        public event Action<bool, string, CourtDesk.Domain.LocalDate> PrintScopeRequested;

        public string DateLong
        {
            get { return _dateLong; }
        }

        public string Progress
        {
            get { return _progress; }
        }

        public double ProgressValue
        {
            get { return _progressValue; }
        }

        public string Tomorrow
        {
            get { return _tomorrow; }
        }

        public bool HasOverdue
        {
            get { return _hasOverdue; }
        }

        public string OverdueHeader
        {
            get { return _overdueHeader; }
        }

        public bool Empty
        {
            get { return _empty; }
        }

        public void Refresh()
        {
            TodaySnapshot snap = _today.Snapshot();
            _dateLong = ArabicDateFormat.FormatLong(snap.Date,
                CourtDesk.Resources.Strings.Weekdays, CourtDesk.Resources.Strings.Months);
            _progress = string.Format(CourtDesk.Resources.Strings.SideProgress, snap.DecidedCount, snap.Total);
            _progressValue = snap.Total == 0 ? 0 : (double)snap.DecidedCount * 100.0 / snap.Total;
            _tomorrow = string.Format(CourtDesk.Resources.Strings.SideTomorrow, snap.TomorrowCount);
            _hasOverdue = snap.Overdue.Count > 0;
            _overdueHeader = string.Format(CourtDesk.Resources.Strings.SideOverdue, snap.Overdue.Count);
            _empty = snap.Total == 0 && snap.Overdue.Count == 0;

            _overdue.Clear();
            foreach (DefendantRow row in snap.Overdue)
            {
                int days = LocalDate.DaysBetween(row.D.SessionDate.Value, snap.Date);
                _overdue.Add(new SessionCard
                {
                    Id = row.D.Id,
                    Name = row.D.FullName,
                    Line2 = string.Format(CourtDesk.Resources.Strings.CaseFormat, row.D.CaseNumber, row.D.CaseYear),
                    Line3 = string.Format(CourtDesk.Resources.Strings.OverdueDays, days),
                    AccentKey = "OVERDUE"
                });
            }
            _pending.Clear();
            foreach (DefendantRow row in snap.Pending)
            {
                _pending.Add(new SessionCard
                {
                    Id = row.D.Id,
                    Name = row.D.FullName,
                    Line2 = string.Format(CourtDesk.Resources.Strings.CaseFormat, row.D.CaseNumber, row.D.CaseYear)
                        + " \u00b7 " + (row.CircuitName ?? ""),
                    Line3 = row.CourtName ?? "",
                    AccentKey = "TODAY"
                });
            }
            _tomorrowCards.Clear();
            foreach (DefendantRow row in snap.Tomorrow)
            {
                _tomorrowCards.Add(new SessionCard
                {
                    Id = row.D.Id,
                    Name = row.D.FullName,
                    Line2 = string.Format(CourtDesk.Resources.Strings.CaseFormat,
                        row.D.CaseNumber, row.D.CaseYear)
                        + " \u00b7 " + (row.CircuitName ?? ""),
                    Line3 = row.CourtName ?? "",
                    AccentKey = "UPCOMING"
                });
            }
            _decidedAll.Clear();
            foreach (DecidedRow row in snap.Decided)
            {
                _decidedAll.Add(new SessionCard
                {
                    Id = row.D.Id,
                    Name = row.D.FullName,
                    Line2 = LabelOf(row.DecisionCode),
                    Line3 = row.NextSessionDate ?? "",
                    ResultSentence = LabelOf(row.DecisionCode) + " \u2192 " + (row.NextSessionDate ?? ""),
                    AccentKey = AccentFor(row.DecisionCode),
                    Accent = AccentBrush(AccentFor(row.DecisionCode)),
                    EventId = row.EventId,
                    ShowUndo = true
                });
            }
            ApplyDecidedCollapse();
            _todayTabText = string.Format(CourtDesk.Resources.Strings.SideTabToday, _pending.Count);
            _overdueTabText = string.Format(
                CourtDesk.Resources.Strings.SideTabOverdue, _overdue.Count);
            _dateLineText = _dateLong + " \u00b7 " + string.Format(
                CourtDesk.Resources.Strings.TomorrowShort, snap.TomorrowCount);
            Raise("DateLong");
            Raise("DateLineText");
            Raise("Progress");
            Raise("ProgressValue");
            Raise("Tomorrow");
            Raise("HasOverdue");
            Raise("OverdueHeader");
            Raise("Empty");
            Raise("TodayTabText");
            Raise("OverdueTabText");
            Raise("TomorrowTabText");
            Raise("MoreDecided");
            Raise("MoreDecidedText");
            Raise("HasDecided");
            Raise("HasMoreDecided");
            Raise("DecidedToggleText");
            Raise("DecidedExpanded");
            if (_pending.Count == 0 && _overdue.Count > 0)
            {
                Set(ref _tab, "overdue", "Tab");
            }
            if (_overdue.Count == 0 && _tab == "overdue")
            {
                Set(ref _tab, "today", "Tab");
            }
        }

        public static string LabelOf(string code)
        {
            if (code == DecisionCodes.RenewDetention)
            {
                return CourtDesk.Resources.Strings.DecRenew;
            }
            if (code == DecisionCodes.Postpone)
            {
                return CourtDesk.Resources.Strings.DecPostpone;
            }
            if (code == DecisionCodes.Release)
            {
                return CourtDesk.Resources.Strings.DecRelease;
            }
            if (code == DecisionCodes.ReleaseBail)
            {
                return CourtDesk.Resources.Strings.DecBail;
            }
            if (code == DecisionCodes.Judgment)
            {
                return CourtDesk.Resources.Strings.DecJudgment;
            }
            if (code == DecisionCodes.Reopen)
            {
                return CourtDesk.Resources.Strings.DecReopen;
            }
            return code ?? "";
        }

        public int MoreDecided
        {
            get { return _moreDecided; }
        }

        public string MoreDecidedText
        {
            get
            {
                return _moreDecided > 0
                    ? string.Format(CourtDesk.Resources.Strings.SideMoreDecidedPlus, _moreDecided)
                    : "";
            }
        }

        private void ApplyDecidedCollapse()
        {
            int limit = _decidedExpanded ? int.MaxValue : 5;
            _decided.Clear();
            int shown = 0;
            foreach (SessionCard c in _decidedAll)
            {
                if (shown >= limit)
                {
                    break;
                }
                _decided.Add(c);
                shown++;
            }
            _moreDecided = Math.Max(0, _decidedAll.Count - shown);
            Raise("MoreDecided");
            Raise("MoreDecidedText");
            Raise("HasDecided");
            Raise("HasMoreDecided");
            Raise("DecidedToggleText");
            Raise("DecidedExpanded");
        }

        private static System.Windows.Media.Brush AccentBrush(string key)
        {
            switch (key)
            {
                case "RELEASED": return CourtDesk.UI.UiRes.Brush(null, "ReleasedFgBrush", "#1B6E2D");
                case "JUDGED": return CourtDesk.UI.UiRes.Brush(null, "JudgedFgBrush", "#5B3FA3");
                case "TODAY": return CourtDesk.UI.UiRes.Brush(null, "TodayFgBrush", "#8A5A00");
                case "OVERDUE": return CourtDesk.UI.UiRes.Brush(null, "OverdueFgBrush", "#A32D2D");
                default: return CourtDesk.UI.UiRes.Brush(null, "UpcomingFgBrush", "#0B5CAD");
            }
        }

        private static string AccentFor(string code)
        {
            if (code == DecisionCodes.Release || code == DecisionCodes.ReleaseBail)
            {
                return "RELEASED";
            }
            if (code == DecisionCodes.Judgment)
            {
                return "JUDGED";
            }
            return "UPCOMING";
        }

        private void OnRecord(object o)
        {
            SessionCard card = o as SessionCard;
            if (card == null)
            {
                return;
            }
            Action<SessionCard> h = RecordRequested;
            if (h != null)
            {
                h(card);
            }
        }

        private void OnUndo(object o)
        {
            SessionCard card = o as SessionCard;
            if (card == null)
            {
                return;
            }
            Action<SessionCard> h = UndoRequested;
            if (h != null)
            {
                h(card);
            }
        }

        private void OnQueue()
        {
            Action h = QueueRequested;
            if (h != null)
            {
                h();
            }
        }

        private void OnPrint(bool preview)
        {
            Action<bool, string, CourtDesk.Domain.LocalDate> scoped = PrintScopeRequested;
            if (scoped != null)
            {
                scoped(preview, _printScope, _scopeDate);
                return;
            }
            Action<bool> h = PrintRequested;
            if (h != null)
            {
                h(preview);
            }
        }

        private void OnCard(object o)
        {
            SessionCard card = o as SessionCard;
            if (card == null)
            {
                return;
            }
            Action<SessionCard> h = CardRequested;
            if (h != null)
            {
                h(card);
            }
        }
    }
}

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
        private readonly ObservableCollection<SessionCard> _decidedAll = new ObservableCollection<SessionCard>();
        private int _moreDecided;

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
                }
            });
            Refresh();
        }

        public ObservableCollection<SessionCard> OverdueCards { get { return _overdue; } }
        public ObservableCollection<SessionCard> PendingCards { get { return _pending; } }
        public ObservableCollection<SessionCard> DecidedCards { get { return _decided; } }
        public ICommand RecordCommand { get; private set; }
        public ICommand UndoCommand { get; private set; }
        public ICommand QueueCommand { get; private set; }
        public ICommand PrintCommand { get; private set; }
        public ICommand PreviewCommand { get; private set; }
        public ICommand CardCommand { get; private set; }
        public ICommand TabCommand { get; private set; }

        public string Tab
        {
            get { return _tab; }
            set
            {
                if (Set(ref _tab, value ?? "today", "Tab"))
                {
                    Raise("TodayTabText");
                    Raise("OverdueTabText");
                    RefreshLists();
                }
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

        public event Action<SessionCard> RecordRequested;
        public event Action<SessionCard> UndoRequested;
        public event Action QueueRequested;
        public event Action<bool> PrintRequested;
        public event Action<SessionCard> CardRequested;

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
                        + " · " + (row.CircuitName ?? ""),
                    Line3 = row.CourtName ?? "",
                    AccentKey = "TODAY"
                });
            }
            _decidedAll.Clear();
            foreach (DecidedRow row in snap.Decided)
            {
                _decidedAll.Add(new SessionCard
                {
                    Id = row.D.Id,
                    Name = row.D.FullName,
                    Line2 = row.DecisionCode,
                    Line3 = row.NextSessionDate ?? "",
                    ResultSentence = row.DecisionCode + " ← " + (row.NextSessionDate ?? ""),
                    AccentKey = AccentFor(row.DecisionCode),
                    Accent = AccentBrush(AccentFor(row.DecisionCode)),
                    EventId = row.EventId,
                    ShowUndo = true
                });
            }
            ApplyDecidedCollapse();
            _dateLineText = _dateLong + " · " + string.Format(CourtDesk.Resources.Strings.TomorrowShort, snap.TomorrowCount);
            Raise("DateLong");
            Raise("DateLineText");
            Raise("Progress");
            Raise("ProgressValue");
            Raise("Tomorrow");
            Raise("HasOverdue");
            Raise("OverdueHeader");
            Raise("Empty");
            RefreshLists();
        }

        private void RefreshLists()
        {
            _todayTabText = string.Format(CourtDesk.Resources.Strings.SideTabToday, _pending.Count);
            _overdueTabText = string.Format(CourtDesk.Resources.Strings.SideTabOverdue, _overdue.Count);
            if (_pending.Count == 0 && _overdue.Count > 0 && _tab == "today")
            {
                Tab = "overdue";
                return;
            }
            if (_overdue.Count == 0 && _tab == "overdue")
            {
                Tab = "today";
            }
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
                    ? string.Format(CourtDesk.Resources.Strings.SideMoreDecided, _moreDecided)
                    : "";
            }
        }

        private void ApplyDecidedCollapse()
        {
            int limit = 5;
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

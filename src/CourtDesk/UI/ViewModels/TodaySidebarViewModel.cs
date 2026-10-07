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
        public long EventId { get; set; }
        public bool ShowUndo { get; set; }
    }

    public sealed class TodaySidebarViewModel : ObservableObject
    {
        private readonly TodayService _today;
        private string _dateLong = "";
        private string _progress = "";
        private double _progressValue;
        private string _tomorrow = "";
        private bool _hasOverdue;
        private string _overdueHeader = "";
        private bool _empty = true;
        private readonly ObservableCollection<SessionCard> _overdue = new ObservableCollection<SessionCard>();
        private readonly ObservableCollection<SessionCard> _pending = new ObservableCollection<SessionCard>();
        private readonly ObservableCollection<SessionCard> _decided = new ObservableCollection<SessionCard>();

        public TodaySidebarViewModel(TodayService today)
        {
            _today = today;
            RecordCommand = new RelayCommand(delegate(object o) { OnRecord(o); });
            UndoCommand = new RelayCommand(delegate(object o) { OnUndo(o); });
            QueueCommand = new RelayCommand(delegate(object o) { OnQueue(); });
            PrintCommand = new RelayCommand(delegate(object o) { OnPrint(false); });
            PreviewCommand = new RelayCommand(delegate(object o) { OnPrint(true); });
            CardCommand = new RelayCommand(delegate(object o) { OnCard(o); });
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
                int days = LocalDate.DaysBetween(
                    row.D.SessionDate.Value, snap.Date);
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
            _decided.Clear();
            foreach (DecidedRow row in snap.Decided)
            {
                _decided.Add(new SessionCard
                {
                    Id = row.D.Id,
                    Name = row.D.FullName,
                    Line2 = row.DecisionCode,
                    Line3 = row.NextSessionDate ?? "",
                    ResultSentence = row.DecisionCode + " ← " + (row.NextSessionDate ?? ""),
                    AccentKey = AccentFor(row.DecisionCode),
                    EventId = row.EventId,
                    ShowUndo = true
                });
            }
            Raise("DateLong");
            Raise("Progress");
            Raise("ProgressValue");
            Raise("Tomorrow");
            Raise("HasOverdue");
            Raise("OverdueHeader");
            Raise("Empty");
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

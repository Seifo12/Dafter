using System;
using System.Collections.Generic;
using System.Windows.Input;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;
using CourtDesk.Resources;

namespace CourtDesk.UI.ViewModels
{
    public sealed class DayCountRow
        {
            public LocalDate Date { get; set; }
            public string Text { get; set; }
            public int Count { get; set; }
            public bool IsSelected { get; set; }
        }

    public sealed class ScheduleViewModel : ObservableObject
    {
        private readonly ScheduleService _schedule;
        private readonly IClock _clock;
        private LocalDate _selected;
        private readonly List<DefendantRow> _day = new List<DefendantRow>();
        private readonly List<DayCountRow> _upcoming = new List<DayCountRow>();

        public ScheduleViewModel(ScheduleService schedule, IClock clock)
        {
            _schedule = schedule;
            _clock = clock;
            _selected = clock.Today;
            PrevWeekCommand = new RelayCommand(delegate { Pick(_selected.AddDays(-7)); });
            NextWeekCommand = new RelayCommand(delegate { Pick(_selected.AddDays(7)); });
            TodayCommand = new RelayCommand(delegate { Pick(clock.Today); });
            TomorrowCommand = new RelayCommand(delegate { Pick(clock.Today.AddDays(1)); });
            PrintCommand = new RelayCommand(delegate
            {
                Action<LocalDate> h = PrintRequested;
                if (h != null)
                {
                    h(_selected);
                }
            });
            EditCommand = new RelayCommand(delegate(object o)
            {
                var row = o as DefendantRow;
                if (row != null)
                {
                    Action<int> h = EditRequested;
                    if (h != null)
                    {
                        h(row.D.Id);
                    }
                }
            });
            Refresh();
        }

        public ICommand PrevWeekCommand { get; private set; }
        public ICommand NextWeekCommand { get; private set; }
        public ICommand TodayCommand { get; private set; }
        public ICommand TomorrowCommand { get; private set; }
        public ICommand PrintCommand { get; private set; }
        public ICommand EditCommand { get; private set; }

        public event Action<LocalDate> PrintRequested;
        public event Action<int> EditRequested;

        public string Title { get { return Strings.NavSchedule; } }

        public LocalDate Selected
        {
            get { return _selected; }
        }

        public string SelectedText
        {
            get { return ArabicDateFormat.FormatLong(_selected, Strings.Weekdays, Strings.Months); }
        }

        public List<DefendantRow> Day { get { return _day; } }
        public List<DayCountRow> Upcoming { get { return _upcoming; } }

        public string EmptyText
        {
            get { return Day.Count == 0 ? Strings.SideEmpty : ""; }
        }

        public void Pick(LocalDate date)
        {
            _selected = date;
            Refresh();
        }

        public void Refresh()
        {
            _day.Clear();
            _day.AddRange(_schedule.Day(_selected));
            _upcoming.Clear();
            LocalDate today = _clock.Today;
            foreach (DateCount c in _schedule.NextDays(today, 14))
            {
                _upcoming.Add(new DayCountRow
                {
                    Date = c.Date,
                    Count = c.Count,
                    IsSelected = c.Date == _selected,
                    Text = Label(c.Date, today) + ": "
                        + ArabicDateFormat.FormatShort(c.Date) + " (" + c.Count + ")"
                });
            }
            Raise("Selected");
            Raise("SelectedText");
            Raise("Day");
            Raise("Upcoming");
            Raise("EmptyText");
        }

        private static string Label(LocalDate date, LocalDate today)
        {
            if (date == today)
            {
                return Strings.DateToday;
            }
            if (date == today.AddDays(1))
            {
                return Strings.DateTomorrow;
            }
            return Strings.Weekdays[ArabicDateFormat.WeekdayIndex(date.DayOfWeek)];
        }
    }
}
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CourtDesk.Domain;
using CourtDesk.UI.ViewModels;

namespace CourtDesk.UI.Controls
{
    public partial class BigCalendar : UserControl
    {
        private LocalDate _month = CalendarModel.FirstOfMonth(LocalDate.FromDateTime(DateTime.Today));
        private LocalDate _today;
        private LocalDate? _minimum;
        private Dictionary<string, int> _counts = new Dictionary<string, int>(StringComparer.Ordinal);

        public BigCalendar()
        {
            InitializeComponent();
            PickCommand = new RelayCommand(delegate(object o)
            {
                CalendarCell c = o as CalendarCell;
                if (c == null || !c.Date.HasValue || !c.Enabled)
                {
                    return;
                }
                Selected = c.Date.Value;
                DateSelected(Selected.Value);
            });
            DataContext = this;
        }

        public ICommand PickCommand { get; private set; }

        public event Action<LocalDate> DateSelected;

        public LocalDate? Selected { get; private set; }

        public LocalDate? Minimum
        {
            get { return _minimum; }
            set { _minimum = value; Rebuild(); }
        }

        public LocalDate Today
        {
            get { return _today; }
            set { _today = value; Rebuild(); }
        }

        public void SetCounts(Dictionary<string, int> counts)
        {
            _counts = counts ?? new Dictionary<string, int>(StringComparer.Ordinal);
            Rebuild();
        }

        public void ShowMonth(LocalDate month)
        {
            _month = CalendarModel.FirstOfMonth(month);
            Rebuild();
        }

        public LocalDate Month
        {
            get { return _month; }
        }

        private void PrevMonth(object sender, RoutedEventArgs e)
        {
            ShowMonth(CalendarModel.AddMonths(_month, -1));
        }

        private void NextMonth(object sender, RoutedEventArgs e)
        {
            ShowMonth(CalendarModel.AddMonths(_month, 1));
        }

        private void Rebuild()
        {
            if (Cells == null)
            {
                return;
            }
            Title.Text = CourtDesk.Resources.Strings.Months[_month.Month - 1] + " " + _month.Year;
            Cells.ItemsSource = CalendarModel.Build(_month, _today, Selected, _minimum, _counts);
        }

        public void Select(LocalDate? date)
        {
            Selected = date;
            if (date.HasValue && date.Value.Month != _month.Month)
            {
                ShowMonth(date.Value);
                return;
            }
            Rebuild();
        }
    }
}
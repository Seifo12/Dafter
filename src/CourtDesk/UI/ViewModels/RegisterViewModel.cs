using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;

namespace CourtDesk.UI.ViewModels
{
    public sealed class RegisterRow : ObservableObject
    {
        public int Id { get; set; }
        public int Number { get; set; }
        public string Name { get; set; }
        public string CaseText { get; set; }
        public string CourtText { get; set; }
        public string SessionText { get; set; }
        public string StateKey { get; set; }
        public string StateWord { get; set; }
        public string SessionLine1 { get; set; }
        public string SessionLine2 { get; set; }
        public System.Windows.Media.Brush StripeBrush { get; set; }
        public string ChargesText { get; set; }
        public string CustodyText { get; set; }
    }

    public sealed class FilterChipItem : ObservableObject
    {
        private bool _checked;

        public string Key { get; set; }
        public string Label { get; set; }
        public int Count { get; set; }

        public string Text
        {
            get { return Label + " " + Count.ToString(); }
        }

        public bool Checked
        {
            get { return _checked; }
            set { Set(ref _checked, value, "Checked"); }
        }
    }

    public sealed class RegisterViewModel : ObservableObject
    {
        private readonly SearchIndex _index;
        private readonly TodayService _today;
        private readonly Func<string> _filterSetting;
        private readonly Action<string> _saveFilter;
        private string _search = "";
        private string _filter = Filters.Detained;
        private RegisterRow _selected;
        private string _hint;
        private bool _hintVisible;
        private bool _wide;
        private string _sortColumn;
        private bool _sortAsc = true;
        private readonly ObservableCollection<RegisterRow> _rows = new ObservableCollection<RegisterRow>();
        private readonly ObservableCollection<FilterChipItem> _chips = new ObservableCollection<FilterChipItem>();
        private readonly System.Collections.Generic.List<SearchRow> _scratch = new System.Collections.Generic.List<SearchRow>();

        public RegisterViewModel(SearchIndex index, TodayService today,
            Func<string> filterSetting, Action<string> saveFilter)
        {
            _index = index;
            _today = today;
            _filterSetting = filterSetting;
            _saveFilter = saveFilter;
            string saved = filterSetting == null ? null : filterSetting();
            if (saved == Filters.All || saved == Filters.Released || saved == Filters.Judged || saved == Filters.NoDate)
            {
                _filter = saved;
            }
            SearchCommand = new RelayCommand(delegate(object o) { Refresh(); });
            ClearSearchCommand = new RelayCommand(delegate(object o) { SearchText = ""; });
            ChipCommand = new RelayCommand(delegate(object o)
            {
                string key = o as string;
                if (!string.IsNullOrEmpty(key))
                {
                    Filter = key;
                }
            });
            SortCommand = new RelayCommand(delegate(object o)
            {
                string col = o as string;
                if (string.IsNullOrEmpty(col))
                {
                    return;
                }
                if (_sortColumn == col)
                {
                    _sortAsc = !_sortAsc;
                }
                else
                {
                    _sortColumn = col;
                    _sortAsc = true;
                }
                Refresh();
            });
            WideCommand = new RelayCommand(delegate(object o) { Wide = !Wide; });
            RefreshChips();
            Refresh();
        }

        public ObservableCollection<RegisterRow> Rows { get { return _rows; } }
        public ObservableCollection<FilterChipItem> Chips { get { return _chips; } }
        public ICommand SearchCommand { get; private set; }
        public ICommand ClearSearchCommand { get; private set; }
        public ICommand ChipCommand { get; private set; }
        public ICommand SortCommand { get; private set; }
        public ICommand WideCommand { get; private set; }

        public string SearchText
        {
            get { return _search; }
            set
            {
                if (Set(ref _search, value ?? "", "SearchText"))
                {
                    Refresh();
                }
            }
        }

        public string Filter
        {
            get { return _filter; }
            set
            {
                if (value == null)
                {
                    return;
                }
                if (Set(ref _filter, value, "Filter"))
                {
                    if (_saveFilter != null)
                    {
                        _saveFilter(value);
                    }
                    RefreshChips();
                    Refresh();
                }
            }
        }

        public RegisterRow Selected
        {
            get { return _selected; }
            set { Set(ref _selected, value, "Selected"); HideHint(); }
        }

        public string Hint
        {
            get { return _hint; }
        }

        public bool HintVisible
        {
            get { return _hintVisible; }
        }

        public bool Wide
        {
            get { return _wide; }
            set
            {
                if (Set(ref _wide, value, "Wide"))
                {
                    Raise("WideOn");
                }
            }
        }

        public bool WideOn { get { return _wide; } }

        public void ShowHint(string text)
        {
            _hint = text;
            _hintVisible = true;
            Raise("Hint");
            Raise("HintVisible");
        }

        public void HideHint()
        {
            if (!_hintVisible)
            {
                return;
            }
            _hintVisible = false;
            Raise("HintVisible");
        }

        public void Refresh()
        {
            _index.Filter(_search, _filter, _scratch);
            _rows.Clear();
            int i = 0;
            foreach (SearchRow hit in _scratch)
            {
                i++;
                _rows.Add(ToRow(i, hit));
            }
            ApplySort();
            RefreshChips();
            Raise("EmptyResult");
            Raise("EmptyInFilter");
        }

        public bool EmptyResult
        {
            get { return _rows.Count == 0; }
        }

        public bool EmptyInFilter
        {
            get { return _rows.Count == 0 && _filter != Filters.All && _index.CountQueryInFilter(_search, Filters.All) > 0; }
        }

        public int AllCount
        {
            get { return _index.CountQueryInFilter(_search, Filters.All); }
        }

        private void RefreshChips()
        {
            _chips.Clear();
            AddChip(Filters.Detained, CourtDesk.Resources.Strings.FilterDetained);
            AddChip(Filters.All, CourtDesk.Resources.Strings.FilterAll);
            AddChip(Filters.Released, CourtDesk.Resources.Strings.FilterReleased);
            AddChip(Filters.Judged, CourtDesk.Resources.Strings.FilterJudged);
            AddChip(Filters.NoDate, CourtDesk.Resources.Strings.FilterNoDate);
            Raise("Chips");
        }

        private void AddChip(string key, string label)
        {
            _chips.Add(new FilterChipItem
            {
                Key = key,
                Label = label,
                Count = _index.CountFilter(key),
                Checked = _filter == key
            });
        }

        private void ApplySort()
        {
            if (string.IsNullOrEmpty(_sortColumn) || _rows.Count < 2)
            {
                return;
            }
            var list = new System.Collections.Generic.List<RegisterRow>(_rows);
            Comparison<RegisterRow> cmp = null;
            if (_sortColumn == "name")
            {
                cmp = delegate(RegisterRow a, RegisterRow b) { return string.CompareOrdinal(a.Name, b.Name); };
            }
            else if (_sortColumn == "case")
            {
                cmp = delegate(RegisterRow a, RegisterRow b) { return string.CompareOrdinal(a.CaseText, b.CaseText); };
            }
            else if (_sortColumn == "court")
            {
                cmp = delegate(RegisterRow a, RegisterRow b) { return string.CompareOrdinal(a.CourtText, b.CourtText); };
            }
            else if (_sortColumn == "session")
            {
                cmp = delegate(RegisterRow a, RegisterRow b) { return string.CompareOrdinal(a.SessionText, b.SessionText); };
            }
            else if (_sortColumn == "state")
            {
                cmp = delegate(RegisterRow a, RegisterRow b) { return string.CompareOrdinal(a.StateKey, b.StateKey); };
            }
            if (cmp == null)
            {
                return;
            }
            if (!_sortAsc)
            {
                Comparison<RegisterRow> inner = cmp;
                cmp = delegate(RegisterRow a, RegisterRow b) { return inner(b, a); };
            }
            list.Sort(cmp);
            _rows.Clear();
            foreach (RegisterRow r in list)
            {
                _rows.Add(r);
            }
        }

        private RegisterRow ToRow(int number, SearchRow hit)
        {
            Defendant d = hit.D;
            string session1 = "";
            string session2 = "";
            if (d.SessionDate.HasValue)
            {
                LocalDate s = d.SessionDate.Value;
                session1 = ArabicDateFormat.FormatShort(s);
                session2 = CourtDesk.Resources.Strings.Weekdays[(int)s.DayOfWeek];
            }
            string court = hit.Source.CourtName ?? "";
            if (!string.IsNullOrEmpty(hit.Source.CircuitName))
            {
                court = court.Length == 0 ? hit.Source.CircuitName : court + " / " + hit.Source.CircuitName;
            }
            if (court.Length == 0)
            {
                court = "—";
            }
            string caseText = string.Format(CourtDesk.Resources.Strings.CaseFormat, d.CaseNumber, d.CaseYear);
            return new RegisterRow
            {
                Id = d.Id,
                Number = number,
                Name = d.FullName,
                CaseText = caseText,
                CourtText = court,
                SessionText = session2,
                SessionLine1 = session1,
                SessionLine2 = session2,
                StateKey = hit.State,
                StateWord = StateWordOf(hit.State),
                StripeBrush = StripeOf(hit.State),
                ChargesText = d.Charges ?? "",
                CustodyText = (d.CustodyLocationId.HasValue || d.DetainedSince.HasValue)
                    ? CourtDesk.Resources.Strings.CustodyHeld
                    : CourtDesk.Resources.Strings.CustodyFree
            };
        }

        private System.Windows.Media.Brush StripeOf(string state)
        {
            System.Windows.Media.Brush b;
            switch (state)
            {
                case DisplayStates.Today: b = CourtDesk.UI.UiRes.Brush(null, "TodayFgBrush", "#8A5A00"); break;
                case DisplayStates.Overdue: b = CourtDesk.UI.UiRes.Brush(null, "OverdueFgBrush", "#A32D2D"); break;
                case DisplayStates.Upcoming: b = CourtDesk.UI.UiRes.Brush(null, "UpcomingFgBrush", "#0B5CAD"); break;
                case DisplayStates.Released: b = CourtDesk.UI.UiRes.Brush(null, "ReleasedFgBrush", "#1B6E2D"); break;
                case DisplayStates.Judged: b = CourtDesk.UI.UiRes.Brush(null, "JudgedFgBrush", "#5B3FA3"); break;
                default: b = CourtDesk.UI.UiRes.Brush(null, "NoDateFgBrush", "#46505E"); break;
            }
            return b;
        }

        private static string StateWordOf(string state)
        {
            switch (state)
            {
                case DisplayStates.Today: return CourtDesk.Resources.Strings.StateToday;
                case DisplayStates.Overdue: return CourtDesk.Resources.Strings.StateOverdue;
                case DisplayStates.Upcoming: return CourtDesk.Resources.Strings.StateUpcoming;
                case DisplayStates.Released: return CourtDesk.Resources.Strings.StatusReleased;
                case DisplayStates.Judged: return CourtDesk.Resources.Strings.StatusJudged;
                default: return CourtDesk.Resources.Strings.StateNoDate;
            }
        }
    }
}

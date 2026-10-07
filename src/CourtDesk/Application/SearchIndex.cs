using System;
using System.Collections.Generic;
using System.Text;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;

namespace CourtDesk.Application
{
    public sealed class SearchRow
    {
        public int Id;
        public Defendant D;
        public DefendantRow Source;
        public string Spaced;
        public string Joined;
        public string Filter;
        public string State;
        internal int _index;
    }

    public static class Filters
    {
        public const string Detained = "DETAINED";
        public const string All = "ALL";
        public const string Released = "RELEASED";
        public const string Judged = "JUDGED";
        public const string NoDate = "NO_DATE";
    }

    public sealed class SearchIndex
    {
        private readonly List<SearchRow> _rows = new List<SearchRow>();
        private readonly Dictionary<int, SearchRow> _byId = new Dictionary<int, SearchRow>();
        private readonly string[] _statusLabels;
        private readonly IDatabase _db;
        private readonly IDefendantStore _defendants;
        private readonly IClock _clock;

        public SearchIndex(IDatabase db, IDefendantStore defendants, IClock clock,
            string[] statusLabels)
        {
            _db = db;
            _defendants = defendants;
            _clock = clock;
            _statusLabels = statusLabels;
        }

        public int Count { get { return _rows.Count; } }

        public void Rebuild()
        {
            List<DefendantRow> all = _db.Read(s => _defendants.ListActiveRows(s));
            _rows.Clear();
            _byId.Clear();
            foreach (DefendantRow row in all)
            {
                Add(row);
            }
        }

        public void ReplaceRow(DefendantRow row)
        {
            SearchRow existing;
            if (_byId.TryGetValue(row.D.Id, out existing))
            {
                _rows[existing._index] = null;
                _byId.Remove(row.D.Id);
            }
            SearchRow fresh = Add(row);
            if (fresh != null && existing != null)
            {
                _rows[fresh._index] = fresh;
            }
        }

        public void Remove(int id)
        {
            SearchRow existing;
            if (!_byId.TryGetValue(id, out existing))
            {
                return;
            }
            _rows[existing._index] = null;
            _byId.Remove(id);
        }

        private SearchRow Add(DefendantRow row)
        {
            Defendant d = row.D;
            var sb = new StringBuilder(160);
            Append(sb, d.FullName);
            Append(sb, d.NationalId);
            Append(sb, d.CaseNumber);
            Append(sb, d.CaseYear.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Append(sb, d.CaseNumber + "/" + d.CaseYear.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Append(sb, d.CaseNumber + " " + d.CaseYear.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Append(sb, d.CaseType);
            Append(sb, d.Charges);
            Append(sb, row.CourtName);
            Append(sb, row.CircuitName);
            Append(sb, row.CustodyName);
            Append(sb, StatusLabel(d.Status));
            Append(sb, d.Notes);
            if (d.SessionDate.HasValue)
            {
                Append(sb, d.SessionDate.Value.ToIso());
                Append(sb, d.SessionDate.Value.Day.ToString("00") + "/" + d.SessionDate.Value.Month.ToString("00")
                    + "/" + d.SessionDate.Value.Year.ToString("0000"));
            }
            string spaced = sb.ToString();
            var rowOut = new SearchRow
            {
                Id = d.Id,
                D = d,
                Source = row,
                Spaced = spaced,
                Joined = RemoveSpaces(spaced),
                Filter = FilterKeyOf(d),
                State = DisplayState.FromRow(d.IsDeleted, d.Status, d.SessionDate, _clock.Today)
            };
            int at = FirstFreeSlot();
            if (at < 0)
            {
                _rows.Add(rowOut);
                at = _rows.Count - 1;
            }
            else
            {
                _rows[at] = rowOut;
            }
            rowOut._index = at;
            _byId[d.Id] = rowOut;
            return rowOut;
        }

        private int FirstFreeSlot()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] == null)
                {
                    return i;
                }
            }
            return -1;
        }

        public List<SearchRow> Filter(string query, string filter, List<SearchRow> reuse)
        {
            List<SearchRow> outp = reuse ?? new List<SearchRow>();
            outp.Clear();
            string[] tokens = Tokenize(query);
            LocalDate today = _clock.Today;
            for (int i = 0; i < _rows.Count; i++)
            {
                SearchRow row = _rows[i];
                if (row == null)
                {
                    continue;
                }
                if (!FilterMatches(row, filter))
                {
                    continue;
                }
                if (!Matches(row, tokens))
                {
                    continue;
                }
                outp.Add(row);
            }
            Sort(outp, today);
            return outp;
        }

        public int CountAll()
        {
            return _byId.Count;
        }

        public int CountFilter(string filter)
        {
            int n = 0;
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] != null && FilterMatches(_rows[i], filter))
                {
                    n++;
                }
            }
            return n;
        }

        public int CountQueryInFilter(string query, string filter)
        {
            return Filter(query, filter, null).Count;
        }

        public List<SearchRow> All()
        {
            var outp = new List<SearchRow>();
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] != null)
                {
                    outp.Add(_rows[i]);
                }
            }
            return outp;
        }

        private bool FilterMatches(SearchRow row, string filter)
        {
            if (string.IsNullOrEmpty(filter) || filter == Filters.All)
            {
                return true;
            }
            return row.Filter == filter;
        }

        private static string FilterKeyOf(Defendant d)
        {
            if (d.Status == DefendantStatuses.Released)
            {
                return Filters.Released;
            }
            if (d.Status == DefendantStatuses.Judged)
            {
                return Filters.Judged;
            }
            if (!d.SessionDate.HasValue)
            {
                return Filters.NoDate;
            }
            return Filters.Detained;
        }

        private static bool Matches(SearchRow row, string[] tokens)
        {
            if (tokens.Length == 0)
            {
                return true;
            }
            for (int t = 0; t < tokens.Length; t++)
            {
                string tok = tokens[t];
                if (row.Spaced.IndexOf(tok, StringComparison.Ordinal) >= 0)
                {
                    continue;
                }
                if (row.Joined.IndexOf(tok, StringComparison.Ordinal) >= 0)
                {
                    continue;
                }
                return false;
            }
            return true;
        }

        internal static string[] Tokenize(string query)
        {
            string n = ArabicText.Normalize(query);
            if (n.Length == 0)
            {
                return new string[0];
            }
            var list = new List<string>();
            foreach (string part in n.Split(' '))
            {
                string p = part.Trim();
                if (p.Length > 0)
                {
                    list.Add(p);
                }
            }
            return list.ToArray();
        }

        private static void Sort(List<SearchRow> rows, LocalDate today)
        {
            rows.Sort(delegate(SearchRow a, SearchRow b)
            {
                int ga = Group(a, today);
                int gb = Group(b, today);
                if (ga != gb)
                {
                    return ga.CompareTo(gb);
                }
                if (ga == 0)
                {
                    int c = a.D.SessionDate.Value.CompareTo(b.D.SessionDate.Value);
                    if (c != 0)
                    {
                        return c;
                    }
                }
                else if (ga == 1)
                {
                    return 0;
                }
                else if (ga == 2)
                {
                    int c = a.D.SessionDate.Value.CompareTo(b.D.SessionDate.Value);
                    if (c != 0)
                    {
                        return c;
                    }
                }
                return string.CompareOrdinal(a.D.NameNorm, b.D.NameNorm);
            });
        }

        private static int Group(SearchRow row, LocalDate today)
        {
            switch (row.State)
            {
                case DisplayStates.Overdue: return 0;
                case DisplayStates.Today: return 1;
                case DisplayStates.Upcoming: return 2;
                case DisplayStates.NoDate: return 3;
                default: return 4;
            }
        }

        private string StatusLabel(string status)
        {
            if (status == DefendantStatuses.Released)
            {
                return _statusLabels[1];
            }
            if (status == DefendantStatuses.Judged)
            {
                return _statusLabels[2];
            }
            return _statusLabels[0];
        }

        private static void Append(StringBuilder sb, string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return;
            }
            string n = ArabicText.Normalize(s);
            if (n.Length == 0)
            {
                return;
            }
            if (sb.Length > 0)
            {
                sb.Append(' ');
            }
            sb.Append(n);
        }

        private static string RemoveSpaces(string s)
        {
            if (s.IndexOf(' ') < 0)
            {
                return s;
            }
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (c != ' ')
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}

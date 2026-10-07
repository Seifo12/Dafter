using System;
using System.Collections.Generic;
using CourtDesk.Domain;

namespace CourtDesk.UI.Controls
{
    public sealed class CalendarCell
    {
        public LocalDate? Date { get; set; }
        public bool InMonth { get; set; }
        public bool IsWeekend { get; set; }
        public bool IsToday { get; set; }
        public bool IsSelected { get; set; }
        public bool Enabled { get; set; }
        public int Count { get; set; }
        public string Number
        {
            get { return Date.HasValue ? Date.Value.Day.ToString() : ""; }
        }
    }

    public static class CalendarModel
    {
        public const int FirstDayOfWeek = 6;

        public static LocalDate FirstOfMonth(LocalDate any)
        {
            return new LocalDate(any.Year, any.Month, 1);
        }

        public static LocalDate AddMonths(LocalDate any, int months)
        {
            int total = any.Year * 12 + (any.Month - 1) + months;
            int year = (int)Math.Floor(total / 12.0);
            int month = total - year * 12 + 1;
            return new LocalDate(year, month, 1);
        }

        public static int DayOfWeekIndex(LocalDate d)
        {
            return (int)d.DayOfWeek;
        }

        public static LocalDate StartGrid(LocalDate month)
        {
            LocalDate first = FirstOfMonth(month);
            int lead = (DayOfWeekIndex(first) - FirstDayOfWeek + 7) % 7;
            return first.AddDays(-lead);
        }

        public static List<CalendarCell> Build(LocalDate month, LocalDate today, LocalDate? selected,
            LocalDate? minimum, Dictionary<string, int> counts)
        {
            var cells = new List<CalendarCell>(42);
            LocalDate start = StartGrid(month);
            for (int i = 0; i < 42; i++)
            {
                LocalDate d = start.AddDays(i);
                int n = 0;
                if (counts != null)
                {
                    string key = d.ToIso();
                    if (counts.ContainsKey(key))
                    {
                        n = counts[key];
                    }
                }
                cells.Add(new CalendarCell
                {
                    Date = d,
                    InMonth = d.Month == month.Month && d.Year == month.Year,
                    IsWeekend = d.IsWeekend,
                    IsToday = d == today,
                    IsSelected = selected.HasValue && selected.Value == d,
                    Enabled = !minimum.HasValue || d >= minimum.Value,
                    Count = n
                });
            }
            while (cells.Count > 35 && cells[cells.Count - 1].Date.Value.Month != month.Month)
            {
                cells.RemoveAt(cells.Count - 1);
            }
            return cells;
        }

        public static string WeekdayHeader(int index)
        {
            return CourtDesk.Resources.Strings.Weekdays[(index + FirstDayOfWeek) % 7];
        }
    }
}
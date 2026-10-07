using System;

namespace CourtDesk.Domain
{
    public struct LocalDate : IComparable<LocalDate>, IEquatable<LocalDate>
    {
        public readonly int Year;
        public readonly int Month;
        public readonly int Day;

        public LocalDate(int year, int month, int day)
        {
            if (year < 1900 || year > 2200)
            {
                throw new ArgumentOutOfRangeException("year");
            }
            if (month < 1 || month > 12)
            {
                throw new ArgumentOutOfRangeException("month");
            }
            int dim = DateTime.DaysInMonth(year, month);
            if (day < 1 || day > dim)
            {
                throw new ArgumentOutOfRangeException("day");
            }
            Year = year;
            Month = month;
            Day = day;
        }

        public static LocalDate Parse(string iso)
        {
            LocalDate d;
            if (!TryParse(iso, out d))
            {
                throw new FormatException("expected YYYY-MM-DD: " + iso);
            }
            return d;
        }

        public static bool TryParse(string iso, out LocalDate date)
        {
            date = default(LocalDate);
            if (string.IsNullOrEmpty(iso) || iso.Length != 10)
            {
                return false;
            }
            if (iso[4] != '-' || iso[7] != '-')
            {
                return false;
            }
            int y;
            int m;
            int d;
            if (!TryDigits(iso, 0, 4, out y) || !TryDigits(iso, 5, 2, out m) || !TryDigits(iso, 8, 2, out d))
            {
                return false;
            }
            try
            {
                date = new LocalDate(y, m, d);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        private static bool TryDigits(string s, int start, int count, out int value)
        {
            value = 0;
            for (int i = 0; i < count; i++)
            {
                char c = s[start + i];
                if (c < '0' || c > '9')
                {
                    return false;
                }
                value = value * 10 + (c - '0');
            }
            return true;
        }

        public static LocalDate FromDateTime(DateTime d)
        {
            return new LocalDate(d.Year, d.Month, d.Day);
        }

        public string ToIso()
        {
            return Year.ToString("0000") + "-" + Month.ToString("00") + "-" + Day.ToString("00");
        }

        public override string ToString()
        {
            return ToIso();
        }

        public DayOfWeek DayOfWeek
        {
            get { return new DateTime(Year, Month, Day).DayOfWeek; }
        }

        public bool IsWeekend
        {
            get { return DayOfWeek == DayOfWeek.Friday || DayOfWeek == DayOfWeek.Saturday; }
        }

        public LocalDate AddDays(int days)
        {
            return FromDateTime(new DateTime(Year, Month, Day).AddDays(days));
        }

        public static int DaysBetween(LocalDate a, LocalDate b)
        {
            return (int)(new DateTime(b.Year, b.Month, b.Day) - new DateTime(a.Year, a.Month, a.Day)).TotalDays;
        }

        public int CompareTo(LocalDate other)
        {
            if (Year != other.Year)
            {
                return Year.CompareTo(other.Year);
            }
            if (Month != other.Month)
            {
                return Month.CompareTo(other.Month);
            }
            return Day.CompareTo(other.Day);
        }

        public bool Equals(LocalDate other)
        {
            return Year == other.Year && Month == other.Month && Day == other.Day;
        }

        public override bool Equals(object obj)
        {
            return obj is LocalDate && Equals((LocalDate)obj);
        }

        public override int GetHashCode()
        {
            return Year * 10000 + Month * 100 + Day;
        }

        public static bool operator ==(LocalDate a, LocalDate b) { return a.Equals(b); }
        public static bool operator !=(LocalDate a, LocalDate b) { return !a.Equals(b); }
        public static bool operator <(LocalDate a, LocalDate b) { return a.CompareTo(b) < 0; }
        public static bool operator >(LocalDate a, LocalDate b) { return a.CompareTo(b) > 0; }
        public static bool operator <=(LocalDate a, LocalDate b) { return a.CompareTo(b) <= 0; }
        public static bool operator >=(LocalDate a, LocalDate b) { return a.CompareTo(b) >= 0; }
    }
}

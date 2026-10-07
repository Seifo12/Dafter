using System;

namespace CourtDesk.Domain
{
    public static class ArabicDateFormat
    {
        public static int WeekdayIndex(DayOfWeek day)
        {
            return (int)day;
        }

        public static string FormatLong(LocalDate date, string[] weekdays, string[] months)
        {
            if (weekdays == null || weekdays.Length != 7)
            {
                throw new ArgumentException("weekdays");
            }
            if (months == null || months.Length != 12)
            {
                throw new ArgumentException("months");
            }
            return weekdays[(int)date.DayOfWeek] + " " + date.Day + " " + months[date.Month - 1] + " " + date.Year;
        }

        public static string FormatShort(LocalDate date)
        {
            return date.Day.ToString("00") + "/" + date.Month.ToString("00") + "/" + date.Year.ToString("0000");
        }
    }
}

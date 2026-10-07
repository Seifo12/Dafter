using System;
using CourtDesk.Domain;
using CourtDesk.Resources;
using Xunit;

namespace CourtDesk.Tests
{
    [Trait("Category", "Domain")]
    public sealed class LocalDateTests
    {
        [Fact]
        public void ParseValid()
        {
            LocalDate d = LocalDate.Parse("2026-10-06");
            Assert.Equal(2026, d.Year);
            Assert.Equal(10, d.Month);
            Assert.Equal(6, d.Day);
            Assert.Equal("2026-10-06", d.ToIso());
        }

        [Fact]
        public void ParseRejectsBadShapes()
        {
            string[] bad = new string[] { "", null, "06/10/2026", "2026-1-1", "2026-13-01", "2026-02-30", "2025-02-29", "abcd-ef-gh" };
            foreach (string s in bad)
            {
                LocalDate d;
                Assert.False(LocalDate.TryParse(s, out d), s ?? "null");
            }
        }

        [Fact]
        public void LeapDayAccepted()
        {
            Assert.Equal("2024-02-29", LocalDate.Parse("2024-02-29").ToIso());
        }

        [Fact]
        public void ParseThrowsOnInvalid()
        {
            Assert.Throws<FormatException>(() => LocalDate.Parse("2026-02-30"));
        }

        [Fact]
        public void AddDaysCrossesMonthAndYear()
        {
            Assert.Equal(new LocalDate(2026, 10, 7), new LocalDate(2026, 10, 6).AddDays(1));
            Assert.Equal(new LocalDate(2026, 2, 1), new LocalDate(2026, 1, 31).AddDays(1));
            Assert.Equal(new LocalDate(2026, 1, 1), new LocalDate(2025, 12, 31).AddDays(1));
            Assert.Equal(new LocalDate(2026, 10, 6), new LocalDate(2026, 10, 7).AddDays(-1));
        }

        [Fact]
        public void DayOfWeekKnownDates()
        {
            Assert.Equal(DayOfWeek.Tuesday, new LocalDate(2026, 10, 6).DayOfWeek);
            Assert.Equal(DayOfWeek.Wednesday, new LocalDate(2026, 10, 7).DayOfWeek);
            Assert.Equal(DayOfWeek.Friday, new LocalDate(2026, 10, 9).DayOfWeek);
            Assert.Equal(DayOfWeek.Saturday, new LocalDate(2026, 10, 10).DayOfWeek);
            Assert.Equal(DayOfWeek.Saturday, new LocalDate(2000, 1, 1).DayOfWeek);
        }

        [Fact]
        public void WeekendIsFridayAndSaturday()
        {
            Assert.True(new LocalDate(2026, 10, 9).IsWeekend);
            Assert.True(new LocalDate(2026, 10, 10).IsWeekend);
            Assert.False(new LocalDate(2026, 10, 7).IsWeekend);
        }

        [Fact]
        public void DaysBetweenAndComparison()
        {
            Assert.Equal(1, LocalDate.DaysBetween(new LocalDate(2026, 10, 6), new LocalDate(2026, 10, 7)));
            Assert.Equal(-1, LocalDate.DaysBetween(new LocalDate(2026, 10, 7), new LocalDate(2026, 10, 6)));
            Assert.True(new LocalDate(2026, 10, 6) < new LocalDate(2026, 10, 7));
            Assert.True(new LocalDate(2026, 10, 7) > new LocalDate(2026, 10, 6));
            Assert.True(new LocalDate(2026, 10, 6) <= new LocalDate(2026, 10, 6));
        }

        [Fact]
        public void ConstructorRejectsOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LocalDate(2026, 13, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LocalDate(2026, 10, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LocalDate(1800, 1, 1));
        }

        [Fact]
        public void ArabicFormatting()
        {
            Assert.Equal("الثلاثاء 6 أكتوبر 2026",
                ArabicDateFormat.FormatLong(new LocalDate(2026, 10, 6), Strings.Weekdays, Strings.Months));
            Assert.Equal("06/10/2026", ArabicDateFormat.FormatShort(new LocalDate(2026, 10, 6)));
            Assert.Equal(7, Strings.Weekdays.Length);
            Assert.Equal(12, Strings.Months.Length);
        }
    }
}

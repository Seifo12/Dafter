using CourtDesk.Domain;
using Xunit;

namespace CourtDesk.Tests
{
    [Trait("Category", "Domain")]
    public sealed class SnapshotJsonTests
    {
        private static DefendantSnapshot Sample()
        {
            return new DefendantSnapshot
            {
                Id = 7,
                FullName = "ABC",
                NationalId = "12345678901234",
                CaseNumber = "1234",
                CaseYear = 2026,
                CaseType = "T",
                Charges = "C",
                CourtId = 1,
                CircuitId = 2,
                CustodyLocationId = 3,
                SessionDate = new LocalDate(2026, 10, 6),
                Status = DefendantStatuses.Detained,
                DetainedSince = new LocalDate(2026, 9, 1),
                Notes = "N",
                IsDeleted = false
            };
        }

        [Fact]
        public void CanonicalOutputIsStable()
        {
            Assert.Equal(
                "{\"id\":7,\"full_name\":\"ABC\",\"national_id\":\"12345678901234\"," +
                "\"case_number\":\"1234\",\"case_year\":2026,\"case_type\":\"T\",\"charges\":\"C\"," +
                "\"court_id\":1,\"circuit_id\":2,\"custody_location_id\":3," +
                "\"session_date\":\"2026-10-06\",\"status\":\"DETAINED\",\"detained_since\":\"2026-09-01\"," +
                "\"notes\":\"N\",\"is_deleted\":false}",
                SnapshotJson.Write(Sample()));
        }

        [Fact]
        public void NullsSerializeAsNull()
        {
            DefendantSnapshot s = Sample();
            s.NationalId = null;
            s.CourtId = null;
            s.SessionDate = null;
            s.DetainedSince = null;
            string json = SnapshotJson.Write(s);
            Assert.Contains("\"national_id\":null", json);
            Assert.Contains("\"court_id\":null", json);
            Assert.Contains("\"session_date\":null", json);
            DefendantSnapshot back = SnapshotJson.Read(json);
            Assert.Null(back.NationalId);
            Assert.Null(back.CourtId);
            Assert.False(back.SessionDate.HasValue);
        }

        [Fact]
        public void RoundTripPreservesEveryField()
        {
            DefendantSnapshot back = SnapshotJson.Read(SnapshotJson.Write(Sample()));
            Assert.Equal(SnapshotJson.Write(Sample()), SnapshotJson.Write(back));
            Assert.Equal(7, back.Id);
            Assert.Equal("ABC", back.FullName);
            Assert.Equal(2026, back.CaseYear);
            Assert.Equal(new LocalDate(2026, 10, 6), back.SessionDate);
        }

        [Fact]
        public void EscapesSpecialCharacters()
        {
            DefendantSnapshot s = Sample();
            s.Notes = "a\"b\\c\nd\te";
            string json = SnapshotJson.Write(s);
            Assert.Contains("\"notes\":\"a\\\"b\\\\c\\nd\\te\"", json);
            Assert.Equal("a\"b\\c\nd\te", SnapshotJson.Read(json).Notes);
        }
    }

    [Trait("Category", "Domain")]
    public sealed class DisplayStateTests
    {
        private static readonly LocalDate Today = new LocalDate(2026, 10, 7);

        [Fact]
        public void AllBranches()
        {
            Assert.Equal(DisplayStates.Hidden,
                DisplayState.FromRow(true, DefendantStatuses.Detained, new LocalDate(2026, 10, 7), Today));
            Assert.Equal(DisplayStates.Released,
                DisplayState.FromRow(false, DefendantStatuses.Released, null, Today));
            Assert.Equal(DisplayStates.Judged,
                DisplayState.FromRow(false, DefendantStatuses.Judged, null, Today));
            Assert.Equal(DisplayStates.NoDate,
                DisplayState.FromRow(false, DefendantStatuses.Detained, null, Today));
            Assert.Equal(DisplayStates.Overdue,
                DisplayState.FromRow(false, DefendantStatuses.Detained, new LocalDate(2026, 10, 6), Today));
            Assert.Equal(DisplayStates.Today,
                DisplayState.FromRow(false, DefendantStatuses.Detained, new LocalDate(2026, 10, 7), Today));
            Assert.Equal(DisplayStates.Upcoming,
                DisplayState.FromRow(false, DefendantStatuses.Detained, new LocalDate(2026, 10, 8), Today));
        }
    }
}

using System;
using System.Collections.Generic;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;
using CourtDesk.Persistence;
using CourtDesk.UI.ViewModels;
using Xunit;

namespace CourtDesk.Tests
{
    internal static class ViewDbAssert
    {
        public static void AssertViewsMatchDb(MainViewModel main, TodayService today,
            IDatabase db, IDefendantStore defendants, IClock clock)
        {
            main.Register.SearchText = "";
            main.Register.Filter = Filters.All;
            main.Register.Refresh();

            List<DefendantRow> active = db.Read(s => defendants.ListActiveRows(s));
            Assert.Equal(active.Count, main.Register.Rows.Count);
            var byId = new Dictionary<int, DefendantRow>();
            foreach (DefendantRow r in active)
            {
                byId[r.D.Id] = r;
            }
            foreach (RegisterRow row in main.Register.Rows)
            {
                Assert.True(byId.ContainsKey(row.Id), "grid row missing from DB: " + row.Id);
                DefendantRow want = byId[row.Id];
                Assert.Equal(want.D.FullName, row.Name);
                string wantSession = want.D.SessionDate.HasValue
                    ? ArabicDateFormat.FormatShort(want.D.SessionDate.Value)
                    : "";
                Assert.Equal(wantSession, row.SessionLine1);
                Assert.Equal(
                    DisplayState.FromRow(want.D.IsDeleted, want.D.Status, want.D.SessionDate, clock.Today),
                    row.StateKey);
            }

            var expect = new Dictionary<string, int>();
            expect[Filters.Detained] = 0;
            expect[Filters.All] = active.Count;
            expect[Filters.Released] = 0;
            expect[Filters.Judged] = 0;
            expect[Filters.NoDate] = 0;
            foreach (DefendantRow r in active)
            {
                string key = ChipKeyOf(r.D);
                expect[key] = expect[key] + 1;
            }
            foreach (FilterChipItem chip in main.Register.Chips)
            {
                Assert.True(expect.ContainsKey(chip.Key), "unknown chip " + chip.Key);
                Assert.Equal(expect[chip.Key], chip.Count);
            }

            TodaySnapshot snap = today.Snapshot();
            AssertCardIds(snap.Pending, main.Sidebar.PendingCards);
            AssertCardIds(snap.Overdue, main.Sidebar.OverdueCards);
            var decidedIds = new List<int>();
            foreach (DecidedRow r in snap.Decided)
            {
                decidedIds.Add(r.D.Id);
            }
            var cardIds = new List<int>();
            foreach (SessionCard c in main.Sidebar.DecidedCards)
            {
                cardIds.Add(c.Id);
            }
            int shown = Math.Min(decidedIds.Count, 5);
            Assert.Equal(shown, cardIds.Count);
            foreach (int id in cardIds)
            {
                Assert.Contains(id, decidedIds);
            }
        }

        private static string ChipKeyOf(Defendant d)
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

        private static void AssertCardIds(List<DefendantRow> rows,
            System.Collections.ObjectModel.ObservableCollection<SessionCard> cards)
        {
            var want = new List<int>();
            foreach (DefendantRow r in rows)
            {
                want.Add(r.D.Id);
            }
            var got = new List<int>();
            foreach (SessionCard c in cards)
            {
                got.Add(c.Id);
            }
            want.Sort();
            got.Sort();
            Assert.Equal(want.Count, got.Count);
            for (int i = 0; i < want.Count; i++)
            {
                Assert.Equal(want[i], got[i]);
            }
        }
    }
}
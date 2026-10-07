using System;
using System.Windows;
using CourtDesk.Application;
using CourtDesk.Domain;
using CourtDesk.UI.Views;
using Xunit;

namespace CourtDesk.Tests
{
    [Trait("Category", "UI")]
    public sealed class SidebarTests
    {
        [Fact]
        public void EmptySidebarShowsEmptyState()
        {
            StaRunner.Run(delegate
            {
                using (var env = new UiEnv())
                {
                    var view = new TodaySidebar { DataContext = env.Sidebar };
                    Assert.True(env.Sidebar.Empty);
                    Assert.Equal(0, env.Sidebar.PendingCards.Count);
                    Assert.Equal(0, env.Sidebar.OverdueCards.Count);
                    Assert.Equal(0, env.Sidebar.DecidedCards.Count);
                    Assert.Equal(FlowDirection.RightToLeft, view.FlowDirection);
                }
            });
        }

        [Fact]
        public void PendingOverdueAndDecidedCards()
        {
            StaRunner.Run(delegate
            {
                using (var env = new UiEnv())
                {
                    int p1 = env.AddDefendant("Pending One", "7001", new LocalDate(2026, 10, 7));
                    int p2 = env.AddDefendant("Pending Two", "7002", new LocalDate(2026, 10, 7));
                    int o1 = env.AddDefendant("Overdue One", "7003", new LocalDate(2026, 10, 1));
                    env.Refresh();
                    Assert.False(env.Sidebar.Empty);
                    Assert.Equal(2, env.Sidebar.PendingCards.Count);
                    Assert.Equal(1, env.Sidebar.OverdueCards.Count);
                    Assert.Equal(0, env.Sidebar.DecidedCards.Count);
                    string progress = env.Sidebar.Progress;
                    Assert.True(progress.Contains("0") && progress.Contains("2"), progress);

                    var decisions = new DecisionService(env.Db, env.Defendants, env.Events,
                        env.Clock, env.Settings);
                    decisions.Decide(new DecisionRequest
                    {
                        RequestUuid = Guid.NewGuid().ToString("N"),
                        DefendantId = p1,
                        ExpectedVersion = 1,
                        Code = DecisionCodes.RenewDetention,
                        NextDate = new LocalDate(2026, 10, 20)
                    });
                    env.Refresh();
                    Assert.Equal(1, env.Sidebar.PendingCards.Count);
                    Assert.Equal(1, env.Sidebar.DecidedCards.Count);
                    Assert.True(env.Sidebar.DecidedCards[0].ShowUndo);
                    Assert.True(env.Sidebar.DecidedCards[0].ResultSentence.Length > 0);
                    Assert.Contains("1", env.Sidebar.Progress);
                    Assert.Equal(p2, env.Sidebar.PendingCards[0].Id);
                    Assert.Equal(o1, env.Sidebar.OverdueCards[0].Id);
                }
            });
        }

        [Fact]
        public void DayRolloverRefreshesSidebar()
        {
            StaRunner.Run(delegate
            {
                using (var env = new UiEnv())
                {
                    env.AddDefendant("Rollover One", "7004", new LocalDate(2026, 10, 8));
                    env.Refresh();
                    Assert.True(env.Sidebar.Empty);
                    env.Clock.Today = new LocalDate(2026, 10, 8);
                    env.Refresh();
                    Assert.False(env.Sidebar.Empty);
                    Assert.Equal(1, env.Sidebar.PendingCards.Count);
                }
            });
        }
    }
}

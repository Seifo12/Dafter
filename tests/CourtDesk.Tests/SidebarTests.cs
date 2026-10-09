using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
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

        [Fact]
        public void DecidedStripCollapsesToFiveWithPlusNAndExpands()
        {
            StaRunner.Run(delegate
            {
                using (var env = new UiEnv())
                {
                    var decisions = new DecisionService(env.Db, env.Defendants, env.Events,
                        env.Clock, env.Settings);
                    for (int i = 0; i < 7; i++)
                    {
                        int id = env.AddDefendant("Decided " + i, (7100 + i).ToString(),
                            new LocalDate(2026, 10, 7));
                        Defendant d = new DefendantService(env.Db, env.Defendants, env.Events,
                            env.Clock, env.Settings, new NullLogger()).Get(id);
                        decisions.Decide(new DecisionRequest
                        {
                            RequestUuid = Guid.NewGuid().ToString("N"),
                            DefendantId = id,
                            ExpectedVersion = d.RowVersion,
                            Code = DecisionCodes.Release,
                            NextDate = null
                        });
                    }
                    env.Refresh();
                    Assert.Equal(5, env.Sidebar.DecidedCards.Count);
                    Assert.Equal(2, env.Sidebar.MoreDecided);
                    Assert.Equal("+2", env.Sidebar.MoreDecidedText);
                    Assert.True(env.Sidebar.HasMoreDecided);
                    Assert.Equal(CourtDesk.Resources.Strings.SideShowAll,
                        env.Sidebar.DecidedToggleText);
                    env.Sidebar.ToggleDecidedCommand.Execute(null);
                    Assert.True(env.Sidebar.DecidedExpanded);
                    Assert.Equal(7, env.Sidebar.DecidedCards.Count);
                    Assert.Equal(0, env.Sidebar.MoreDecided);
                    Assert.Equal("", env.Sidebar.MoreDecidedText);
                    Assert.Equal(CourtDesk.Resources.Strings.SideShowLess,
                        env.Sidebar.DecidedToggleText);
                    env.Sidebar.ToggleDecidedCommand.Execute(null);
                    Assert.False(env.Sidebar.DecidedExpanded);
                    Assert.Equal(5, env.Sidebar.DecidedCards.Count);
                }
            });
        }
        [Fact]
        public void SessionCardButtonsShowFullLabel()
        {
            StaRunner.Run(delegate
            {
                using (var env = new UiEnv())
                {
                    env.AddDefendant("Pending One", "7001", new LocalDate(2026, 10, 7));
                    env.AddDefendant("Pending Two", "7002", new LocalDate(2026, 10, 7));
                    env.Refresh();
                    var view = new TodaySidebar { DataContext = env.Sidebar };
                    view.Measure(new Size(272, 700));
                    view.Arrange(new Rect(0, 0, 272, 700));
                    view.UpdateLayout();
                    var labels = new List<string>();
                    CollectRecordLabels(view, labels);
                    Assert.True(labels.Count >= 2, "expected 2 record labels, got " + labels.Count);
                    foreach (string s in labels)
                    {
                        Assert.Equal(CourtDesk.Resources.Strings.SideRecord, s);
                    }
                }
            });
        }

        private static void CollectRecordLabels(DependencyObject root, List<string> outp)
        {
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                Button b = child as Button;
                if (b != null && b.CommandParameter is CourtDesk.UI.ViewModels.SessionCard)
                {
                    var texts = new List<TextBlock>();
                    CollectTextBlocks(b, texts);
                    foreach (TextBlock t in texts)
                    {
                        if (!string.IsNullOrEmpty(t.Text))
                        {
                            outp.Add(t.Text);
                        }
                    }
                }
                CollectRecordLabels(child, outp);
            }
        }

        private static void CollectTextBlocks(DependencyObject root, List<TextBlock> outp)
        {
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                TextBlock t = child as TextBlock;
                if (t != null)
                {
                    outp.Add(t);
                }
                CollectTextBlocks(child, outp);
            }
        }
[Fact]
        public void TomorrowTabListsNextDay()
        {
            StaRunner.Run(delegate
            {
                using (var env = new UiEnv())
                {
                    env.Clock.Today = new LocalDate(2026, 10, 7);
                    env.AddDefendant("بكره واحد", "7010", new LocalDate(2026, 10, 8));
                    env.AddDefendant("بكره اتنين", "7011", new LocalDate(2026, 10, 8));
                    env.AddDefendant("النهارده", "7012", new LocalDate(2026, 10, 7));
                    env.Refresh();
                    Assert.Equal(2, env.Sidebar.TomorrowCards.Count);
                    Assert.Contains("2", env.Sidebar.TomorrowTabText);
                    env.Sidebar.Tab = "tomorrow";
                    Assert.Equal("tomorrow", env.Sidebar.Tab);
                    bool names = false;
                    foreach (var c in env.Sidebar.TomorrowCards)
                    {
                        if (c.Name == "بكره واحد")
                        {
                            names = true;
                        }
                    }
                    Assert.True(names, "tomorrow cards wrong");
                }
            });
        }
    }
}

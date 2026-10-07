using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;
using CourtDesk.Persistence;
using CourtDesk.UI.Controls;
using CourtDesk.UI.Dialogs;
using CourtDesk.UI.ViewModels;
using Xunit;

namespace CourtDesk.Tests
{
    [Trait("Category", "UI")]
    public sealed class DialogInteractionTests
    {
        private sealed class Env : IDisposable
        {
            public string Home;
            public DbGateway Db;
            public FakeClock Clock;
            public SettingsService Settings;
            public IDefendantStore Defendants;
            public IEventStore Events;
            public LookupService Lookups;
            public DefendantService DefendantService;
            public DecisionService DecisionService;
            public UndoService Undo;

            public Env()
            {
                Home = TestDb.NewHome();
                Clock = new FakeClock();
                RecoveryResult rec = TestDb.Open(Home, Clock);
                Db = rec.Gateway;
                Defendants = new SqliteDefendantStore();
                Events = new SqliteEventStore();
                var store = new SqliteLookupStore();
                Settings = new SettingsService(Db, new SqliteSettingsStore());
                Db.Write(s =>
                {
                    SeedData.SeedLookups((DbContext)s,
                        CourtDesk.Resources.Strings.SeedCourts,
                        CourtDesk.Resources.Strings.SeedCustody,
                        CourtDesk.Resources.Strings.SeedReasons);
                    return 0;
                });
                Lookups = new LookupService(Db, store);
                DefendantService = new DefendantService(Db, Defendants, Events, Clock, Settings, new NullLogger());
                DecisionService = new DecisionService(Db, Defendants, Events, Clock, Settings);
                Undo = new UndoService(Db, Defendants, Events, Clock, Settings);
            }

            public int Add(string name, string caseNumber, LocalDate? session)
            {
                return DefendantService.Create(new DefendantInput
                {
                    FullName = name,
                    CaseNumber = caseNumber,
                    CaseYear = "2026",
                    SessionDate = session
                }, Guid.NewGuid().ToString("N"), true).Id;
            }

            public void Dispose()
            {
                try { Db.Close(); }
                catch (Exception) { }
                TestDb.Wipe(Home);
            }
        }

        private static void Pump()
        {
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Loaded, new Action(delegate { }));
        }

        private static List<Button> ButtonsUnder(DependencyObject root)
        {
            var outp = new List<Button>();
            var stack = new Stack<DependencyObject>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                DependencyObject cur = stack.Pop();
                Button b = cur as Button;
                if (b != null)
                {
                    outp.Add(b);
                }
                int n = VisualTreeHelper.GetChildrenCount(cur);
                for (int i = 0; i < n; i++)
                {
                    stack.Push(VisualTreeHelper.GetChild(cur, i));
                }
            }
            return outp;
        }

        private static string ShotDir()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, "CourtDesk.sln")))
                {
                    string d = Path.Combine(dir, "artifacts", "dialogs");
                    Directory.CreateDirectory(d);
                    return d;
                }
                dir = Path.GetDirectoryName(dir);
            }
            throw new InvalidOperationException("repo root not found");
        }

        private static void Shot(Window w, string name)
        {
            w.Measure(new Size(w.Width > 0 ? w.Width : 900, w.Height > 0 ? w.Height : 700));
            w.Arrange(new Rect(0, 0, w.Width > 0 ? w.Width : 900, w.Height > 0 ? w.Height : 700));
            w.UpdateLayout();
            Pump();
            int sw = (int)Math.Ceiling(w.ActualWidth);
            int sh = (int)Math.Ceiling(w.ActualHeight);
            if (sw < 10 || sh < 10)
            {
                return;
            }
            var bmp = new RenderTargetBitmap(sw, sh, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(w);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using (var fs = new FileStream(Path.Combine(ShotDir(), name + ".png"), FileMode.Create))
            {
                enc.Save(fs);
            }
        }

        private static DependencyObject FindByName(DependencyObject root, string name)
        {
            var stack = new Stack<DependencyObject>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                DependencyObject cur = stack.Pop();
                FrameworkElement fe = cur as FrameworkElement;
                if (fe != null && fe.Name == name)
                {
                    return cur;
                }
                int n = VisualTreeHelper.GetChildrenCount(cur);
                for (int i = 0; i < n; i++)
                {
                    stack.Push(VisualTreeHelper.GetChild(cur, i));
                }
            }
            return null;
        }

        private static Button FindOffsetButton(DependencyObject root, int days)
        {
            foreach (Button b in ButtonsUnder(root))
            {
                if (b.Tag is int && (int)b.Tag == days)
                {
                    return b;
                }
            }
            return null;
        }

        [Fact]
        public void OffsetButtonsChangeTheChosenDate()
        {
            StaRunner.Run(delegate
            {
                using (var env = new Env())
                {
                    int id = env.Add("زراير الاوفست", "7001", new LocalDate(2026, 10, 7));
                    var vm = new DecisionWizardViewModel(env.DecisionService, env.Undo,
                        env.DefendantService, env.Lookups, env.Settings, env.Clock,
                        new List<int> { id }, 0);
                    vm.Choose(DecisionCodes.RenewDetention);
                    Assert.Equal(2, vm.Step);

                    var dlg = new DecisionWizardDialog(vm, null, env.Clock);
                    dlg.Left = -32000;
                    dlg.Top = -32000;
                    dlg.ShowActivated = false;
                    try
                    {
                        dlg.Show();
                        Pump();
                        dlg.UpdateLayout();
                        Pump();

                        List<Button> all = ButtonsUnder(dlg);
                        string dump = "buttons=" + all.Count + ";";
                        foreach (Button bb in all)
                        {
                            object tag = bb.Tag;
                            string content = bb.Content as string;
                            dump += " [" + (tag == null ? "null" : tag.ToString()) + "/"
                                + (content == null ? (bb.Content == null ? "null" : bb.Content.GetType().Name) : content) + "]";
                        }
                        Assert.True(vm.Offsets.Count >= 3);
                        var session = new LocalDate(2026, 10, 7);
                        foreach (OffsetButton option in vm.Offsets)
                        {
                            Button b = FindOffsetButton(dlg, option.Days);
                            Assert.True(b != null, "no button for offset " + option.Days + ". " + dump);
                            Assert.False(string.IsNullOrEmpty(b.Content as string),
                                "empty label for offset " + option.Days);
                            b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            Pump();
                            Assert.Equal(session.AddDays(option.Days), vm.NextDate);
                            Assert.Contains(session.AddDays(option.Days).Day.ToString(), vm.ChosenLine);
                        }
                        Shot(dlg, "wiz-step2-offsets");
                    }
                    finally
                    {
                        dlg.Close();
                    }
                }
            });
        }

        [Fact]
        public void CardTimelineShowsCreatedAndDecision()
        {
            StaRunner.Run(delegate
            {
                using (var env = new Env())
                {
                    int id = env.Add("بطاقة التايم لاين", "7002", new LocalDate(2026, 10, 7));
                    Defendant d = env.DefendantService.Get(id);
                    env.DecisionService.Decide(new DecisionRequest
                    {
                        RequestUuid = Guid.NewGuid().ToString("N"),
                        DefendantId = id,
                        ExpectedVersion = d.RowVersion,
                        Code = DecisionCodes.RenewDetention,
                        NextDate = new LocalDate(2026, 10, 20)
                    });

                    var dlg = new DefendantCardDialog(env.DefendantService, env.Undo, env.Lookups, id);
                    dlg.Left = -32000;
                    dlg.Top = -32000;
                    dlg.ShowActivated = false;
                    try
                    {
                        dlg.Show();
                        Pump();
                        dlg.UpdateLayout();
                        Pump();
                        ItemsControl timeline = FindByName(dlg, "Timeline") as ItemsControl;
                        Assert.True(timeline != null, "timeline control not found");
                        Assert.True(timeline.Items.Count >= 2,
                            "timeline has " + timeline.Items.Count + " items, want >= 2");
                        foreach (TimelineEntry e in timeline.Items)
                        {
                            Assert.False(string.IsNullOrEmpty(e.Title));
                            Assert.DoesNotContain("LookupItem", e.Title + e.Detail);
                        }
                        Shot(dlg, "card-with-timeline");
                    }
                    finally
                    {
                        dlg.Close();
                    }
                }
            });
        }

        [Fact]
        public void LookupItemsRenderTheirNames()
        {
            var items = new List<LookupItem>
            {
                new LookupItem { Id = 1, Name = "النيابة العامة" },
                new LookupItem { Id = 2, Name = "حجز القسم" }
            };
            foreach (LookupItem item in items)
            {
                Assert.Equal(item.Name, item.ToString());
                Assert.DoesNotContain("LookupItem", item.ToString());
            }
        }
    }
}
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
            public SearchIndex Index;
            public TodayService Today;
            public RegisterViewModel Register;
            public TodaySidebarViewModel Sidebar;
            public MainViewModel Main;

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
                Index = new SearchIndex(Db, Defendants, Clock, new string[] { "d", "r", "j" });
                Index.Rebuild();
                Today = new TodayService(Db, Defendants, Clock, Settings);
                Register = new RegisterViewModel(Index, Today, null, null);
                Sidebar = new TodaySidebarViewModel(Today);
                Main = new MainViewModel(Settings, Clock, Register, Sidebar);
                Main.Index = Index;
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

        [Fact]
        public void CardShowsCircuitNameNotIdAndFormattedDates()
        {
            StaRunner.Run(delegate
            {
                using (var env = new Env())
                {
                    var courts = env.Lookups.Courts(false);
                    Assert.True(courts.Count > 0, "no seeded courts");
                    int circuitId = env.Lookups.AddCircuit(courts[0].Id, "دائرة الاختبار");
                    var circuits = env.Lookups.Circuits(courts[0].Id, false);
                    Assert.True(circuits.Count > 0, "no seeded circuits");
                    string circuitName = circuits[0].Name;
                    int id = env.DefendantService.Create(new DefendantInput
                    {
                        FullName = "صاحب دائرة",
                        CaseNumber = "7401",
                        CaseYear = "2026",
                        SessionDate = new LocalDate(2026, 10, 7),
                        CourtId = courts[0].Id,
                        CircuitId = circuitId
                    }, Guid.NewGuid().ToString("N"), true).Id;

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
                        TextBlock court = FindByName(dlg, "CourtText") as TextBlock;
                        Assert.True(court != null, "CourtText not found");
                        Assert.Contains(circuitName, court.Text);
                        TextBox fields = FindByName(dlg, "FieldsBox") as TextBox;
                        Assert.True(fields != null, "FieldsBox not found");
                        Assert.Contains(circuitName, fields.Text);
                        ItemsControl timeline = FindByName(dlg, "Timeline") as ItemsControl;
                        Assert.True(timeline != null && timeline.Items.Count > 0, "no timeline");
                        foreach (TimelineEntry e in timeline.Items)
                        {
                            Assert.DoesNotContain("T", e.Detail);
                            Assert.Contains("/", e.Detail);
                        }
                    }
                    finally
                    {
                        dlg.Close();
                    }
                }
            });
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
        public void MedicalImagesDialogOpensWithoutMissingResources()
        {
            StaRunner.Run(delegate
            {
                using (var env = new Env())
                {
                    int id = env.Add("صاحب صورة", "7200", new LocalDate(2026, 10, 7));
                    var medical = new MedicalService(new SqliteMedicalStore(env.Db),
                        env.Home, () => env.Clock.UtcNow);
                    var dlg = new MedicalImagesDialog(medical, id);
                    dlg.Left = -32000;
                    dlg.Top = -32000;
                    dlg.ShowActivated = false;
                    try
                    {
                        dlg.Show();
                        Pump();
                        dlg.UpdateLayout();
                        Pump();
                        Assert.NotNull(dlg.FindName("List"));
                        Assert.NotNull(dlg.FindName("AddButton"));
                    }
                    finally
                    {
                        dlg.Close();
                    }
                }
            });
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
                        var primary = dlg.FindResource("PrimaryButton") as System.Windows.Style;
                        var secondary = dlg.FindResource("SecondaryButton") as System.Windows.Style;
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
                            Assert.True(object.ReferenceEquals(b.Style, primary),
                                "selected offset not primary for " + option.Days);
                            foreach (OffsetButton other in vm.Offsets)
                            {
                                if (other.Days == option.Days)
                                {
                                    continue;
                                }
                                Button ob = FindOffsetButton(dlg, other.Days);
                                Assert.True(object.ReferenceEquals(ob.Style, secondary),
                                    "unselected offset not secondary for " + other.Days);
                            }
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
        public void FridayOffsetShowsWeekendTickBoxFullyVisible()
        {
            StaRunner.Run(delegate
            {
                using (var env = new Env())
                {
                    int id = env.Add("friday held", "7009", new LocalDate(2026, 10, 9));
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
                        Button b = FindOffsetButton(dlg, 7);
                        Assert.True(b != null, "no 7-day offset button");
                        b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Pump();
                        dlg.UpdateLayout();
                        Pump();
                        Assert.Equal(new LocalDate(2026, 10, 16), vm.NextDate);
                        var warn = dlg.FindName("WarnWeekend") as System.Windows.Controls.Border;
                        Assert.True(warn != null, "no WarnWeekend band");
                        Assert.Equal(Visibility.Visible, warn.Visibility);
                        var box = dlg.FindName("ConfirmWeekendBox") as System.Windows.Controls.CheckBox;
                        Assert.True(box != null, "no ConfirmWeekendBox");
                        Assert.True(box.ActualHeight >= 40, "tick box too short: " + box.ActualHeight);
                        var top = box.TranslatePoint(new System.Windows.Point(0, 0), dlg);
                        var bottom = box.TranslatePoint(
                            new System.Windows.Point(0, box.ActualHeight), dlg);
                        Assert.True(bottom.Y <= dlg.ActualHeight,
                            "tick box bottom " + bottom.Y + " outside dialog " + dlg.ActualHeight);
                        Assert.True(top.Y >= 0, "tick box top above dialog");
                        Shot(dlg, "wiz-step2-friday");
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
        public void WizardSaveEndToEndUpdatesGrid()
        {
            StaRunner.Run(delegate
            {
                using (var env = new Env())
                {
                    int id = env.Add("حفظ من المعالج", "7003", new LocalDate(2026, 10, 7));
                    var vm = new DecisionWizardViewModel(env.DecisionService, env.Undo,
                        env.DefendantService, env.Lookups, env.Settings, env.Clock,
                        new List<int> { id }, 0);
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

                        Button tile = null;
                        foreach (Button b in ButtonsUnder(dlg))
                        {
                            if (b.Tag is DecisionTile)
                            {
                                var t = (DecisionTile)b.Tag;
                                if (t.Code == DecisionCodes.RenewDetention)
                                {
                                    tile = b;
                                }
                            }
                        }
                        Assert.True(tile != null, "renew tile not found");
                        Assert.True(tile.Command != null, "tile button has no command");
                        Assert.True(tile.CommandParameter is DecisionTile, "tile parameter is not a tile");
                        Assert.True(tile.Command.CanExecute(tile.CommandParameter),
                            "tile command cannot execute");
                        tile.Command.Execute(tile.CommandParameter);
                        Pump();
                        Assert.Equal(2, vm.Step);

                        Button fifteen = FindOffsetButton(dlg, 15);
                        Assert.True(fifteen != null, "15-day button not found");
                        fifteen.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Pump();
                        Assert.Equal(new LocalDate(2026, 10, 22), vm.NextDate);

                        vm.Save();
                        Pump();
                        Assert.True(vm.Done, "wizard did not complete save");
                    }
                    finally
                    {
                        dlg.Close();
                    }
                    env.Main.RefreshAfterDataChanged(new List<int> { id });
                    ViewDbAssert.AssertViewsMatchDb(env.Main, env.Today, env.Db,
                        env.Defendants, env.Clock);
                    bool found = false;
                    foreach (var row in env.Register.Rows)
                    {
                        if (row.Id == id && row.SessionLine1 == "22/10/2026")
                        {
                            found = true;
                        }
                    }
                    Assert.True(found, "decided row with new date not in grid");
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
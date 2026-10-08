using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Bootstrap;
using CourtDesk.Domain;
using CourtDesk.Persistence;
using CourtDesk.UI.Dialogs;
using CourtDesk.UI.ViewModels;
using CourtDesk.UI.ViewModels;
using Xunit;

namespace CourtDesk.Tests
{
    internal sealed class DialogEnv : IDisposable
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

        public DialogEnv()
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

        public void Refresh()
        {
            Index.Rebuild();
            Register.Refresh();
            Sidebar.Refresh();
        }

        public void Dispose()
        {
            try { Db.Close(); }
            catch (Exception) { }
            TestDb.Wipe(Home);
        }
    }

    [Trait("Category", "UI")]
    public sealed class TemplateAudit
    {
        [Fact]
        public void NoDefaultChromeInViewsAndDialogs()
        {
            StaRunner.Run(delegate
            {
                var failures = new List<string>();
                using (var env = new DialogEnv())
                {
                    int id = env.Add("template audit", "6001", new LocalDate(2026, 10, 7));
                    env.Refresh();
                    CheckWindow("main", Show(new CourtDesk.MainWindow(env.Main), 1366, 768), failures);
                    var wiz = OpenWizard(env, id);
                    try
                    {
                        Show(wiz, 960, 740);
                        CheckWindow("wizard-step1", wiz, failures);
                        wiz.ViewModel.Choose(DecisionCodes.RenewDetention);
                        Pump();
                        CheckWindow("wizard-step2", wiz, failures);
                        wiz.ViewModel.PickOffset(7);
                        wiz.ViewModel.NextCommand.Execute(null);
                        Pump();
                        CheckWindow("wizard-step3", wiz, failures);
                    }
                    finally
                    {
                        wiz.Close();
                    }
                    CheckWindow("edit", Show(OpenEdit(env, 0), 900, 740), failures);
                    CheckWindow("card", Show(new DefendantCardDialog(
                        env.DefendantService, env.Undo, env.Lookups, id), 900, 700), failures);
                    CheckWindow("confirm", Show(new ConfirmDialog("t", "b", "d", "s"), 500, 300), failures);
                    CheckWindow("message", Show(new MessageDialog("t", "b"), 500, 300), failures);
                    CheckWindow("clock", Show(new ClockWarningDialog(), 600, 400), failures);
                    CheckWindow("help", Show(new HelpWindow(), 800, 600), failures);
                    CheckWindow("firstrun", Show(new FirstRunDialog(env.Settings), 700, 500), failures);
                    CheckWindow("asktext", Show(new AskTextWindow("t", "l"), 500, 300), failures);
                }
                Assert.True(failures.Count == 0, "TemplateAudit failures:" + Environment.NewLine
                    + string.Join(Environment.NewLine, failures.ToArray()));
            });
        }

        [Fact]
        public void CompositionRootDatabaseChangeRefreshesUiWithoutCrash()
        {
            StaRunner.Run(delegate
            {
                string home = TestDb.NewHome();
                Composition c = null;
                Window window = null;
                try
                {
                    c = CompositionRoot.Build(home, new FakeClock(), null, new NullLogger());
                    var svc = new DefendantService(c.Db, c.Defendants, c.Events,
                        c.Clock, c.Settings, new NullLogger());
                    c.Main.Register.Filter = Filters.All;
                    c.Main.RefreshAll();
                    window = new CourtDesk.MainWindow(c.Main, c);
                    window.WindowState = WindowState.Normal;
                    window.Width = 1366;
                    window.Height = 768;
                    window.Left = -32000;
                    window.Top = -32000;
                    window.ShowActivated = false;
                    window.Show();
                    Pump();
                    window.UpdateLayout();
                    var unhandled = new List<Exception>();
                    window.Dispatcher.UnhandledException += delegate(object s,
                        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
                    {
                        unhandled.Add(e.Exception);
                    };
                    Exception error = null;
                    int created = 0;
                    var worker = new System.Threading.Thread(delegate()
                    {
                        try
                        {
                            c.Main.RefreshAll();
                            created = svc.Create(new DefendantInput
                            {
                                FullName = "cross thread",
                                CaseNumber = "6401",
                                CaseYear = "2026",
                                SessionDate = new LocalDate(2026, 10, 7)
                            }, Guid.NewGuid().ToString("N"), true).Id;
                        }
                        catch (Exception e)
                        {
                            error = e;
                        }
                    });
                    worker.Start();
                    worker.Join();
                    Assert.True(error == null, "background write failed: "
                        + (error == null ? "" : error.GetType().Name + ": " + error.Message));
                    for (int i = 0; i < 10; i++)
                    {
                        Pump();
                        System.Threading.Thread.Sleep(20);
                    }
                    window.UpdateLayout();
                    Assert.True(unhandled.Count == 0, "unhandled on UI thread: "
                        + (unhandled.Count == 0 ? "" : unhandled[0].GetType().Name
                            + ": " + unhandled[0].Message));
                    Assert.True(created > 0, "no id returned: " + created);
                    c.Main.RefreshAfterDataChanged(new List<int> { created });
                    Pump();
                    window.UpdateLayout();
                    Assert.Equal(1, c.Main.Register.Rows.Count);
                }
                finally
                {
                    if (window != null)
                    {
                        window.Close();
                    }
                    if (c != null)
                    {
                        try { c.Db.Close(); }
                        catch (Exception) { }
                    }
                    TestDb.Wipe(home);
                }
            });
        }

        [Fact]
        public void DecisionThroughCoordinatorUpdatesGridRow()
        {
            StaRunner.Run(delegate
            {
                string home = TestDb.NewHome();
                Composition c = null;
                Window window = null;
                try
                {
                    c = CompositionRoot.Build(home, new FakeClock(), null, new NullLogger());
                    int id = c.DefendantService.Create(new DefendantInput
                    {
                        FullName = "postpone me",
                        CaseNumber = "6601",
                        CaseYear = "2026",
                        SessionDate = new LocalDate(2026, 10, 7)
                    }, Guid.NewGuid().ToString("N"), true).Id;
                    c.SearchIndex.Rebuild();
                    c.Main.Register.Filter = Filters.All;
                    c.Main.RefreshAll();
                    Assert.Equal(1, c.Main.Register.Rows.Count);
                    c.DecisionService.Decide(new DecisionRequest
                    {
                        RequestUuid = Guid.NewGuid().ToString("N"),
                        DefendantId = id,
                        ExpectedVersion = c.DefendantService.Get(id).RowVersion,
                        Code = DecisionCodes.Postpone,
                        NextDate = new LocalDate(2026, 10, 21)
                    });
                    c.Coordinator.MarkFor(id);
                    Pump();
                    Assert.Equal(1, c.Main.Register.Rows.Count);
                    Assert.Equal("21/10/2026", c.Main.Register.Rows[0].SessionLine1);
                }
                finally
                {
                    if (window != null)
                    {
                        window.Close();
                    }
                    if (c != null)
                    {
                        try { c.Db.Close(); }
                        catch (Exception) { }
                    }
                    TestDb.Wipe(home);
                }
            });
        }

        private static DecisionWizardDialog OpenWizard(DialogEnv env, int id)
        {
            var vm = new DecisionWizardViewModel(env.DecisionService, env.Undo,
                env.DefendantService, env.Lookups, env.Settings, env.Clock,
                new List<int> { id }, 0);
            return new DecisionWizardDialog(vm, null, env.Clock);
        }

        private static DefendantEditDialog OpenEdit(DialogEnv env, int id)
        {
            Defendant existing = id == 0 ? null : env.DefendantService.Get(id);
            var vm = new DefendantEditViewModel(env.DefendantService, env.Lookups, env.Clock, existing);
            return new DefendantEditDialog(vm, env.Lookups, env.Clock, null);
        }

        private static Window Show(Window w, int width, int height)
        {
            w.WindowState = WindowState.Normal;
            w.Width = width;
            w.Height = height;
            w.Left = -32000;
            w.Top = -32000;
            w.ShowActivated = false;
            w.Show();
            Pump();
            w.UpdateLayout();
            Pump();
            return w;
        }

        private static void Pump()
        {
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Loaded,
                new Action(delegate { }));
        }

        private static void CheckWindow(string tag, Window w, List<string> failures)
        {
            try
            {
                object bigCombo = w.FindResource("BigCombo");
                var all = new List<DependencyObject>();
                Flatten(w, all);
                foreach (DependencyObject o in all)
                {
                    ComboBox cb = o as ComboBox;
                    if (cb != null && !object.ReferenceEquals(cb.Style, bigCombo))
                    {
                        failures.Add(tag + ": ComboBox without BigCombo style at " + Chain(cb));
                    }
                    ScrollBar sb = o as ScrollBar;
                    if (sb != null && sb.ActualWidth > 1.0
                        && sb.Template.FindName("CourtThumb", sb) == null)
                    {
                        failures.Add(tag + ": ScrollBar with default template at " + Chain(sb));
                    }
                    CheckBox ch = o as CheckBox;
                    if (ch != null && ch.IsVisible && ch.Template.FindName("Box", ch) == null)
                    {
                        failures.Add(tag + ": CheckBox with default template at " + Chain(ch));
                    }
                    ToolTip tt = o as ToolTip;
                    if (tt != null && tt.IsVisible && tt.Template.FindName("TipBorder", tt) == null)
                    {
                        failures.Add(tag + ": ToolTip with default template at " + Chain(tt));
                    }
                    DatePicker dp = o as DatePicker;
                    if (dp != null && dp.IsVisible)
                    {
                        failures.Add(tag + ": DatePicker without custom template at " + Chain(dp));
                    }
                    DataGridRowHeader rh = o as DataGridRowHeader;
                    if (rh != null && rh.ActualWidth >= 1.0)
                    {
                        failures.Add(tag + ": DataGridRowHeader visibly leaking at " + Chain(rh));
                    }
                    DataGrid g = o as DataGrid;
                    if (g != null && g.HeadersVisibility != DataGridHeadersVisibility.Column)
                    {
                        failures.Add(tag + ": DataGrid headers must be Column-only");
                    }
                }
            }
            finally
            {
                w.Close();
            }
        }

        private static void Flatten(DependencyObject root, List<DependencyObject> outp)
        {
            var stack = new Stack<DependencyObject>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                DependencyObject cur = stack.Pop();
                outp.Add(cur);
                int n = VisualTreeHelper.GetChildrenCount(cur);
                for (int i = 0; i < n; i++)
                {
                    stack.Push(VisualTreeHelper.GetChild(cur, i));
                }
            }
        }

        private static string Chain(DependencyObject o)
        {
            string c = "";
            int hops = 0;
            while (o != null && hops < 4)
            {
                FrameworkElement f = o as FrameworkElement;
                if (f != null)
                {
                    string nm = string.IsNullOrEmpty(f.Name) ? f.GetType().Name
                        : f.GetType().Name + "#" + f.Name;
                    c = c == "" ? nm : nm + ">" + c;
                }
                o = VisualTreeHelper.GetParent(o);
                hops++;
            }
            return c;
        }
    }
}

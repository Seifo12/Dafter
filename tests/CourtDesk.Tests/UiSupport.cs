using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;
using CourtDesk.Persistence;
using CourtDesk.UI.ViewModels;

namespace CourtDesk.Tests
{
    internal static class StaRunner
    {
        private static bool _appReady;
        private static readonly object _lock = new object();

        public static void Run(Action body)
        {
            Exception error = null;
            var t = new Thread(delegate()
            {
                try
                {
                    EnsureApp();
                    body();
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }
                catch (Exception e)
                {
                    error = e;
                    try
                    {
                        Dispatcher.CurrentDispatcher.InvokeShutdown();
                    }
                    catch (Exception) { }
                }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
            if (!t.Join(120000))
            {
                throw new TimeoutException("STA work timed out");
            }
            if (error != null)
            {
                throw new InvalidOperationException("STA failed: " + error.Message, error);
            }
        }

        public static T Run<T>(Func<T> body)
        {
            T result = default(T);
            Run(delegate { result = body(); });
            return result;
        }

        private static void EnsureApp()
        {
            lock (_lock)
            {
                if (_appReady)
                {
                    return;
                }
                if (System.Windows.Application.Current == null)
                {
                    var app = new System.Windows.Application();
                    app.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
                }
                else
                {
                    System.Windows.Application.Current.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
                }
                string[] dicts = new string[]
                {
                    "UI/Styles/Colors.xaml",
                    "UI/Styles/Typography.xaml",
                    "UI/Styles/Icons.xaml",
                    "UI/Styles/Controls.xaml"
                };
                foreach (string d in dicts)
                {
                    var rd = new ResourceDictionary();
                    rd.Source = new Uri("pack://application:,,,/CourtDesk;component/" + d);
                    System.Windows.Application.Current.Resources.MergedDictionaries.Add(rd);
                }
                _appReady = true;
            }
        }
    }

    internal sealed class UiEnv : IDisposable
    {
        public string Home;
        public DbGateway Db;
        public FakeClock Clock;
        public SettingsService Settings;
        public IDefendantStore Defendants;
        public IEventStore Events;
        public SearchIndex Index;
        public TodayService Today;
        public RegisterViewModel Register;
        public TodaySidebarViewModel Sidebar;
        public MainViewModel Main;

        public UiEnv()
        {
            Home = TestDb.NewHome();
            Clock = new FakeClock();
            RecoveryResult rec = TestDb.Open(Home, Clock);
            Db = rec.Gateway;
            Defendants = new SqliteDefendantStore();
            Events = new SqliteEventStore();
            var stores = new SqliteSettingsStore();
            Settings = new SettingsService(Db, stores);
            Db.Write(s =>
            {
                SeedData.SeedLookups((DbContext)s,
                    CourtDesk.Resources.Strings.SeedCourts,
                    CourtDesk.Resources.Strings.SeedCustody,
                    CourtDesk.Resources.Strings.SeedReasons);
                return 0;
            });
            Index = new SearchIndex(Db, Defendants, Clock,
                new string[] { "d", "r", "j" });
            Index.Rebuild();
            Today = new TodayService(Db, Defendants, Clock, Settings);
            Register = new RegisterViewModel(Index, Today, null, null);
            Sidebar = new TodaySidebarViewModel(Today);
            Main = new MainViewModel(Settings, Clock, Register, Sidebar);
            Main.Index = Index;
        }

        public int AddDefendant(string name, string caseNumber, LocalDate? session)
        {
            var svc = new DefendantService(Db, Defendants, Events, Clock, Settings, new NullLogger());
            return svc.Create(new DefendantInput
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
}

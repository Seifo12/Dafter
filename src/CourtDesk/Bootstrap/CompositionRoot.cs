using System.Collections.Generic;
using System.Threading;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;
using CourtDesk.Persistence;
using CourtDesk.Platform;
using CourtDesk.Resources;
using CourtDesk.UI.ViewModels;

namespace CourtDesk.Bootstrap
{
    public sealed class Composition
    {
        public DbGateway Db;
        public IClock Clock;
        public string Home;
        public SettingsService Settings;
        public IDefendantStore Defendants;
        public IEventStore Events;
        public ILookupStore Lookups;
        public ISettingsStore SettingStore;
        public IMetaStore Meta;
        public DefendantService DefendantService;
        public DecisionService DecisionService;
        public UndoService UndoService;
        public TodayService TodayService;
        public ScheduleService ScheduleService;
        public SearchIndex SearchIndex;
        public LookupService LookupService;
        public BackupService BackupService;
        public RosterService RosterService;
        public ClockSanityService ClockSanity;
        public IntegrityService Integrity;
        public CsvExportService CsvExport;
        public RecoveryResult Recovery;
        public MainViewModel Main;
        public CourtDesk.UI.UiCoordinator Coordinator;
        public CourtDesk.Printing.PrintService PrintService;
        public CourtDesk.Application.ExcelTransferService Excel;
        public MedicalService Medical;
    }

    public static class CompositionRoot
    {
public static Composition Build(string home, IClock clock, SynchronizationContext marshal, ILogger log)
        {
            SynchronizationContext ui = marshal;
            if (ui == null)
            {
                ui = new System.Windows.Threading.DispatcherSynchronizationContext(
                    System.Windows.Threading.Dispatcher.CurrentDispatcher);
            }
            RecoveryResult rec = DatabaseRecovery.OpenWithRecovery(home, clock, ui);
            var db = rec.Gateway;
            var defendants = new SqliteDefendantStore();
            var events = new SqliteEventStore();
            var lookups = new SqliteLookupStore();
            var settingStore = new SqliteSettingsStore();
            var meta = new SqliteMetaStore();
            var settings = new SettingsService(db, settingStore);
            db.Write(s =>
            {
                SeedData.SeedLookups((DbContext)s, Strings.SeedCourts, Strings.SeedCustody, Strings.SeedReasons);
                return 0;
            });
            var engine = new SqliteBackupEngine(db, clock, home);
            var today = new TodayService(db, defendants, clock, settings);
            var index = new SearchIndex(db, defendants, clock,
                new string[] { Strings.StatusDetained, Strings.StatusReleased, Strings.StatusJudged });
            index.Rebuild();
            var backupPaths = BackupService.ForHome(home);
            var c = new Composition
            {
                Db = db,
                Clock = clock,
                Settings = settings,
                Defendants = defendants,
                Events = events,
                Lookups = lookups,
                SettingStore = settingStore,
                Meta = meta,
                Recovery = rec,
                DefendantService = new DefendantService(db, defendants, events, clock, settings, log),
                DecisionService = new DecisionService(db, defendants, events, clock, settings),
                UndoService = new UndoService(db, defendants, events, clock, settings),
                TodayService = today,
                ScheduleService = new ScheduleService(db, defendants, clock),
                SearchIndex = index,
                LookupService = new LookupService(db, lookups),
                BackupService = new BackupService(db, engine, new PortsDriveProvider(), meta, clock, settings, backupPaths, log),
                RosterService = new RosterService(db, defendants, events, clock, settings),
                ClockSanity = new ClockSanityService(db, clock, meta, BuildDate.Value),
                Integrity = new IntegrityService(db, engine, log),
CsvExport = new CsvExportService(db, defendants, clock),
                Medical = new MedicalService(new SqliteMedicalStore(db), home,
                    () => clock.UtcNow)
            };
            var register = new RegisterViewModel(index, today,
                delegate { return settings.Get(SettingKeys.LastFilter); },
                delegate(string v) { settings.Set(SettingKeys.LastFilter, v); });
            var sidebar = new TodaySidebarViewModel(today);
            c.Home = home;
c.Main = new MainViewModel(settings, clock, register, sidebar)
        {
            Schedule = new ScheduleViewModel(c.ScheduleService, clock),
            History = new HistoryViewModel(db, events, defendants),
            Index = index
        };
c.PrintService = new CourtDesk.Printing.PrintService(db,
                new CourtDesk.Persistence.SqliteRosterPrintLog(), c.RosterService);
            c.Excel = new CourtDesk.Application.ExcelTransferService(db, defendants,
                events, clock, c.DefendantService);
            c.Coordinator = new CourtDesk.UI.UiCoordinator(new CourtDesk.UI.UiCoordinator.CompositionLike
            {
                Defendants = c.DefendantService,
                Decisions = c.DecisionService,
                Undo = c.UndoService,
                Lookups = c.LookupService,
                Settings = settings,
                Schedule = c.ScheduleService,
                Today = c.TodayService,
                Clock = clock,
                Printing = c.PrintService,
                Rosters = c.RosterService,
                Medical = c.Medical,
                History = new CourtDesk.UI.ViewModels.HistoryViewModel(db, events, defendants)
            });
            db.DataChanged += delegate(long seq)
            {
                List<int> dirty = c.Coordinator.DrainDirtyIds();
                if (dirty.Count > 0)
                {
                    c.Main.RefreshAfterDataChanged(dirty);
                }
                else
                {
                    c.Main.RefreshAll();
                }
                RefreshStatus(c);
            };
            c.Coordinator.DirtyMarked += delegate(int id)
            {
                c.Main.RefreshAfterDataChanged(new List<int> { id });
                RefreshStatus(c);
            };
            RefreshStatus(c);
            return c;
        }

        public static void RefreshStatus(Composition c)
        {
            string records = c.Db.Read(s => c.Defendants.CountActive(s)).ToString(
                System.Globalization.CultureInfo.InvariantCulture);
            string exit = FormatExit(c);
            int age = c.BackupService.UsbAgeDays();
            string usb;
            if (age < 0)
            {
                usb = Strings.BkNeverUsb;
            }
            else if (c.BackupService.UsbReminderDue())
            {
                usb = string.Format(Strings.BkUsbOld, age);
            }
            else
            {
                usb = string.Format(Strings.BkLastUsb, age);
            }
            c.Main.SetStatus(records, exit, usb);
        }

        private static string FormatExit(Composition c)
        {
            string raw = c.BackupService.LastExitBackupUtc();
            if (string.IsNullOrEmpty(raw))
            {
                return string.Format(Strings.BkLastExit, "");
            }
            System.DateTime t;
            if (!System.DateTime.TryParseExact(raw, "yyyy-MM-ddTHH:mm:ss'Z'",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                out t))
            {
                return string.Format(Strings.BkLastExit, "");
            }
            return string.Format(Strings.BkLastExit,
                string.Format("{0:00}/{1:00}/{2}", t.Day, t.Month, t.Year));
        }
    }
}

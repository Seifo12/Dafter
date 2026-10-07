using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;
using CourtDesk.Persistence;
using Xunit;

namespace CourtDesk.Tests
{
    internal sealed class FixedClock : IClock
    {
        public LocalDate Today { get { return new LocalDate(2026, 10, 7); } }
        public DateTime UtcNow { get { return new DateTime(2026, 10, 7, 8, 0, 0, DateTimeKind.Utc); } }
    }

    [Trait("Category", "Screens")]
    public sealed class RenderScreenTests
    {
        private static string OutDir()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, "CourtDesk.sln")))
                {
                    return Path.Combine(dir, "artifacts", "screens");
                }
                dir = Path.GetDirectoryName(dir);
            }
            throw new InvalidOperationException("repo root not found");
        }

        private sealed class Seed
        {
            private readonly IDefendantStore _defendants;
            private readonly IEventStore _events;
            private readonly DefendantService _svc;
            private readonly DecisionService _decide;
            private readonly DbGateway _db;

            public Seed(DbGateway db, IDefendantStore defendants, IEventStore events,
                DefendantService svc, DecisionService decide)
            {
                _db = db;
                _defendants = defendants;
                _events = events;
                _svc = svc;
                _decide = decide;
            }

            public int Add(string name, string caseNumber, LocalDate? session)
            {
                return _svc.Create(new DefendantInput
                {
                    FullName = name,
                    CaseNumber = caseNumber,
                    CaseYear = "2026",
                    SessionDate = session
                }, Guid.NewGuid().ToString("N"), true).Id;
            }

            public void Mixed()
            {
                Add("محمد عبد الله السيد", "1001", new LocalDate(2026, 10, 7));
                Add("السيد حسن علي علي", "1002", new LocalDate(2026, 10, 7));
                Add("كريم فؤاد إبراهيم", "1003", new LocalDate(2026, 10, 1));
                int d = Add("طارق محمود حسن", "1004", new LocalDate(2026, 10, 7));
                Defendant row = _svc.Get(d);
                _decide.Decide(new DecisionRequest
                {
                    RequestUuid = Guid.NewGuid().ToString("N"),
                    DefendantId = d,
                    ExpectedVersion = row.RowVersion,
                    Code = DecisionCodes.RenewDetention,
                    NextDate = new LocalDate(2026, 10, 20)
                });
            }

            public void Bulk(int count)
            {
                _db.Write(s =>
                {
                    var store = new SqliteDefendantStore();
                    var evs = new SqliteEventStore();
                    for (int i = 0; i < count; i++)
                    {
                        var row = new Defendant
                        {
                            FullName = "متهم رقم " + i + " تجريبي",
                            CaseNumber = (300000 + i).ToString(),
                            CaseYear = 2026,
                            SessionDate = new LocalDate(2026, 10, 7).AddDays(i % 10 - 2),
                            Status = DefendantStatuses.Detained,
                            RowVersion = 0
                        };
                        int id = store.Insert((DbContext)s, row);
                        evs.Insert((DbContext)s, new SessionEvent
                        {
                            RequestUuid = Guid.NewGuid().ToString("N"),
                            DefendantId = id,
                            EventType = EventTypes.Created,
                            AfterJson = SnapshotJson.Write(DefendantSnapshot.FromDefendant(row)),
                            OperatorName = "x",
                            OccurredAtUtc = "2026-10-07T08:00:00Z",
                            OccurredLocalDate = "2026-10-07"
                        });
                    }
                    return 0;
                });
            }
        }

        [Fact]
        public void RenderAllScreens()
        {
            StaRunner.Run(delegate
            {
                string outDir = OutDir();
                Directory.CreateDirectory(outDir);
                Render(outDir, "reg-0-1366", 1366, 768, 100, delegate(Seed s) { });
                Render(outDir, "reg-3-1366", 1366, 768, 100, delegate(Seed s) { s.Mixed(); });
                Render(outDir, "reg-200-1366", 1366, 768, 100, delegate(Seed s) { s.Bulk(200); });
                Render(outDir, "reg-3-1024", 1024, 768, 100, delegate(Seed s) { s.Mixed(); });
                Render(outDir, "reg-3-150", 1366, 768, 150, delegate(Seed s) { s.Mixed(); });
            });
        }

        private static void Render(string outDir, string name, int w, int h, int scale, Action<Seed> seed)
        {
            string home = TestDb.NewHome();
            try
            {
                var clock = new FixedClock();
                RecoveryResult rec = TestDb.Open(home, clock);
                DbGateway db = rec.Gateway;
                var defendants = new SqliteDefendantStore();
                var events = new SqliteEventStore();
                var settings = new SettingsService(db, new SqliteSettingsStore());
                db.Write(s =>
                {
                    SeedData.SeedLookups((DbContext)s,
                        CourtDesk.Resources.Strings.SeedCourts,
                        CourtDesk.Resources.Strings.SeedCustody,
                        CourtDesk.Resources.Strings.SeedReasons);
                    return 0;
                });
                var svc = new DefendantService(db, defendants, events, clock, settings, new NullLogger());
                var decide = new DecisionService(db, defendants, events, clock, settings);
                var s = new Seed(db, defendants, events, svc, decide);
                seed(s);
                var index = new SearchIndex(db, defendants, clock,
                    new string[] { CourtDesk.Resources.Strings.StatusDetained, CourtDesk.Resources.Strings.StatusReleased, CourtDesk.Resources.Strings.StatusJudged });
                index.Rebuild();
                var today = new TodayService(db, defendants, clock, settings);
                var register = new CourtDesk.UI.ViewModels.RegisterViewModel(index, today,
                    delegate { return settings.Get(SettingKeys.LastFilter); },
                    delegate(string v) { settings.Set(SettingKeys.LastFilter, v); });
                var sidebar = new CourtDesk.UI.ViewModels.TodaySidebarViewModel(today);
                settings.Set(SettingKeys.FontScale, scale.ToString());
                var main = new CourtDesk.UI.ViewModels.MainViewModel(settings, clock, register, sidebar);
                main.RefreshAll();
                main.SetStatus(
                    db.Read(x => ((CourtDesk.Application.Ports.IDefendantStore)defendants).CountActive(x)).ToString(),
                    "15/09/2026",
                    "لسه ماحفظتش نسخة على الفلاشة.");

                var window = new CourtDesk.MainWindow(main);
                window.WindowState = WindowState.Normal;
                window.Width = w;
                window.Height = h;
                window.Left = -32000;
                window.Top = -32000;
                window.ShowActivated = false;
                window.Show();
                window.UpdateLayout();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Loaded,
                    new Action(delegate { }));
                window.UpdateLayout();
                var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(window);
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bmp));
                using (var fs = new FileStream(Path.Combine(outDir, name + ".png"), FileMode.Create))
                {
                    enc.Save(fs);
                }
                window.Close();
                db.Close();
            }
            finally
            {
                TestDb.Wipe(home);
            }
        }
    }
}
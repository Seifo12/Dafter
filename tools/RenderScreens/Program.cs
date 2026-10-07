using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CourtDesk;
using CourtDesk.Application;
using CourtDesk.Bootstrap;
using CourtDesk.Domain;
using CourtDesk.Persistence;
using CourtDesk.Resources;

namespace RenderScreens
{
    internal static class Program
    {
        private static string _out;

        [STAThread]
        private static int Main(string[] args)
        {
            _out = args.Length > 0 ? args[0] : DefaultOut();
            Directory.CreateDirectory(_out);
            var app = new Application();
            foreach (string d in new string[] { "UI/Styles/Colors.xaml", "UI/Styles/Typography.xaml", "UI/Styles/Icons.xaml", "UI/Styles/Controls.xaml" })
            {
                var rd = new ResourceDictionary();
                rd.Source = new Uri("pack://application:,,,/CourtDesk;component/" + d);
                app.Resources.MergedDictionaries.Add(rd);
            }

            Shot("reg-0-1366", 1366, 768, 100, delegate(Seed s) { });
            Shot("reg-3-1366", 1366, 768, 100, delegate(Seed s) { s.Mixed(); });
            Shot("reg-200-1366", 1366, 768, 100, delegate(Seed s) { s.Bulk(200); });
            Shot("reg-3-1024", 1024, 768, 100, delegate(Seed s) { s.Mixed(); });
            Shot("reg-3-150", 1366, 768, 150, delegate(Seed s) { s.Mixed(); });
            Console.WriteLine("screens in " + _out);
            return 0;
        }

        private static string DefaultOut()
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
            return "screens";
        }

        private static void Shot(string name, int w, int h, int scale, Action<Seed> seed)
        {
            string home = Path.Combine(Path.GetTempPath(), "cdr-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(home);
            try
            {
                var clock = new FakeClock();
                Composition c = CompositionRoot.Build(home, clock, null, new CourtDesk.Application.Ports.NullLogger());
                if (scale != 100)
                {
                    c.Settings.Set(SettingKeys.FontScale, scale.ToString());
                    var register = new CourtDesk.UI.ViewModels.RegisterViewModel(c.SearchIndex, c.TodayService,
                        delegate { return c.Settings.Get(SettingKeys.LastFilter); },
                        delegate(string v) { c.Settings.Set(SettingKeys.LastFilter, v); });
                    var sidebar = new CourtDesk.UI.ViewModels.TodaySidebarViewModel(c.TodayService);
                    c.Main = new CourtDesk.UI.ViewModels.MainViewModel(c.Settings, clock, register, sidebar);
                }
                seed(new Seed(c));
                c.SearchIndex.Rebuild();
                c.Main.RefreshAll();
                var window = new MainWindow(c.Main);
                window.WindowState = WindowState.Normal;
                window.Width = w;
                window.Height = h;
                window.Measure(new Size(w, h));
                window.Arrange(new Rect(0, 0, w, h));
                window.UpdateLayout();
                var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(window);
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bmp));
                using (var fs = new FileStream(Path.Combine(_out, name + ".png"), FileMode.Create))
                {
                    enc.Save(fs);
                }
                window.Close();
                c.Db.Close();
            }
            finally
            {
                try { Directory.Delete(home, true); }
                catch (Exception) { }
            }
        }

        private sealed class FakeClock : IClock
        {
            public LocalDate Today { get { return new LocalDate(2026, 10, 7); } }
            public DateTime UtcNow { get { return new DateTime(2026, 10, 7, 8, 0, 0, DateTimeKind.Utc); } }
        }

        private sealed class Seed
        {
            private readonly Composition _c;
            private int _n;

            public Seed(Composition c)
            {
                _c = c;
            }

            public void Mixed()
            {
                Add("محمد عبد الله السيد", "1001", new LocalDate(2026, 10, 7));
                Add("السيد حسن علي", "1002", new LocalDate(2026, 10, 7));
                int o = Add("كريم فؤاد إبراهيم", "1003", new LocalDate(2026, 10, 1));
                int d = Add("طارق محمود حسن", "1004", new LocalDate(2026, 10, 7));
                Defendant row = _c.DefendantService.Get(d);
                _c.DecisionService.Decide(new DecisionRequest
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
                _c.Db.Write(s =>
                {
                    var store = new SqliteDefendantStore();
                    var events = new SqliteEventStore();
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
                        events.Insert((DbContext)s, new SessionEvent
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

            private int Add(string name, string caseNumber, LocalDate? session)
            {
                _n++;
                return _c.DefendantService.Create(new DefendantInput
                {
                    FullName = name,
                    CaseNumber = caseNumber,
                    CaseYear = "2026",
                    SessionDate = session
                }, Guid.NewGuid().ToString("N"), true).Id;
            }
        }
    }
}

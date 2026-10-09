using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;
using CourtDesk.Persistence;
using CourtDesk.UI.Dialogs;
using CourtDesk.UI.ViewModels;

namespace CourtDesk.Bootstrap
{
    public static class ScreenshotMode
    {
        public static int Run(string outDir, string homeArg)
        {
            string home = string.IsNullOrEmpty(homeArg)
                ? Path.Combine(Path.GetTempPath(),
                    "courtdesk-shots-" + Guid.NewGuid().ToString("N"))
                : homeArg;
            _stageFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "courtdesk-shots-stage.log");
            Stage("dirs");
            try
            {
                Directory.CreateDirectory(outDir);
                Directory.CreateDirectory(home);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("FAIL dirs: " + e.Message);
                return 1;
            }
                Stage("culture");
                var culture = new CultureInfo("ar-EG");
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;
                Stage("app");
                var app = new System.Windows.Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            foreach (string d in new string[]
            {
                "UI/Styles/Colors.xaml",
                "UI/Styles/Typography.xaml",
                "UI/Styles/Icons.xaml",
                "UI/Styles/Controls.xaml"
            })
            {
                var rd = new ResourceDictionary();
                rd.Source = new Uri("pack://application:,,,/CourtDesk;component/" + d);
                System.Windows.Application.Current.Resources.MergedDictionaries.Add(rd);
            }
            try
            {
                Stage("native");
                NativeExtractReport native = NativeExtractor.Extract(home);
                if (!native.Ok)
                {
                    Console.Error.WriteLine("FAIL native: " + native.Detail);
                    return 2;
                }
                Stage("build");
                Composition c = CompositionRoot.Build(home, new SystemClock(),
                    SynchronizationContext.Current, new NullLogger());
                try
                {
                    Stage("seed");
                    Seed(c);
                    Stage("refresh");
                    c.Main.RefreshAll();
                    Stage("mainwindow");
                    var main = new MainWindow(c.Main, c);
                    try
                    {
                        Show(main, 1366, 768);
                        Save(main, Path.Combine(outDir, "r2-main-1366.png"), 1366, 768);
                        Show(main, 1024, 768);
                        Save(main, Path.Combine(outDir, "r2-main-1024.png"), 1024, 768);
                    }
                    finally
                    {
                        main.Close();
                    }
                    Step("wizard", delegate { ShootWizard(c, outDir); });
                    Step("edit", delegate { ShootDialog(c, outDir, "r2-edit.png", OpenEdit(c)); });
                    Step("backup", delegate
                    {
                        ShootDialog(c, outDir, "r2-backup.png",
                            new UsbBackupDialog(c.BackupService, c.Settings));
                    });
                    Step("preview", delegate
                    {
                        RosterModel model = c.RosterService.Build(c.Clock.Today);
                        ShootDialog(c, outDir, "r2-preview.png",
                            new PrintPreviewWindow(c.PrintService.BuildDocument(model)));
                    });
                }
                finally
                {
                    try { c.Db.Close(); }
                    catch (Exception) { }
                }
                Console.WriteLine("screenshots ok in " + outDir);
                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("FAIL " + e.GetType().Name + ": " + e.Message);
                Console.Error.WriteLine(e.StackTrace);
                return 9;
            }
            finally
            {
                app.Shutdown();
            }
        }

        private static string _stageFile;

        private static void Stage(string name)
        {
            Console.Error.WriteLine("stage: " + name);
            Console.Error.Flush();
            try
            {
                File.AppendAllText(_stageFile, name + Environment.NewLine);
            }
            catch (Exception) { }
        }

        private static void Seed(Composition c)
        {
            c.Db.Write(s =>
            {
                var store = new SqliteDefendantStore();
                var evs = new SqliteEventStore();
                LocalDate baseDay = c.Clock.Today;
                for (int i = 0; i < 200; i++)
                {
                    var row = new Defendant
                    {
                        FullName = "test " + i,
                        CaseNumber = (300000 + i).ToString(),
                        CaseYear = 2026,
                        SessionDate = baseDay.AddDays(i % 10 - 2),
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
                        OperatorName = "shots",
                        OccurredAtUtc = "2026-10-07T08:00:00Z",
                        OccurredLocalDate = "2026-10-07"
                    });
                }
                return 0;
            });
            c.SearchIndex.Rebuild();
            int done = 0;
            foreach (DefendantRow r in c.Db.Read(s => c.Defendants.ListActiveRows(s)))
            {
                if (done >= 3 || r.D.SessionDate != c.Clock.Today)
                {
                    continue;
                }
                Defendant d = c.DefendantService.Get(r.D.Id);
                c.DecisionService.Decide(new DecisionRequest
                {
                    RequestUuid = Guid.NewGuid().ToString("N"),
                    DefendantId = d.Id,
                    ExpectedVersion = d.RowVersion,
                    Code = DecisionCodes.RenewDetention,
                    NextDate = c.Clock.Today.AddDays(13)
                });
                done++;
            }
            c.SearchIndex.Rebuild();
        }

        private static void Step(string name, Action body)
        {
            Stage(name);
            try
            {
                body();
            }
            catch (Exception e)
            {
                Stage("ERROR " + name + ": " + e.GetType().Name + ": " + e.Message
                    + " | " + (e.StackTrace ?? "").Replace("\r", " ").Replace("\n", " "));
            }
        }

        private static int FirstPendingId(Composition c)
        {
            foreach (DefendantRow r in c.TodayService.Snapshot().Pending)
            {
                return r.D.Id;
            }
            return 0;
        }

        private static void ShootWizard(Composition c, string outDir)
        {
            int id = FirstPendingId(c);
            var vm = new DecisionWizardViewModel(c.DecisionService, c.UndoService,
                c.DefendantService, c.LookupService, c.Settings, c.Clock,
                new List<int> { id }, 0);
            var dlg = new DecisionWizardDialog(vm, new PopupHost(), c.Clock);
            try
            {
                Show(dlg, 960, 740);
                Save(dlg, Path.Combine(outDir, "r2-wiz-1.png"), 960, 740);
                vm.Choose(DecisionCodes.RenewDetention);
                Pump();
                Save(dlg, Path.Combine(outDir, "r2-wiz-2.png"), 960, 740);
                vm.PickOffset(7);
                vm.NextCommand.Execute(null);
                Pump();
                Save(dlg, Path.Combine(outDir, "r2-wiz-3.png"), 960, 740);
                Step("wiz-save", delegate { vm.Save(); });
                Pump();
                Step("wiz-done-shot", delegate
                {
                    Save(dlg, Path.Combine(outDir, "r2-wiz-done.png"), 960, 740);
                });
            }
            finally
            {
                dlg.Close();
            }
        }

        private static DefendantEditDialog OpenEdit(Composition c)
        {
            var vm = new DefendantEditViewModel(c.DefendantService, c.LookupService, c.Clock, null);
            var dlg = new DefendantEditDialog(vm, c.LookupService, c.Clock, new PopupHost());
            dlg.AttachMedical(c.Medical);
            return dlg;
        }

        private static void ShootDialog(Composition c, string outDir, string name, Window dlg)
        {
            try
            {
                int w = (int)dlg.Width;
                int h = (int)dlg.Height;
                if (double.IsNaN(w) || w <= 0)
                {
                    w = 900;
                }
                if (double.IsNaN(h) || h <= 0)
                {
                    h = 700;
                }
                Show(dlg, w, h);
                Save(dlg, Path.Combine(outDir, name), w, h);
            }
            finally
            {
                dlg.Close();
            }
        }

        private static void Show(Window w, int width, int height)
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
        }

        private static void Pump()
        {
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Loaded,
                new Action(delegate { }));
        }

        private static void Save(Visual v, string path, int width, int height)
        {
            var bmp = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(v);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using (var fs = new FileStream(path, FileMode.Create))
            {
                enc.Save(fs);
            }
            Console.WriteLine("shot " + Path.GetFileName(path));
        }
    }
}

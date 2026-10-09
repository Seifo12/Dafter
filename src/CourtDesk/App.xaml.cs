using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Bootstrap;
using CourtDesk.Domain;
using CourtDesk.Persistence;
using CourtDesk.Platform;
using CourtDesk.UI.Dialogs;
using CourtDesk.UI.Shell;

namespace CourtDesk
{
    public partial class App : System.Windows.Application
    {
        private Composition _composition;
        private SingleInstanceGuard _guard;
        private FileLogger _logger;
        private BackupScheduler _scheduler;
        private PowerEvents _power;
        private Action<BackupCandidate> _restore;

        protected override void OnStartup(System.Windows.StartupEventArgs e)
        {
            RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
            base.OnStartup(e);
            Thread.CurrentThread.CurrentCulture = ArabicCulture.Value;
            Thread.CurrentThread.CurrentUICulture = ArabicCulture.Value;
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = ArabicCulture.Value;
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = ArabicCulture.Value;

            string home = null;
            foreach (string a in e.Args)
            {
                if (a == "--home")
                {
                    home = "next";
                }
                else if (home == "next")
                {
                    home = a;
                }
            }
            string dir = DbPaths.ResolveHome(home == "next" ? null : home);
            NativeExtractReport native = NativeExtractor.Extract(dir);
            if (!native.Ok)
            {
                MessageBox.Show(CourtDesk.Resources.Strings.BusyOther,
                    CourtDesk.Resources.Strings.AppTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(6);
                return;
            }
            _guard = new SingleInstanceGuard();
            if (!_guard.IsPrimary)
            {
                Shutdown(5);
                return;
            }
            _logger = new FileLogger(dir);
            new CrashHandler(this, _logger).Install();
            EnsureStyleDictionaries();
            var splash = new SplashWindow();
            splash.Show();
            splash.ContentRendered += delegate(object s, System.EventArgs ev)
            {
                BuildAndShow(dir, splash);
            };
        }

        private ILogger Log()
        {
            return (ILogger)_logger ?? (ILogger)new NullLogger();
        }

        private static void EnsureStyleDictionaries()
        {
            string[] dicts = new string[]
            {
                "UI/Styles/Colors.xaml",
                "UI/Styles/Typography.xaml",
                "UI/Styles/Icons.xaml",
                "UI/Styles/Controls.xaml"
            };
            foreach (string d in dicts)
            {
                bool present = false;
                foreach (System.Windows.ResourceDictionary m in
                    System.Windows.Application.Current.Resources.MergedDictionaries)
                {
                    if (m.Source != null && m.Source.OriginalString.EndsWith(d))
                    {
                        present = true;
                        break;
                    }
                }
                if (present)
                {
                    continue;
                }
                var rd = new System.Windows.ResourceDictionary();
                rd.Source = new System.Uri(
                    "pack://application:,,,/CourtDesk;component/" + d);
                System.Windows.Application.Current.Resources.MergedDictionaries.Add(rd);
            }
        }

        private void BuildAndShow(string dir, SplashWindow splash)
        {
            Composition c;
            try
            {
                var marshal = SynchronizationContext.Current;
                c = CompositionRoot.Build(dir, new SystemClock(), marshal, Log());
            }
            catch (BusyException)
            {
                splash.Close();
                MessageBox.Show(CourtDesk.Resources.Strings.BusyOther, CourtDesk.Resources.Strings.AppTitle,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown(2);
                return;
            }
            catch (UnsupportedVersionException)
            {
                splash.Close();
                MessageBox.Show(CourtDesk.Resources.Strings.DbNewer, CourtDesk.Resources.Strings.AppTitle,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown(3);
                return;
            }
            catch (NoBackupException)
            {
                splash.Close();
                MessageBox.Show(CourtDesk.Resources.Strings.RecoverNone, CourtDesk.Resources.Strings.AppTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(4);
                return;
            }
            _composition = c;
            var main = new MainWindow(c.Main, c);
            InputLanguageAssist.ApplyTo(main);
            WirePlatform(c, dir);
            main.Closed += delegate(object s, System.EventArgs ev)
            {
                ExitBackup();
                try
                {
                    c.Db.Close();
                }
                finally
                {
                    Shutdown(0);
                }
            };
            c.Main.CloseRequested += delegate { main.Close(); };
            splash.Close();
            main.Show();
            ShowRecoveryNotice(c);
            CheckClock(c, main);
        }

        private void WirePlatform(Composition c, string dir)
        {
            _scheduler = new BackupScheduler(c.BackupService, Log(), null);
            _scheduler.AutoMinutes = 30;
            _scheduler.Start();
            _power = new PowerEvents(Log(), delegate { ExitBackup(); });
            _power.Install();
            _restore = delegate(BackupCandidate cand)
            {
                try
                {
                    if (_scheduler != null)
                    {
                        _scheduler.Stop();
                    }
                    if (_composition != null)
                    {
                        _composition.Db.Close();
                    }
                    RestoreSwap.Install(DbPaths.Db(dir), DbPaths.RestoreMarker(dir),
                        DbPaths.PreOpDir(dir), cand.Path, new SystemClock());
                    Log().Info("restore.installed", cand.Path);
                    MessageBox.Show(CourtDesk.Resources.Strings.BkRestoreSafe,
                        CourtDesk.Resources.Strings.AppTitle,
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    Shutdown(0);
                }
                catch (Exception e)
                {
                    Log().Error("restore.install", e.Message);
                    MessageBox.Show(CourtDesk.Resources.Strings.BkUsbFailUnknown,
                        CourtDesk.Resources.Strings.AppTitle,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            c.Main.HelpRequested += delegate { new HelpWindow().ShowDialog(); };
            c.Main.BackupRequested += delegate { OpenBackupSettings(c, dir); };
        }

        private void OpenBackupSettings(Composition c, string dir)
        {
            var view = new BackupSettingsView(c.BackupService, c.Settings, dir);
            view.RestoreRequested = _restore;
            view.Excel = c.Excel;
            view.RowsProvider = delegate
            {
                c.Main.Register.Filter = CourtDesk.Application.Filters.All;
                c.Main.Register.Refresh();
                return c.Main.Register.Rows;
            };
            view.ScopedRowsProvider = delegate(string scope)
            {
                c.Main.Register.Filter = string.IsNullOrEmpty(scope)
                    ? CourtDesk.Application.Filters.All
                    : scope;
                c.Main.Register.Refresh();
                return c.Main.Register.Rows;
            };
            view.ImportDone = delegate(int touched)
            {
                if (touched > 0)
                {
                    c.Main.Index.Rebuild();
                    c.Main.RefreshAll();
                }
            };
            view.ScopePrintRequested = delegate(bool preview, string scope)
            {
                if (c.Coordinator != null)
                {
                    c.Coordinator.PrintScope(preview, scope);
                }
            };
            view.OfficerSaved = delegate
            {
                if (c.Main != null)
                {
                    c.Main.RefreshAll();
                }
            };
            view.ShowDialog();
        }

        private void ExitBackup()
        {
            try
            {
                if (_scheduler != null)
                {
                    _scheduler.RunExitSnapshotNow();
                }
            }
            catch (Exception)
            {
            }
        }

        private void ShowRecoveryNotice(Composition c)
        {
            try
            {
                RecoveryResult rec = c.Recovery;
                if (rec == null)
                {
                    return;
                }
                if (!rec.WasRecovered && rec.InvariantWarnings.Count == 0)
                {
                    if (rec.WasUnclean)
                    {
                        Log().Info("recovery.silent-unclean", c.Home);
                    }
                    return;
                }
                string detail = rec.WasRecovered
                    ? (rec.BackupTimeIso ?? "")
                    : string.Join(" | ", rec.InvariantWarnings.ToArray());
                MessageBox.Show(
                    string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        CourtDesk.Resources.Strings.RecoverNotice,
                        DbPaths.QuarantineDir(c.Home), detail),
                    CourtDesk.Resources.Strings.AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception)
            {
            }
        }

        private void CheckClock(Composition c, Window owner)
        {
            try
            {
                ClockVerdict verdict = c.ClockSanity.Check();
                if (verdict != null && verdict.Suspect)
                {
                    ClockWarningDialog.ShowFor(owner, verdict);
                }
            }
            catch (Exception)
            {
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ExitBackup();
            if (_scheduler != null)
            {
                _scheduler.Stop();
            }
            if (_power != null)
            {
                _power.Uninstall();
            }
            if (_composition != null)
            {
                try
                {
                    _composition.Db.Close();
                }
                catch (System.Exception)
                {
                }
            }
            if (_guard != null)
            {
                _guard.Dispose();
            }
            base.OnExit(e);
        }
    }
}
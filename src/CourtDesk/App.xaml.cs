using System.Threading;
using System.Windows;
using System.Windows.Media;
using CourtDesk.Application.Ports;
using CourtDesk.Bootstrap;
using CourtDesk.Domain;
using CourtDesk.Persistence;
using CourtDesk.UI.Shell;

namespace CourtDesk
{
    public partial class App : System.Windows.Application
    {
        private Composition _composition;

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
            var splash = new SplashWindow();
            splash.Show();
            splash.ContentRendered += delegate(object s, System.EventArgs ev)
            {
                BuildAndShow(dir, splash);
            };
        }

        private void BuildAndShow(string dir, SplashWindow splash)
        {
            Composition c;
            try
            {
                var marshal = SynchronizationContext.Current;
                c = CompositionRoot.Build(dir, new SystemClock(), marshal, new NullLogger());
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
            var main = new MainWindow(c.Main);
            main.Closed += delegate(object s, System.EventArgs ev)
            {
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
        }

        protected override void OnExit(ExitEventArgs e)
        {
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
            base.OnExit(e);
        }
    }
}

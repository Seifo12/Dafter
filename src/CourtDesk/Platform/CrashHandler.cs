using System;
using System.Windows;
using System.Windows.Threading;
using CourtDesk.Application.Ports;

namespace CourtDesk.Platform
{
    public sealed class CrashHandler
    {
        private readonly ILogger _log;
        private System.Windows.Application _app;

        public CrashHandler(System.Windows.Application app, ILogger log)
        {
            _app = app;
            _log = log;
        }

        public bool LogOnly { get; set; }

        public void Install()
        {
            AppDomain.CurrentDomain.UnhandledException += OnDomain;
            System.Windows.Threading.Dispatcher.CurrentDispatcher.UnhandledException += OnDispatcher;
        }

        private void OnDomain(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            try
            {
                _log.Error("crash.unhandled", ex == null ? "unknown" : ex.ToString());
            }
            catch (Exception)
            {
            }
        }

        private void OnDispatcher(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                _log.Error("crash.dispatcher", e.Exception == null ? "unknown" : e.Exception.ToString());
            }
            catch (Exception)
            {
            }
            e.Handled = true;
            try
            {
                MessageBox.Show(
                    CourtDesk.Resources.Strings.CrashUserMessage,
                    CourtDesk.Resources.Strings.AppTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception)
            {
            }
            if (_app != null)
            {
                try
                {
                    _app.Shutdown(9);
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
using System;
using System.Windows;
using Microsoft.Win32;
using CourtDesk.Application.Ports;

namespace CourtDesk.Platform
{
    public sealed class PowerEvents
    {
        private readonly ILogger _log;
        private readonly Action _onSessionEnding;

        public PowerEvents(ILogger log, Action onSessionEnding)
        {
            _log = log;
            _onSessionEnding = onSessionEnding;
        }

        public void Install()
        {
            Microsoft.Win32.SystemEvents.SessionEnding += OnSessionEnding;
            Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerMode;
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplay;
        }

        public void Uninstall()
        {
            try
            {
                Microsoft.Win32.SystemEvents.SessionEnding -= OnSessionEnding;
                Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerMode;
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplay;
            }
            catch (Exception)
            {
            }
        }

        private void OnSessionEnding(object sender, Microsoft.Win32.SessionEndingEventArgs e)
        {
            try
            {
                _log.Info("power.session_ending", e.Reason.ToString());
            }
            catch (Exception)
            {
            }
            Run();
        }

        private void OnPowerMode(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
        {
            try
            {
                _log.Info("power.mode", e.Mode.ToString());
            }
            catch (Exception)
            {
            }
        }

        private void OnDisplay(object sender, EventArgs e)
        {
            try
            {
                _log.Info("power.display", "");
            }
            catch (Exception)
            {
            }
        }

        private void Run()
        {
            if (_onSessionEnding == null)
            {
                return;
            }
            try
            {
                _onSessionEnding();
            }
            catch (Exception)
            {
            }
        }

        public void SessionEndingNow()
        {
            Run();
        }
    }
}
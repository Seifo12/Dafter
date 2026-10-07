using System;
using System.Globalization;
using System.Windows;
using CourtDesk.Application;
using CourtDesk.Resources;

namespace CourtDesk.UI.Dialogs
{
    public partial class BackupSettingsView : Window
    {
        private readonly BackupService _backups;
        private readonly SettingsService _settings;
        private readonly string _home;

        public BackupSettingsView(BackupService backups, SettingsService settings, string home)
        {
            InitializeComponent();
            _backups = backups;
            _settings = settings;
            _home = home;
            Reload();
        }

        public System.Action<BackupCandidate> RestoreRequested;

        private void Reload()
        {
            string last = _settings.Get(SettingKeys.LastUsbBackupUtc);
            LastUsbText.Text = string.IsNullOrEmpty(last)
                ? Strings.BkNeverUsb
                : string.Format(CultureInfo.InvariantCulture, Strings.BkLastUsb, last);
            DirectToggle.Content = _settings.Get(SettingKeys.PrintDirect) == "0"
                ? Strings.SettingsDirectPrint + " - " + Strings.FormNo
                : Strings.SettingsDirectPrint + " - " + Strings.FormYes;
            OffsetText.Text = _settings.Get(SettingKeys.PostponeOffsets);
        }

        private void DirectClick(object sender, RoutedEventArgs e)
        {
            _settings.Set(SettingKeys.PrintDirect,
                _settings.Get(SettingKeys.PrintDirect) == "0" ? "1" : "0");
            Reload();
        }

        private void OffsetMinus(object sender, RoutedEventArgs e)
        {
            Shift(-1);
        }

        private void OffsetPlus(object sender, RoutedEventArgs e)
        {
            Shift(1);
        }

        private void Shift(int step)
        {
            int current;
            if (!int.TryParse(_settings.Get(SettingKeys.PostponeOffsets), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out current))
            {
                current = 0;
            }
            current = Math.Max(-90, Math.Min(365, current + step));
            _settings.Set(SettingKeys.PostponeOffsets,
                current.ToString(CultureInfo.InvariantCulture));
            Reload();
        }

        private void UsbClick(object sender, RoutedEventArgs e)
        {
            var d = new UsbBackupDialog(_backups, _settings);
            d.ShowDialog();
            Reload();
        }

        private void RestoreClick(object sender, RoutedEventArgs e)
        {
            var d = new RestoreDialog(_home, _backups);
            if (d.ShowDialog() == true && d.Chosen != null && RestoreRequested != null)
            {
                RestoreRequested(d.Chosen);
            }
            Reload();
        }

        private void CloseClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
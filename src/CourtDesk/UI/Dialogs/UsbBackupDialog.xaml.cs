using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Resources;

namespace CourtDesk.UI.Dialogs
{
    public partial class UsbBackupDialog : Window
    {
        private readonly BackupService _backups;
        private readonly SettingsService _settings;
        private readonly CourtDesk.Domain.IClock _clock;
        private DriveItem _chosen;

        public UsbBackupDialog(BackupService backups, SettingsService settings)
            : this(backups, settings, new CourtDesk.Domain.SystemClock())
        {
        }

        public UsbBackupDialog(BackupService backups, SettingsService settings, CourtDesk.Domain.IClock clock)
        {
            InitializeComponent();
            _backups = backups;
            _settings = settings;
            _clock = clock;
            Reload();
        }

        public UsbExportResult Result { get; private set; }

        public string StatusLine { get; private set; }

        private void Reload()
        {
            DriveList.Items.Clear();
            DriveItem[] drives;
            try
            {
                drives = _backups.RemovableDrives();
            }
            catch (Exception)
            {
                drives = new DriveItem[0];
            }
            foreach (DriveItem d in drives)
            {
                DriveList.Items.Add(Describe(d));
            }
            InsertText.Text = drives.Length == 0
                ? Strings.BkUsbInsert
                : Strings.BkUsbChoose;
            string last = _settings.Get(SettingKeys.LastUsbBackupUtc);
            LastUsbText.Text = string.IsNullOrEmpty(last)
                ? Strings.BkNeverUsb
                : string.Format(CultureInfo.InvariantCulture, Strings.BkLastUsb, last);
        }

        public static string Describe(DriveItem d)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} ({1})",
                string.IsNullOrEmpty(d.VolumeLabel) ? d.Name : d.VolumeLabel,
                FreeText(d.FreeBytes));
        }

        public static string FreeText(long bytes)
        {
            if (bytes <= 0)
            {
                return "0 MB";
            }
            return (bytes / (1024.0 * 1024.0)).ToString("0", CultureInfo.InvariantCulture) + " MB";
        }

        private void RetryClick(object sender, RoutedEventArgs e)
        {
            Reload();
        }

        private void SaveClick(object sender, RoutedEventArgs e)
        {
            int index = DriveList.SelectedIndex;
            if (index < 0)
            {
                StatusLine = Strings.BkUsbInsert;
                InsertText.Text = StatusLine;
                return;
            }
            DriveItem[] drives = _backups.RemovableDrives();
            if (index >= drives.Length)
            {
                Reload();
                return;
            }
            _chosen = drives[index];
            Result = _backups.ExportUsb(_chosen);
            if (Result != null && Result.Ok)
            {
_settings.Set(SettingKeys.LastUsbBackupUtc, _clock.Today.ToIso());
                DialogResult = true;
                return;
            }
            StatusLine = Result == null
                ? Strings.BkUsbFailUnknown
                : string.Format(CultureInfo.InvariantCulture, Strings.BkUsbFail,
                    FailureText(Result));
            InsertText.Text = StatusLine;
        }

        public static string FailureText(UsbExportResult r)
        {
            switch (r.Failure)
            {
                case UsbFailure.Removed:
                    return Strings.BkUsbFailRemoved;
                case UsbFailure.Full:
                    return Strings.BkUsbFailFull;
                case UsbFailure.ReadOnly:
                    return Strings.BkUsbFailRo;
                default:
                    return Strings.BkUsbFailUnknown;
            }
        }

        private void CloseClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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

        public System.Action<int> ImportDone;

        public System.Action<bool, string> ScopePrintRequested;

        private System.Collections.Generic.IReadOnlyList<CourtDesk.UI.ViewModels.RegisterRow> ScopeRows()
        {
            if (ScopedRowsProvider != null)
            {
                return ScopedRowsProvider(_scope);
            }
            return RowsProvider == null ? null : RowsProvider();
        }

        private void ScopePreviewClick(object sender, RoutedEventArgs e)
        {
            if (ScopePrintRequested != null)
            {
                ScopePrintRequested(true, _scope);
            }
        }

        private void ScopePrintClick(object sender, RoutedEventArgs e)
        {
            if (ScopePrintRequested != null)
            {
                ScopePrintRequested(false, _scope);
            }
        }

        private void SaveOfficerClick(object sender, RoutedEventArgs e)
        {
            _settings.Set(SettingKeys.OperatorName, OperatorBox.Text.Trim());
            _settings.Set(SettingKeys.DirectorateName, DirectorateBox.Text.Trim());
            _settings.Set(SettingKeys.StationName, StationBox.Text.Trim());
            _settings.Set(SettingKeys.RosterHeaderLine1, HeaderBox.Text.Trim());
            System.Action h = OfficerSaved;
            if (h != null)
            {
                h();
            }
            if (IsLoaded && IsVisible)
            {
                MessageDialog.Show(this, Strings.OfficerTitle, Strings.OfficerSaved);
            }
        }

        public System.Action OfficerSaved;

        public System.Func<System.Collections.Generic.IReadOnlyList<CourtDesk.UI.ViewModels.RegisterRow>> RowsProvider;

        public System.Func<string, System.Collections.Generic.IReadOnlyList<CourtDesk.UI.ViewModels.RegisterRow>> ScopedRowsProvider;

        private string _scope = "ALL";

        private void ScopeChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = ScopeBox.SelectedItem as ComboBoxItem;
            if (item != null && item.Tag != null)
            {
                _scope = Convert.ToString(item.Tag);
            }
        }

        public ExcelTransferService Excel;

        private string DefaultPath(string kind)
        {
            string dir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                dir = _home;
            }
            return Path.Combine(dir, "courtdesk-" + kind + "-"
                + new CourtDesk.Domain.SystemClock().UtcNow.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)
                + ".xlsx");
        }

        private string SavePicker(string title, string kind)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = title,
                Filter = "Excel Workbook (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
                FileName = System.IO.Path.GetFileName(DefaultPath(kind)),
                InitialDirectory = System.IO.Path.GetDirectoryName(DefaultPath(kind)),
                AddExtension = true,
                OverwritePrompt = true
            };
            return dlg.ShowDialog(this) == true ? dlg.FileName : null;
        }

        private string OpenPicker(string title)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = title,
                Filter = "Excel Workbook (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };
            return dlg.ShowDialog(this) == true ? dlg.FileName : null;
        }

        private void TemplateClick(object sender, RoutedEventArgs e)
        {
            if (Excel == null)
            {
                return;
            }
            try
            {
                string path = SavePicker(Strings.ExcelTemplate, "template");
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }
                Excel.WriteTemplate(path);
                MessageDialog.Show(this, Strings.ExcelTitle,
                    string.Format(CultureInfo.InvariantCulture, Strings.ExcelSaved, path));
            }
            catch (System.Exception ex)
            {
                MessageDialog.Show(this, Strings.ExcelTitle, ex.Message);
            }
        }

        private void ImportClick(object sender, RoutedEventArgs e)
        {
            if (Excel == null)
            {
                return;
            }
            string path = OpenPicker(Strings.ExcelImport);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }
            try
            {
                ImportReport report = Excel.Import(path, false);
                string body = string.Format(CultureInfo.InvariantCulture,
                    Strings.ExcelDone, report.Added, report.Skipped);
                if (report.Problems.Count > 0)
                {
                    // the dialog cannot scroll, so keep the list short and
                    // readable no matter how many rows were imported
                    int shown = System.Math.Min(5, report.Problems.Count);
                    var lines = new System.Collections.Generic.List<string>();
                    for (int i = 0; i < shown; i++)
                    {
                        lines.Add(report.Problems[i]);
                    }
                    body = body + "\n" + string.Join("\n", lines.ToArray());
                    if (report.Problems.Count > shown)
                    {
                        body = body + "\n" + string.Format(
                            CultureInfo.InvariantCulture, Strings.ExcelMoreProblems,
                            report.Problems.Count - shown);
                    }
                }
                MessageDialog.Show(this, Strings.ExcelImport, body);
                System.Action<int> h = ImportDone;
                if (h != null)
                {
                    h(report.Added + report.Skipped);
                }
            }
            catch (System.Exception ex)
            {
                MessageDialog.Show(this, Strings.ExcelImport, ex.Message);
            }
        }

        private void ExportClick(object sender, RoutedEventArgs e)
        {
            if (Excel == null)
            {
                return;
            }
            System.Collections.Generic.IReadOnlyList<CourtDesk.UI.ViewModels.RegisterRow> rows;
            if (ScopedRowsProvider != null)
            {
                rows = ScopedRowsProvider(_scope);
            }
            else
            {
                rows = RowsProvider == null ? null : RowsProvider();
            }
            if (rows == null || rows.Count == 0)
            {
                MessageDialog.Show(this, Strings.ExcelExport, Strings.ExcelNothing);
                return;
            }
            string path = SavePicker(Strings.ExcelExport, "export");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            try
            {
                int n = Excel.Export(path, rows);
                MessageDialog.Show(this, Strings.ExcelExport,
                    string.Format(CultureInfo.InvariantCulture, Strings.ExcelSavedCount,
                        string.Format(CultureInfo.InvariantCulture, Strings.ExcelSaved, path), n));
            }
            catch (System.Exception ex)
            {
                MessageDialog.Show(this, Strings.ExcelExport, ex.Message);
            }
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
            OperatorBox.Text = _settings.Get(SettingKeys.OperatorName) ?? "";
            DirectorateBox.Text = _settings.Get(SettingKeys.DirectorateName) ?? "";
            StationBox.Text = _settings.Get(SettingKeys.StationName) ?? "";
            HeaderBox.Text = _settings.Get(SettingKeys.RosterHeaderLine1) ?? "";
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
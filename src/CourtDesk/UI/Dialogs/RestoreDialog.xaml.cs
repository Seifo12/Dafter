using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using CourtDesk.Application;
using CourtDesk.Resources;

namespace CourtDesk.UI.Dialogs
{
    public partial class RestoreDialog : Window
    {
        private readonly string _home;
        private readonly string _dbPath;
        private readonly BackupService _backups;

        public RestoreDialog(string home, BackupService backups)
        {
            InitializeComponent();
            _home = home;
            _backups = backups;
            _dbPath = CourtDesk.Persistence.DbPaths.Db(home);
            Reload();
        }

        public BackupCandidate Chosen { get; private set; }

        private void Reload()
        {
            var rows = new List<BackupRow>();
            List<BackupCandidate> all;
            try
            {
                all = _backups == null ? new List<BackupCandidate>() : _backups.ListLocalBackups();
            }
            catch (Exception)
            {
                all = new List<BackupCandidate>();
            }
            BackupCandidate newest = null;
            foreach (BackupCandidate c in all)
            {
                rows.Add(new BackupRow
                {
                    Candidate = c,
                    Tier = c.Tier,
                    FileName = c.FileName,
                    SizeText = SizeText(c.Path),
                    WrittenText = c.WrittenUtc.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture)
                });
                if (newest == null || c.WrittenUtc > newest.WrittenUtc)
                {
                    newest = c;
                }
            }
            BackupGrid.ItemsSource = rows;
            long size = DbSize();
            string when = newest == null ? "-" : newest.WrittenUtc.ToString(
                "yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);
            CompareText.Text = string.Format(CultureInfo.InvariantCulture, Strings.BkRestoreCompare,
                CountFiles(), when, size);
            if (rows.Count > 0)
            {
                BackupGrid.SelectedIndex = 0;
            }
            else
            {
                CompareText.Text = Strings.RecoverNone;
            }
        }

        private static BackupCandidate[] ListFromDisk()
        {
            var out_ = new List<BackupCandidate>();
            return out_.ToArray();
        }

        private long DbSize()
        {
            try
            {
                var fi = new FileInfo(_dbPath);
                return fi.Exists ? fi.Length : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private int CountFiles()
        {
            try
            {
                return Directory.GetFiles(_home).Length;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static string SizeText(string path)
        {
            try
            {
                var fi = new FileInfo(path);
                if (!fi.Exists)
                {
                    return "-";
                }
                return (fi.Length / (1024.0 * 1024.0)).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
            }
            catch (Exception)
            {
                return "-";
            }
        }

        private void BackupGrid_SelectedItemChanged(object sender, SelectionChangedEventArgs e)
        {
            var row = BackupGrid.SelectedItem as BackupRow;
            Chosen = row == null ? null : row.Candidate;
        }

        private void GoClick(object sender, RoutedEventArgs e)
        {
            if (Chosen == null)
            {
                return;
            }
            DialogResult = true;
        }

        private void BackClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private sealed class BackupRow
        {
            public BackupCandidate Candidate;
            public string Tier;
            public string FileName;
            public string SizeText;
            public string WrittenText;
        }
    }
}
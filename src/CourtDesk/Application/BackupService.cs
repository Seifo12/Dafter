using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;

namespace CourtDesk.Application
{
    public enum UsbFailure
    {
        None,
        Removed,
        Full,
        ReadOnly,
        Unknown
    }

    public sealed class UsbExportResult
    {
        public bool Ok;
        public UsbFailure Failure;
        public string FinalPath;
        public DriveItem Drive;
    }

    public sealed class BackupService
    {
        public const int AutoRingSize = 10;
        public const int ExitKeepNewest = 60;
        public const int ExitKeepMonths = 12;
        public const int UsbKeepNewest = 30;

        private readonly IDatabase _db;
        private readonly IBackupEngine _engine;
        private readonly IDriveProvider _drives;
        private readonly IMetaStore _meta;
        private readonly IClock _clock;
        private readonly SettingsService _settings;
        private readonly Paths _paths;
        private readonly ILogger _log;
        private int _autoIndex;

        public BackupService(IDatabase db, IBackupEngine engine, IDriveProvider drives, IMetaStore meta,
            IClock clock, SettingsService settings, Paths paths, ILogger log)
        {
            _db = db;
            _engine = engine;
            _drives = drives;
            _meta = meta;
            _clock = clock;
            _settings = settings;
            _paths = paths;
            _log = log;
        }

        public sealed class Paths
        {
            public string Home;
            public string AutoDir;
            public string ExitDir;
            public string PreOpDir;
            public string TmpDir;
        }

        public static Paths ForHome(string home)
        {
            Persistence.HomePaths p = Persistence.DbPathsFor.Of(home);
            return new Paths
            {
                Home = home,
                AutoDir = p.AutoDir,
                ExitDir = p.ExitDir,
                PreOpDir = p.PreOpDir,
                TmpDir = p.TmpDir
            };
        }

        public DriveItem[] RemovableDrives()
        {
            DriveItem[] all = _drives.List();
            var outp = new List<DriveItem>();
            foreach (DriveItem d in all)
            {
                if (d.Kind == DriveKind.Removable)
                {
                    outp.Add(d);
                }
            }
            return outp.ToArray();
        }

        public BackupReport TakeAutoSnapshot()
        {
            EnsureDir(_paths.AutoDir);
            string dest = Path.Combine(_paths.AutoDir, "auto-" + _autoIndex + ".db");
            _autoIndex = (_autoIndex + 1) % AutoRingSize;
            return _engine.Snapshot(dest);
        }

        public BackupReport TakeExitSnapshot()
        {
            string seq = _db.Read(s => _meta.GetMeta(s, "change_seq"));
            string last = _db.Read(s => _meta.GetMeta(s, "last_exit_backup_seq"));
            if (last == seq)
            {
                return null;
            }
            EnsureDir(_paths.ExitDir);
            string stamp = _clock.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string dest = Path.Combine(_paths.ExitDir, "exit-" + stamp + ".db");
            BackupReport rep = _engine.Snapshot(dest);
            long n;
            if (!long.TryParse(seq, out n))
            {
                n = 0;
            }
            string recorded = (n + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            _db.Write(s =>
            {
                _meta.SetMeta(s, "last_exit_backup_seq", recorded);
                return 0;
            });
            ApplyRetention(_paths.ExitDir, "exit-*.db");
            string secondary = _settings.Get(SettingKeys.SecondaryBackupDir);
            if (!string.IsNullOrEmpty(secondary))
            {
try
                    {
                        if (!Directory.Exists(secondary))
                        {
                            Directory.CreateDirectory(secondary);
                        }
                        StreamCopy(rep.Path, Path.Combine(secondary, Path.GetFileName(rep.Path)));
                    }
                catch (IOException e)
                {
                    _log.Warn("E-BAK-010", "secondary copy skipped: " + e.GetType().Name);
                }
                catch (UnauthorizedAccessException e)
                {
                    _log.Warn("E-BAK-011", "secondary copy skipped: " + e.GetType().Name);
                }
            }
            return rep;
        }

        public static void ApplyRetention(string dir, string pattern)
        {
            if (!Directory.Exists(dir))
            {
                return;
            }
            FileInfo[] files = new DirectoryInfo(dir).GetFiles(pattern);
            Array.Sort(files, delegate(FileInfo a, FileInfo b) { return b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc); });
            var keptMonths = new HashSet<string>();
            for (int i = 0; i < files.Length; i++)
            {
                if (i < ExitKeepNewest)
                {
                    continue;
                }
                string month = files[i].LastWriteTimeUtc.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                if (keptMonths.Count < ExitKeepMonths && !keptMonths.Contains(month))
                {
                    keptMonths.Add(month);
                    continue;
                }
                try
                {
                    files[i].Delete();
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        public UsbExportResult ExportUsb(DriveItem drive)
        {
            var result = new UsbExportResult { Drive = drive };
            if (drive == null)
            {
                result.Failure = UsbFailure.Removed;
                return result;
            }
            long needed = CurrentDbSize() * 2 + 5L * 1024 * 1024;
            if (drive.FreeBytes < needed)
            {
                result.Failure = UsbFailure.Full;
                return result;
            }
            EnsureDir(_paths.TmpDir);
            string snap = Path.Combine(_paths.TmpDir, "usb-" +
                _clock.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".db");
            BackupReport rep;
            try
            {
                rep = _engine.Snapshot(snap);
            }
            catch (Exception e)
            {
                result.Failure = UsbFailure.Unknown;
                _log.Error("E-BAK-020", "usb snapshot failed: " + e.GetType().Name);
                return result;
            }
            string folder = Path.Combine(drive.Name, "CourtDesk_Backups");
            try
            {
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }
            }
            catch (UnauthorizedAccessException)
            {
                result.Failure = UsbFailure.ReadOnly;
                TryDelete(snap);
                return result;
            }
            catch (IOException)
            {
                result.Failure = UsbFailure.Removed;
                TryDelete(snap);
                return result;
            }
            string stamp = _clock.UtcNow.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture);
            string partial = Path.Combine(folder, "CourtDesk_" + stamp + ".db.partial");
            string final = Path.Combine(folder, "CourtDesk_" + stamp + ".db");
            try
            {
                CopyVerified(rep.Path, partial, rep);
            }
            catch (UnauthorizedAccessException)
            {
                TryDelete(partial);
                TryDelete(snap);
                result.Failure = UsbFailure.ReadOnly;
                return result;
            }
            catch (DriveNotFoundException)
            {
                TryDelete(partial);
                TryDelete(snap);
                result.Failure = UsbFailure.Removed;
                return result;
            }
            catch (DirectoryNotFoundException)
            {
                TryDelete(partial);
                TryDelete(snap);
                result.Failure = UsbFailure.Removed;
                return result;
            }
            catch (IOException)
            {
                TryDelete(partial);
                TryDelete(snap);
                result.Failure = UsbFailure.Removed;
                return result;
            }
            if (File.Exists(final))
            {
                File.Delete(final);
            }
            File.Move(partial, final);
            TryDelete(snap);
            RotateUsb(folder);
            _settings.Set(SettingKeys.LastUsbBackupUtc, DbGatewayFormat.Utc(_clock.UtcNow));
            result.Ok = true;
            result.FinalPath = final;
            return result;
        }

        public string LastExitBackupUtc()
        {
            List<BackupCandidate> all = ListLocalBackups();
            foreach (BackupCandidate c in all)
            {
                if (c.Tier == "exit")
                {
                    return DbGatewayFormat.Utc(c.WrittenUtc);
                }
            }
            return null;
        }

        public int UsbAgeDays()
        {
            string raw = _settings.LastUsbBackupUtc();
            if (string.IsNullOrEmpty(raw))
            {
                return -1;
            }
            DateTime then;
            if (!DateTime.TryParseExact(raw, "yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out then))
            {
                return -1;
            }
            double hours = (_clock.UtcNow - then).TotalHours;
            if (hours < 0)
            {
                return 0;
            }
            return (int)(hours / 24.0);
        }

        public bool UsbReminderDue()
        {
            int threshold = _settings.GetInt(SettingKeys.UsbBackupReminderDays, 7);
            int age = UsbAgeDays();
            return age < 0 || age > threshold;
        }

        public List<BackupCandidate> ListLocalBackups()
        {
            var outp = new List<BackupCandidate>();
            foreach (string dir in new string[] { _paths.ExitDir, _paths.AutoDir, _paths.PreOpDir })
            {
                if (!Directory.Exists(dir))
                {
                    continue;
                }
                foreach (FileInfo f in new DirectoryInfo(dir).GetFiles("*.db"))
                {
                    outp.Add(new BackupCandidate
                    {
                        Path = f.FullName,
                        FileName = f.Name,
                        Tier = Path.GetFileName(dir),
                        WrittenUtc = f.LastWriteTimeUtc
                    });
                }
            }
            outp.Sort(delegate(BackupCandidate a, BackupCandidate b) { return b.WrittenUtc.CompareTo(a.WrittenUtc); });
            return outp;
        }

        private void RotateUsb(string folder)
        {
            FileInfo[] files = new DirectoryInfo(folder).GetFiles("CourtDesk_*.db");
            Array.Sort(files, delegate(FileInfo a, FileInfo b) { return b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc); });
            for (int i = UsbKeepNewest; i < files.Length; i++)
            {
                try { files[i].Delete(); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        internal static void StreamCopy(string src, string dest)
        {
            if (File.Exists(dest))
            {
                File.Delete(dest);
            }
            using (var input = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = new FileStream(dest, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var buf = new byte[64 * 1024];
                int n;
                while ((n = input.Read(buf, 0, buf.Length)) > 0)
                {
                    output.Write(buf, 0, n);
                }
                output.Flush(true);
            }
        }

        internal static void CopyVerified(string src, string dest, BackupReport rep)
        {
            using (var input = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buf = new byte[64 * 1024];
                int n;
                while ((n = input.Read(buf, 0, buf.Length)) > 0)
                {
                    output.Write(buf, 0, n);
                }
                output.Flush(true);
            }
            Persistence.RestoreSwap.VerifyBackup(dest);
            using (var f = new FileStream(dest, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                string got = BitConverter.ToString(sha.ComputeHash(f)).Replace("-", "").ToLowerInvariant();
                if (got != rep.Sha256)
                {
                    throw new IOException("sha mismatch");
                }
            }
        }

        private long CurrentDbSize()
        {
            string db = Persistence.DbPaths.Db(_paths.Home);
            try
            {
                return File.Exists(db) ? new FileInfo(db).Length : 0;
            }
            catch (IOException)
            {
                return 0;
            }
        }

        private static void EnsureDir(string dir)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public static class DbGatewayFormat
    {
        public static string Utc(DateTime utc)
        {
            return utc.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);
        }
    }

    public sealed class BackupCandidate
    {
        public string Path;
        public string FileName;
        public string Tier;
        public DateTime WrittenUtc;
    }
}

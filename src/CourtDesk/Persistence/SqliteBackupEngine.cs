using System;
using System.Data.SQLite;
using System.IO;
using System.Security.Cryptography;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;

namespace CourtDesk.Persistence
{
    public sealed class SqliteBackupEngine : IBackupEngine
    {
        private readonly DbGateway _gateway;
        private readonly IClock _clock;
        private readonly string _home;

        public SqliteBackupEngine(DbGateway gateway, IClock clock, string home)
        {
            _gateway = gateway;
            _clock = clock;
            _home = home;
        }

        public BackupReport Snapshot(string destFinalPath)
        {
            return _gateway.Read(ctx => SnapshotFrom(((DbContext)ctx).Connection, _home, destFinalPath));
        }

        internal static BackupReport SnapshotFrom(SQLiteConnection source, string home, string destFinalPath)
        {
            string tmp = Path.Combine(DbPaths.TmpDir(home), "snap-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                CopyDatabase(source, tmp, true);
                var report = VerifyAndSeal(tmp);
                FlushFile(tmp);
                if (File.Exists(destFinalPath))
                {
                    File.Replace(tmp, destFinalPath, null);
                }
                else
                {
                    string dir = Path.GetDirectoryName(destFinalPath);
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    File.Move(tmp, destFinalPath);
                }
                report.Path = destFinalPath;
                using (SHA256 sha = SHA256.Create())
                using (Stream s = File.OpenRead(destFinalPath))
                {
                    report.Sha256 = BitConverter.ToString(sha.ComputeHash(s)).Replace("-", "").ToLowerInvariant();
                }
                report.SizeBytes = new FileInfo(destFinalPath).Length;
                return report;
            }
            catch (Exception e)
            {
                try
                {
                    if (File.Exists(tmp))
                    {
                        File.Delete(tmp);
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                if (e is DbException)
                {
                    throw;
                }
                throw new DbException(ErrorCodes.DbBackup, "snapshot failed: " + e.Message, e);
            }
        }

        internal static void CopyDatabase(SQLiteConnection source, string destPath, bool destDeleteJournal)
        {
            using (var dest = new SQLiteConnection("Data Source=" + destPath + ";Version=3;Pooling=False"))
            {
                dest.Open();
                source.BackupDatabase(dest, "main", "main", -1, null, 0);
                if (destDeleteJournal)
                {
                    using (var cmd = new SQLiteCommand("PRAGMA journal_mode=DELETE;", dest))
                    {
                        object v = cmd.ExecuteScalar();
                        if (v == null || v.ToString().ToLowerInvariant() != "delete")
                        {
                            throw new DbException(ErrorCodes.DbBackup, "snapshot journal_mode not delete");
                        }
                    }
                    using (var cmd = new SQLiteCommand("PRAGMA synchronous=FULL;", dest))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
                dest.Close();
            }
        }

        internal static BackupReport VerifyAndSeal(string tmpPath)
        {
            var report = new BackupReport();
            using (var conn = new SQLiteConnection("Data Source=" + tmpPath + ";Version=3;Pooling=False;Read Only=True"))
            {
                conn.Open();
                try
                {
                    if (!IntegrityChecker.QuickCheck(conn))
                    {
                        throw new DbException(ErrorCodes.DbBackup, "snapshot integrity_check failed");
                    }
                    report.DefendantCount = Scalar(conn, "SELECT COUNT(*) FROM defendants");
                    report.EventCount = Scalar(conn, "SELECT COUNT(*) FROM session_events");
                }
                finally
                {
                    conn.Close();
                }
            }
            return report;
        }

        internal static void FlushFile(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                fs.Flush(true);
            }
        }

        private static long Scalar(SQLiteConnection conn, string sql)
        {
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                return Convert.ToInt64(cmd.ExecuteScalar());
            }
        }
    }
}

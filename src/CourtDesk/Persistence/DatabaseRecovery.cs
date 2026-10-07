using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Threading;
using CourtDesk.Domain;

namespace CourtDesk.Persistence
{
    public sealed class RecoveryResult
    {
        public DbGateway Gateway;
        public bool WasRecovered;
        public string BackupTimeIso;
        public int BackupAgeMinutes;
        public bool WasUnclean;
        public List<string> InvariantWarnings = new List<string>();
    }

    public static class DatabaseRecovery
    {
        public static RecoveryResult OpenWithRecovery(string home, IClock clock, SynchronizationContext marshal)
        {
            DbPaths.EnsureAll(home);
            DbPaths.CleanTmp(home);
            string db = DbPaths.Db(home);
            string marker = DbPaths.RestoreMarker(home);
            RestoreSwap.ResolvePending(db, marker);
            try
            {
                SqliteBootstrap.PrepareDatabase(db, clock);
            }
            catch (BusyException)
            {
                throw;
            }
            catch (UnsupportedVersionException)
            {
                throw;
            }
            catch (DbException e)
            {
                return RecoverCorrupt(home, clock, marshal, db);
            }
            catch (SQLiteException e)
            {
                if (IsCorruption(e))
                {
                    return RecoverCorrupt(home, clock, marshal, db);
                }
                throw new DbException(ErrorCodes.DbState, "open failed: " + e.Message, e);
            }
            var gateway = new DbGateway(db, clock, marshal);
            var result = new RecoveryResult { Gateway = gateway };
            FinishOpen(gateway, result);
            return result;
        }

        private static RecoveryResult RecoverCorrupt(string home, IClock clock, SynchronizationContext marshal, string db)
        {
            string stamp = clock.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            string quarantine = Path.Combine(DbPaths.QuarantineDir(home),
                "corrupt-" + stamp + ".db");
            try
            {
                if (File.Exists(db))
                {
                    File.Move(db, quarantine);
                }
                foreach (string ext in new string[] { "-wal", "-shm", "-journal" })
                {
                    if (File.Exists(db + ext))
                    {
                        File.Move(db + ext, quarantine + ext);
                    }
                }
            }
            catch (IOException e)
            {
                throw new DbException(ErrorCodes.DbState, "quarantine failed: " + e.Message, e);
            }
            foreach (string candidate in Candidates(home))
            {
                try
                {
                    RestoreSwap.VerifyBackup(candidate);
                }
                catch (DbException)
                {
                    continue;
                }
                RestoreSwap.Install(db, DbPaths.RestoreMarker(home), DbPaths.PreOpDir(home), candidate, clock);
                SqliteBootstrap.PrepareDatabase(db, clock);
                var gateway = new DbGateway(db, clock, marshal);
                var result = new RecoveryResult { Gateway = gateway, WasRecovered = true };
                FileInfo fi = new FileInfo(candidate);
                result.BackupTimeIso = DbGateway.FormatUtc(fi.LastWriteTimeUtc);
                result.BackupAgeMinutes = Math.Max(0, (int)(clock.UtcNow - fi.LastWriteTimeUtc).TotalMinutes);
                FinishOpen(gateway, result);
                return result;
            }
            throw new NoBackupException();
        }

        private static List<string> Candidates(string home)
        {
            var files = new List<FileInfo>();
            foreach (string dir in new string[] { DbPaths.AutoDir(home), DbPaths.ExitDir(home), DbPaths.PreOpDir(home) })
            {
                if (!Directory.Exists(dir))
                {
                    continue;
                }
                files.AddRange(new DirectoryInfo(dir).GetFiles("*.db"));
            }
            files.Sort((a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            var out_ = new List<string>();
            foreach (FileInfo f in files)
            {
                out_.Add(f.FullName);
            }
            return out_;
        }

        private static void FinishOpen(DbGateway gateway, RecoveryResult result)
        {
            string clean = gateway.Read(ctx => Meta.Get(((DbContext)ctx).Connection, "clean_shutdown"));
            if (clean != "1")
            {
                result.WasUnclean = true;
            }
            gateway.Write(ctx =>
            {
                Meta.Set(((DbContext)ctx).Connection, "clean_shutdown", "0");
                return 0;
            });
            result.InvariantWarnings = gateway.Read(ctx => IntegrityChecker.CheckInvariants(((DbContext)ctx).Connection));
        }

        private static bool IsCorruption(SQLiteException e)
        {
            return e.ResultCode == SQLiteErrorCode.Corrupt || e.ResultCode == SQLiteErrorCode.NotADb
                || e.ResultCode == SQLiteErrorCode.IoErr || e.ResultCode == SQLiteErrorCode.Full
                || e.ResultCode == SQLiteErrorCode.CantOpen;
        }
    }
}

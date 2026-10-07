using System;
using System.Data.SQLite;
using System.IO;
using CourtDesk.Domain;

namespace CourtDesk.Persistence
{
    public static class RestoreSwap
    {
        public static void Install(string dbPath, string markerPath, string preOpDir, string sourceBackupPath, IClock clock)
        {
            VerifyBackup(sourceBackupPath);
            string stamp = clock.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            if (File.Exists(dbPath))
            {
                try
                {
                    string pre = Path.Combine(preOpDir, "pre-restore-" + stamp + ".db");
                    if (!Directory.Exists(preOpDir))
                    {
                        Directory.CreateDirectory(preOpDir);
                    }
                    using (var src = OpenReadWrite(dbPath))
                    using (var dst = new SQLiteConnection("Data Source=" + pre + ";Version=3;Pooling=False"))
                    {
                        dst.Open();
                        src.BackupDatabase(dst, "main", "main", -1, null, 0);
                        dst.Close();
                    }
                    SqliteBackupEngine.FlushFile(pre);
                }
                catch (Exception e) when (e is SQLiteException || e is DbException || e is IOException)
                {
                    throw new DbException(ErrorCodes.DbRestore, "pre-restore copy failed: " + e.Message, e);
                }
            }
            using (var conn = OpenReadWrite(dbPath, true))
            {
                if (conn != null)
                {
                    using (conn)
                    using (var cmd = new SQLiteCommand("PRAGMA wal_checkpoint(TRUNCATE);", conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            string restoring = dbPath + ".restoring";
            if (File.Exists(restoring))
            {
                File.Delete(restoring);
            }
            using (var src = OpenReadOnly(sourceBackupPath))
            using (var dst = new SQLiteConnection("Data Source=" + restoring + ";Version=3;Pooling=False"))
            {
                dst.Open();
                src.BackupDatabase(dst, "main", "main", -1, null, 0);
                using (var cmd = new SQLiteCommand("PRAGMA synchronous=FULL;", dst))
                {
                    cmd.ExecuteNonQuery();
                }
                dst.Close();
            }
            VerifyBackup(restoring);
            SqliteBackupEngine.FlushFile(restoring);

            string restoreId = Guid.NewGuid().ToString("N");
            using (var fs = new FileStream(markerPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var w = new StreamWriter(fs))
            {
                w.Write(restoreId);
                w.Flush();
                fs.Flush(true);
            }
            if (File.Exists(dbPath))
            {
                string replaced = dbPath + ".replaced";
                if (File.Exists(replaced))
                {
                    File.Delete(replaced);
                }
                File.Move(dbPath, replaced);
            }
            DeleteSidecars(dbPath);
            File.Move(restoring, dbPath);

            using (var conn = OpenReadWrite(dbPath))
            using (var cmd = new SQLiteCommand("UPDATE app_meta SET value = @v WHERE key = 'restore_id'", conn))
            {
                cmd.Parameters.AddWithValue("@v", restoreId);
                cmd.ExecuteNonQuery();
            }
            string done = dbPath + ".replaced";
            if (File.Exists(done))
            {
                File.Delete(done);
            }
            File.Delete(markerPath);
        }

        public static void ResolvePending(string dbPath, string markerPath)
        {
            if (!File.Exists(markerPath))
            {
                return;
            }
            string id = File.ReadAllText(markerPath).Trim();
            try
            {
                using (var conn = OpenReadOnly(dbPath))
                {
                    string current = Meta.Get(conn, "restore_id");
                    if (current == id)
                    {
                        string replaced = dbPath + ".replaced";
                        if (File.Exists(replaced))
                        {
                            File.Delete(replaced);
                        }
                        File.Delete(markerPath);
                        return;
                    }
                }
            }
            catch (Exception e) when (e is SQLiteException || e is DbException || e is IOException)
            {
            }
            string bak = dbPath + ".replaced";
            if (File.Exists(bak))
            {
                DeleteSidecars(dbPath);
                if (File.Exists(dbPath))
                {
                    File.Delete(dbPath);
                }
                File.Move(bak, dbPath);
            }
            else
            {
                string restoring = dbPath + ".restoring";
                if (File.Exists(restoring))
                {
                    File.Delete(restoring);
                }
            }
            File.Delete(markerPath);
        }

        public static void VerifyBackup(string path)
        {
            if (!File.Exists(path))
            {
                throw new CorruptDatabaseException("backup missing: " + path);
            }
            try
            {
                using (var conn = OpenReadOnly(path))
                {
                    if (!IntegrityChecker.QuickCheck(conn))
                    {
                        throw new CorruptDatabaseException("backup quick_check failed: " + path);
                    }
                    foreach (string t in new string[] { "defendants", "session_events", "app_meta" })
                    {
                        using (var cmd = new SQLiteCommand(
                            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @t", conn))
                        {
                            cmd.Parameters.AddWithValue("@t", t);
                            if (Convert.ToInt32(cmd.ExecuteScalar()) != 1)
                            {
                                throw new CorruptDatabaseException("backup missing table " + t + ": " + path);
                            }
                        }
                    }
                    int user;
                    using (var cmd = new SQLiteCommand("PRAGMA user_version;", conn))
                    {
                        user = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                    if (user < 1 || user > DbPaths.SupportedSchemaVersion)
                    {
                        throw new CorruptDatabaseException("backup user_version " + user + ": " + path);
                    }
                }
            }
            catch (SQLiteException e)
            {
                throw new CorruptDatabaseException("backup unreadable: " + path + " (" + e.Message + ")");
            }
        }

        private static SQLiteConnection OpenReadOnly(string path)
        {
            var conn = new SQLiteConnection("Data Source=" + path + ";Version=3;Pooling=False;Read Only=True");
            conn.Open();
            return conn;
        }

        private static SQLiteConnection OpenReadWrite(string path, bool allowMissing)
        {
            if (allowMissing && !File.Exists(path))
            {
                return null;
            }
            var conn = new SQLiteConnection(DbGateway.ConnectionString(path));
            conn.Open();
            return conn;
        }

        private static SQLiteConnection OpenReadWrite(string path)
        {
            var conn = new SQLiteConnection(DbGateway.ConnectionString(path));
            conn.Open();
            return conn;
        }

        private static void DeleteSidecars(string dbPath)
        {
            foreach (string ext in new string[] { "-wal", "-shm", "-journal" })
            {
                try
                {
                    if (File.Exists(dbPath + ext))
                    {
                        File.Delete(dbPath + ext);
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}

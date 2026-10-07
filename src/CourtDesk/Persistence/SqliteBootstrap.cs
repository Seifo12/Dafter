using System;
using System.Data.SQLite;
using System.IO;
using CourtDesk.Domain;

namespace CourtDesk.Persistence
{
    public static class SqliteBootstrap
    {
        public static void PrepareDatabase(string dbPath, IClock clock)
        {
            string dir = Path.GetDirectoryName(dbPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            using (SQLiteConnection conn = new SQLiteConnection(DbGateway.ConnectionString(dbPath)))
            {
                try
                {
                    conn.Open();
                }
                catch (SQLiteException e) when (IsBusy(e))
                {
                    throw new BusyException();
                }
                ApplyPragmas(conn);
                MigrationRunner.Apply(conn);
                CheckApplicationId(conn);
                if (!IntegrityChecker.QuickCheck(conn))
                {
                    throw new CorruptDatabaseException("quick_check failed");
                }
                EnsureMeta(conn, clock);
            }
        }

        public static void ApplyPragmas(SQLiteConnection conn)
        {
            try
            {
                Execute(conn, "PRAGMA locking_mode=EXCLUSIVE;");
                ExpectGet(conn, "PRAGMA locking_mode;", "locking_mode", "exclusive");
                Expect(conn, "PRAGMA journal_mode=WAL;", "journal_mode", "wal");
                Execute(conn, "PRAGMA synchronous=FULL;");
                ExpectGet(conn, "PRAGMA synchronous;", "synchronous", "2");
                Execute(conn, "PRAGMA foreign_keys=ON;");
                ExpectGet(conn, "PRAGMA foreign_keys;", "foreign_keys", "1");
                Execute(conn, "PRAGMA busy_timeout=5000;");
                Execute(conn, "PRAGMA trusted_schema=OFF;");
                Execute(conn, "PRAGMA cell_size_check=ON;");
                Execute(conn, "PRAGMA secure_delete=FAST;");
                Execute(conn, "PRAGMA temp_store=MEMORY;");
                Execute(conn, "PRAGMA wal_autocheckpoint=400;");
                Execute(conn, "PRAGMA journal_size_limit=4194304;");
            }
            catch (SQLiteException e) when (IsBusy(e))
            {
                throw new BusyException();
            }
        }

        public static bool IsBusy(SQLiteException e)
        {
            return e.ResultCode == SQLiteErrorCode.Busy || e.ResultCode == SQLiteErrorCode.Locked;
        }

        private static void CheckApplicationId(SQLiteConnection conn)
        {
            using (var cmd = new SQLiteCommand("PRAGMA application_id;", conn))
            {
                object v = cmd.ExecuteScalar();
                if (v == null || Convert.ToInt64(v) != 0x43444B31)
                {
                    throw new NotOurDatabaseException();
                }
            }
            int user = MigrationRunner.GetUserVersion(conn);
            if (user > DbPaths.SupportedSchemaVersion)
            {
                throw new UnsupportedVersionException(user);
            }
        }

        private static void EnsureMeta(SQLiteConnection conn, IClock clock)
        {
            if (string.IsNullOrEmpty(Meta.Get(conn, "schema_created_utc")))
            {
                Meta.Set(conn, "schema_created_utc", DbGateway.FormatUtc(clock.UtcNow));
            }
        }

        private static void Expect(SQLiteConnection conn, string pragma, string name, string want)
        {
            using (var cmd = new SQLiteCommand(pragma, conn))
            {
                object v = cmd.ExecuteScalar();
                string got = v == null ? "" : v.ToString().ToLowerInvariant();
                if (got != want)
                {
                    throw new CorruptDatabaseException(name + " is '" + got + "', want '" + want + "'");
                }
            }
        }

        private static void ExpectGet(SQLiteConnection conn, string pragma, string name, string want)
        {
            using (var cmd = new SQLiteCommand(pragma, conn))
            {
                object v = cmd.ExecuteScalar();
                string got = v == null ? "" : v.ToString().ToLowerInvariant();
                if (got != want)
                {
                    throw new CorruptDatabaseException(name + " is '" + got + "', want '" + want + "'");
                }
            }
        }

        private static void Execute(SQLiteConnection conn, string sql)
        {
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.ExecuteNonQuery();
            }
        }
    }
}

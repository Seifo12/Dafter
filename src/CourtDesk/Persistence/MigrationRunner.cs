using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Reflection;
using System.Text;

namespace CourtDesk.Persistence
{
    public static class MigrationRunner
    {
        public static void Apply(SQLiteConnection conn)
        {
            Apply(conn, null);
        }

        public static void Apply(SQLiteConnection conn, Action<SQLiteConnection, int> preMigrateBackup)
        {
            int v = GetUserVersion(conn);
            if (v > DbPaths.SupportedSchemaVersion)
            {
                throw new UnsupportedVersionException(v);
            }
            if (v == 0)
            {
                RunScript(conn, LoadEmbedded("001_initial.sql"));
                SetUserVersion(conn, 1);
            }
        }

        public static int GetUserVersion(SQLiteConnection conn)
        {
            using (var cmd = new SQLiteCommand("PRAGMA user_version;", conn))
            {
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        private static void SetUserVersion(SQLiteConnection conn, int v)
        {
            using (var cmd = new SQLiteCommand("PRAGMA user_version = " + v + ";", conn))
            {
                cmd.ExecuteNonQuery();
            }
        }

        private static void RunScript(SQLiteConnection conn, string sql)
        {
            using (SQLiteTransaction tx = conn.BeginTransaction())
            {
                try
                {
                    foreach (string stmt in SplitStatements(sql))
                    {
                        using (var cmd = new SQLiteCommand(stmt, conn, tx))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }
                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }

        internal static List<string> SplitStatements(string sql)
        {
            var out_ = new List<string>();
            var sb = new StringBuilder();
            bool inTrigger = false;
            using (var reader = new StringReader(sql))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string t = line.TrimStart();
                    if (!inTrigger && t.StartsWith("CREATE TRIGGER", StringComparison.OrdinalIgnoreCase))
                    {
                        inTrigger = true;
                    }
                    sb.AppendLine(line);
                    if (inTrigger)
                    {
                        if (t.Equals("END;", StringComparison.OrdinalIgnoreCase))
                        {
                            inTrigger = false;
                            out_.Add(sb.ToString());
                            sb.Length = 0;
                        }
                    }
                    else if (t.EndsWith(";", StringComparison.Ordinal))
                    {
                        out_.Add(sb.ToString());
                        sb.Length = 0;
                    }
                }
            }
            if (sb.ToString().Trim().Length > 0)
            {
                out_.Add(sb.ToString());
            }
            return out_;
        }

        internal static string LoadEmbedded(string fileName)
        {
            Assembly asm = typeof(MigrationRunner).Assembly;
            foreach (string name in asm.GetManifestResourceNames())
            {
                if (name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    using (Stream s = asm.GetManifestResourceStream(name))
                    using (var reader = new StreamReader(s, Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            throw new InvalidOperationException("embedded migration missing: " + fileName);
        }
    }
}

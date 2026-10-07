using System.Collections.Generic;
using System.Data.SQLite;
using CourtDesk.Domain;

namespace CourtDesk.Persistence
{
    public static class IntegrityChecker
    {
        public static bool QuickCheck(SQLiteConnection conn)
        {
            using (var cmd = new SQLiteCommand("PRAGMA quick_check;", conn))
            using (SQLiteDataReader r = cmd.ExecuteReader())
            {
                if (!r.Read())
                {
                    return false;
                }
                if (r.GetString(0) != "ok")
                {
                    return false;
                }
                return !r.Read();
            }
        }

        public static List<string> CheckInvariants(SQLiteConnection conn)
        {
            var bad = new List<string>();
            CheckI1(conn, bad);
            CheckI2(conn, bad);
            CheckI3(conn, bad);
            CheckI4(conn, bad);
            CheckI5(conn, bad);
            using (var cmd = new SQLiteCommand("PRAGMA foreign_key_check;", conn))
            using (SQLiteDataReader r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    bad.Add("I6 fk " + r.GetString(0) + " row " + r.GetInt64(1));
                }
            }
            return bad;
        }

        private static void CheckI1(SQLiteConnection conn, List<string> bad)
        {
            const string sql =
                "SELECT d.id FROM defendants d " +
                "WHERE (SELECT COUNT(*) FROM session_events e WHERE e.defendant_id = d.id " +
                "AND e.event_type IN ('CREATED','IMPORTED')) <> 1 " +
                "OR (SELECT e.id FROM session_events e WHERE e.defendant_id = d.id ORDER BY e.id LIMIT 1) <> " +
                "(SELECT e2.id FROM session_events e2 WHERE e2.defendant_id = d.id " +
                "AND e2.event_type IN ('CREATED','IMPORTED') ORDER BY e2.id LIMIT 1)";
            using (var cmd = new SQLiteCommand(sql, conn))
            using (SQLiteDataReader r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    bad.Add("I1 defendant " + r.GetInt64(0));
                }
            }
        }

        private static void CheckI2(SQLiteConnection conn, List<string> bad)
        {
            const string sql = "SELECT id FROM defendants WHERE status <> 'DETAINED' AND session_date IS NOT NULL";
            using (var cmd = new SQLiteCommand(sql, conn))
            using (SQLiteDataReader r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    bad.Add("I2 defendant " + r.GetInt64(0));
                }
            }
        }

        private static void CheckI3(SQLiteConnection conn, List<string> bad)
        {
            var store = new SqliteDefendantStore();
            var ctx = new DbContext(conn, "", "", false);
            foreach (Defendant d in store.ListAll(ctx))
            {
                string after;
                using (var cmd = new SQLiteCommand(
                    "SELECT after_json FROM session_events WHERE defendant_id = @id ORDER BY id DESC LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@id", d.Id);
                    object v = cmd.ExecuteScalar();
                    after = v == null ? null : v.ToString();
                }
                if (after == null || after != SnapshotJson.Write(DefendantSnapshot.FromDefendant(d)))
                {
                    bad.Add("I3 defendant " + d.Id);
                }
            }
        }

        private static void CheckI4(SQLiteConnection conn, List<string> bad)
        {
            const string sql =
                "SELECT e.id FROM session_events e WHERE e.undone_by_event_id IS NOT NULL " +
                "AND (SELECT COUNT(*) FROM session_events u WHERE u.undoes_event_id = e.id " +
                "AND u.event_type = 'UNDO') <> 1";
            using (var cmd = new SQLiteCommand(sql, conn))
            using (SQLiteDataReader r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    bad.Add("I4 event " + r.GetInt64(0));
                }
            }
        }

        private static void CheckI5(SQLiteConnection conn, List<string> bad)
        {
            const string sql =
                "SELECT d.id FROM defendants d WHERE d.circuit_id IS NOT NULL " +
                "AND (d.court_id IS NULL OR d.court_id <> " +
                "(SELECT court_id FROM circuits WHERE id = d.circuit_id))";
            using (var cmd = new SQLiteCommand(sql, conn))
            using (SQLiteDataReader r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    bad.Add("I5 defendant " + r.GetInt64(0));
                }
            }
        }
    }
}

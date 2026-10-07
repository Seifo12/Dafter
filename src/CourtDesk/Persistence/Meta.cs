using System.Data.SQLite;

namespace CourtDesk.Persistence
{
    internal static class Meta
    {
        public static string Get(SQLiteConnection conn, string key)
        {
            using (var cmd = new SQLiteCommand("SELECT value FROM app_meta WHERE key = @k", conn))
            {
                cmd.Parameters.AddWithValue("@k", key);
                object v = cmd.ExecuteScalar();
                return v == null ? null : v.ToString();
            }
        }

        public static void Set(SQLiteConnection conn, string key, string value)
        {
            using (var cmd = new SQLiteCommand("UPDATE app_meta SET value = @v WHERE key = @k", conn))
            {
                cmd.Parameters.AddWithValue("@v", value ?? "");
                cmd.Parameters.AddWithValue("@k", key);
                cmd.ExecuteNonQuery();
            }
        }

        public static long GetLong(SQLiteConnection conn, string key)
        {
            string v = Get(conn, key);
            long n;
            if (v != null && long.TryParse(v, out n))
            {
                return n;
            }
            return 0;
        }
    }
}

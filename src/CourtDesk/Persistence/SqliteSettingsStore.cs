using System.Data.SQLite;
using CourtDesk.Application.Ports;

namespace CourtDesk.Persistence
{
    public sealed class SqliteSettingsStore : ISettingsStore
    {
        string ISettingsStore.Get(ISession s, string key) { return Get((DbContext)s, key); }
        void ISettingsStore.Set(ISession s, string key, string value) { Set((DbContext)s, key, value); }

        public string Get(DbContext ctx, string key)
        {
            using (var cmd = new SQLiteCommand("SELECT value FROM settings WHERE key = @k", ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@k", key);
                object v = cmd.ExecuteScalar();
                return v == null ? null : v.ToString();
            }
        }

        public void Set(DbContext ctx, string key, string value)
        {
            using (var cmd = new SQLiteCommand("UPDATE settings SET value = @v, updated_at_utc = @now WHERE key = @k", ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@v", value ?? "");
                cmd.Parameters.AddWithValue("@now", ctx.UtcNowIso);
                cmd.Parameters.AddWithValue("@k", key);
                if (cmd.ExecuteNonQuery() == 1)
                {
                    return;
                }
            }
            using (var cmd = new SQLiteCommand(
                "INSERT INTO settings (key, value, updated_at_utc) VALUES (@k, @v, @now)", ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@k", key);
                cmd.Parameters.AddWithValue("@v", value ?? "");
                cmd.Parameters.AddWithValue("@now", ctx.UtcNowIso);
                cmd.ExecuteNonQuery();
            }
        }
    }
}

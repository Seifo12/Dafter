using System;
using System.Collections.Generic;
using System.Data.SQLite;
using CourtDesk.Application.Ports;

namespace CourtDesk.Persistence
{
    public sealed class SqliteRosterPrintLog : IRosterPrintLog
    {
        void IRosterPrintLog.Append(ISession s, string rosterDateIso, int count,
            IReadOnlyList<int> defendantIds, string operatorName)
        {
            var ctx = (DbContext)s;
            using (var cmd = new SQLiteCommand(
                "INSERT INTO roster_prints(roster_date, printed_at_utc, operator_name, " +
                "defendant_count, defendant_ids_json) VALUES(@d, @t, @o, @c, @j)", ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@d", rosterDateIso);
                cmd.Parameters.AddWithValue("@t", ctx.UtcNowIso);
                cmd.Parameters.AddWithValue("@o", operatorName ?? "");
                cmd.Parameters.AddWithValue("@c", count);
                cmd.Parameters.AddWithValue("@j", IdsJson(defendantIds));
                cmd.ExecuteNonQuery();
            }
        }

        int IRosterPrintLog.CountFor(ISession s, string rosterDateIso)
        {
            var ctx = (DbContext)s;
            using (var cmd = new SQLiteCommand(
                "SELECT COUNT(*) FROM roster_prints WHERE roster_date = @d", ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@d", rosterDateIso);
                return System.Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        internal static string IdsJson(IReadOnlyList<int> ids)
        {
            var sb = new System.Text.StringBuilder("[");
            if (ids != null)
            {
                for (int i = 0; i < ids.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(',');
                    }
                    sb.Append(ids[i].ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            sb.Append(']');
            return sb.ToString();
        }
    }
}
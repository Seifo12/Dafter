using System.Collections.Generic;
using System.Data.SQLite;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;

namespace CourtDesk.Persistence
{
    public sealed class SqliteLookupStore : ILookupStore
    {
        List<CourtRow> ILookupStore.Courts(ISession s, bool a) { return Courts((DbContext)s, a); }
        CourtRow ILookupStore.GetCourt(ISession s, int id) { return GetCourt((DbContext)s, id); }
        int ILookupStore.InsertCourt(ISession s, string n) { return InsertCourt((DbContext)s, n); }
        void ILookupStore.RenameCourt(ISession s, int id, string n) { RenameCourt((DbContext)s, id, n); }
        void ILookupStore.SetCourtActive(ISession s, int id, bool a) { SetCourtActive((DbContext)s, id, a); }
        List<CircuitRow> ILookupStore.Circuits(ISession s, int c, bool a) { return Circuits((DbContext)s, c, a); }
        CircuitRow ILookupStore.GetCircuit(ISession s, int id) { return GetCircuit((DbContext)s, id); }
        int ILookupStore.InsertCircuit(ISession s, int c, string n) { return InsertCircuit((DbContext)s, c, n); }
        void ILookupStore.RenameCircuit(ISession s, int id, string n) { RenameCircuit((DbContext)s, id, n); }
        void ILookupStore.SetCircuitActive(ISession s, int id, bool a) { SetCircuitActive((DbContext)s, id, a); }
        List<CustodyRow> ILookupStore.CustodyLocations(ISession s, bool a) { return CustodyLocations((DbContext)s, a); }
        int ILookupStore.InsertCustody(ISession s, string n) { return InsertCustody((DbContext)s, n); }
        void ILookupStore.RenameCustody(ISession s, int id, string n) { RenameCustody((DbContext)s, id, n); }
        void ILookupStore.SetCustodyActive(ISession s, int id, bool a) { SetCustodyActive((DbContext)s, id, a); }
        List<ReasonRow> ILookupStore.Reasons(ISession s, bool a) { return Reasons((DbContext)s, a); }
        int ILookupStore.InsertReason(ISession s, string l) { return InsertReason((DbContext)s, l); }
        void ILookupStore.RenameReason(ISession s, int id, string l) { RenameReason((DbContext)s, id, l); }
        void ILookupStore.SetReasonActive(ISession s, int id, bool a) { SetReasonActive((DbContext)s, id, a); }
        void ILookupStore.RecordChargeUse(ISession s, string t) { RecordChargeUse((DbContext)s, t); }
        List<string> ILookupStore.SuggestCharges(ISession s, string p, int l) { return SuggestCharges((DbContext)s, p, l); }

        public List<CourtRow> Courts(DbContext ctx, bool activeOnly)
        {
            var out_ = new List<CourtRow>();
            string sql = "SELECT id, name, sort_order, is_active FROM courts" +
                (activeOnly ? " WHERE is_active = 1" : "") + " ORDER BY sort_order, name_norm";
            using (var cmd = new SQLiteCommand(sql, ctx.Connection))
            using (SQLiteDataReader r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    out_.Add(new CourtRow { Id = r.GetInt32(0), Name = r.GetString(1), SortOrder = r.GetInt32(2), IsActive = r.GetInt32(3) != 0 });
                }
            }
            return out_;
        }

        public CourtRow GetCourt(DbContext ctx, int id)
        {
            using (var cmd = new SQLiteCommand("SELECT id, name, sort_order, is_active FROM courts WHERE id = @id", ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (SQLiteDataReader r = cmd.ExecuteReader())
                {
                    if (!r.Read())
                    {
                        return null;
                    }
                    return new CourtRow { Id = r.GetInt32(0), Name = r.GetString(1), SortOrder = r.GetInt32(2), IsActive = r.GetInt32(3) != 0 };
                }
            }
        }

        public int InsertCourt(DbContext ctx, string name)
        {
            return InsertNamed(ctx, "courts", "name", "name_norm", name);
        }

        public void RenameCourt(DbContext ctx, int id, string name)
        {
            RenameNamed(ctx, "courts", "name", "name_norm", id, name);
        }

        public void SetCourtActive(DbContext ctx, int id, bool active)
        {
            SetActive(ctx, "courts", id, active);
        }

        public List<CircuitRow> Circuits(DbContext ctx, int courtId, bool activeOnly)
        {
            var out_ = new List<CircuitRow>();
            string sql = "SELECT id, court_id, name, is_active FROM circuits WHERE court_id = @c" +
                (activeOnly ? " AND is_active = 1" : "") + " ORDER BY name_norm";
            using (var cmd = new SQLiteCommand(sql, ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@c", courtId);
                using (SQLiteDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        out_.Add(new CircuitRow { Id = r.GetInt32(0), CourtId = r.GetInt32(1), Name = r.GetString(2), IsActive = r.GetInt32(3) != 0 });
                    }
                }
            }
            return out_;
        }

        public CircuitRow GetCircuit(DbContext ctx, int id)
        {
            using (var cmd = new SQLiteCommand("SELECT id, court_id, name, is_active FROM circuits WHERE id = @id", ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (SQLiteDataReader r = cmd.ExecuteReader())
                {
                    if (!r.Read())
                    {
                        return null;
                    }
                    return new CircuitRow { Id = r.GetInt32(0), CourtId = r.GetInt32(1), Name = r.GetString(2), IsActive = r.GetInt32(3) != 0 };
                }
            }
        }

        public int InsertCircuit(DbContext ctx, int courtId, string name)
        {
            using (var cmd = new SQLiteCommand(
                "INSERT INTO circuits (court_id, name, name_norm, created_at_utc) VALUES (@c, @n, @nn, @now)", ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@c", courtId);
                cmd.Parameters.AddWithValue("@n", name);
                cmd.Parameters.AddWithValue("@nn", ArabicText.Normalize(name));
                cmd.Parameters.AddWithValue("@now", ctx.UtcNowIso);
                cmd.ExecuteNonQuery();
            }
            return (int)LastId(ctx);
        }

        public void RenameCircuit(DbContext ctx, int id, string name)
        {
            RenameNamed(ctx, "circuits", "name", "name_norm", id, name);
        }

        public void SetCircuitActive(DbContext ctx, int id, bool active)
        {
            SetActive(ctx, "circuits", id, active);
        }

        public List<CustodyRow> CustodyLocations(DbContext ctx, bool activeOnly)
        {
            var out_ = new List<CustodyRow>();
            string sql = "SELECT id, name, sort_order, is_active FROM custody_locations" +
                (activeOnly ? " WHERE is_active = 1" : "") + " ORDER BY sort_order, name_norm";
            using (var cmd = new SQLiteCommand(sql, ctx.Connection))
            using (SQLiteDataReader r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    out_.Add(new CustodyRow { Id = r.GetInt32(0), Name = r.GetString(1), SortOrder = r.GetInt32(2), IsActive = r.GetInt32(3) != 0 });
                }
            }
            return out_;
        }

        public int InsertCustody(DbContext ctx, string name)
        {
            return InsertNamed(ctx, "custody_locations", "name", "name_norm", name);
        }

        public void RenameCustody(DbContext ctx, int id, string name)
        {
            RenameNamed(ctx, "custody_locations", "name", "name_norm", id, name);
        }

        public void SetCustodyActive(DbContext ctx, int id, bool active)
        {
            SetActive(ctx, "custody_locations", id, active);
        }

        public List<ReasonRow> Reasons(DbContext ctx, bool activeOnly)
        {
            var out_ = new List<ReasonRow>();
            string sql = "SELECT id, label, sort_order, is_active FROM postponement_reasons" +
                (activeOnly ? " WHERE is_active = 1" : "") + " ORDER BY sort_order, label_norm";
            using (var cmd = new SQLiteCommand(sql, ctx.Connection))
            using (SQLiteDataReader r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    out_.Add(new ReasonRow { Id = r.GetInt32(0), Label = r.GetString(1), SortOrder = r.GetInt32(2), IsActive = r.GetInt32(3) != 0 });
                }
            }
            return out_;
        }

        public int InsertReason(DbContext ctx, string label)
        {
            using (var cmd = new SQLiteCommand(
                "INSERT INTO postponement_reasons (label, label_norm) VALUES (@l, @ln)", ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@l", label);
                cmd.Parameters.AddWithValue("@ln", ArabicText.Normalize(label));
                cmd.ExecuteNonQuery();
            }
            return (int)LastId(ctx);
        }

        public void RenameReason(DbContext ctx, int id, string label)
        {
            RenameNamed(ctx, "postponement_reasons", "label", "label_norm", id, label);
        }

        public void SetReasonActive(DbContext ctx, int id, bool active)
        {
            SetActive(ctx, "postponement_reasons", id, active);
        }

        public void RecordChargeUse(DbContext ctx, string text)
        {
            string norm = ArabicText.Normalize(text);
            using (var cmd = new SQLiteCommand(
                "UPDATE charge_suggestions SET use_count = use_count + 1, last_used_at_utc = @now WHERE text_norm = @n",
                ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@now", ctx.UtcNowIso);
                cmd.Parameters.AddWithValue("@n", norm);
                if (cmd.ExecuteNonQuery() == 1)
                {
                    return;
                }
            }
            using (var cmd = new SQLiteCommand(
                "INSERT INTO charge_suggestions (text, text_norm, use_count, last_used_at_utc) VALUES (@t, @n, 1, @now)",
                ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@t", text.Trim());
                cmd.Parameters.AddWithValue("@n", norm);
                cmd.Parameters.AddWithValue("@now", ctx.UtcNowIso);
                cmd.ExecuteNonQuery();
            }
        }

        public List<string> SuggestCharges(DbContext ctx, string prefix, int limit)
        {
            var out_ = new List<string>();
            string norm = ArabicText.Normalize(prefix ?? "");
            const string sql = "SELECT text FROM charge_suggestions WHERE text_norm LIKE @p ESCAPE '\\' " +
                "ORDER BY use_count DESC, last_used_at_utc DESC LIMIT @l";
            using (var cmd = new SQLiteCommand(sql, ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@p", EscapeLike(norm) + "%");
                cmd.Parameters.AddWithValue("@l", limit);
                using (SQLiteDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        out_.Add(r.GetString(0));
                    }
                }
            }
            return out_;
        }

        private static int InsertNamed(DbContext ctx, string table, string col, string normCol, string value)
        {
            using (var cmd = new SQLiteCommand(
                "INSERT INTO " + table + " (" + col + ", " + normCol + ", created_at_utc) VALUES (@v, @n, @now)", ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@v", value);
                cmd.Parameters.AddWithValue("@n", ArabicText.Normalize(value));
                cmd.Parameters.AddWithValue("@now", ctx.UtcNowIso);
                cmd.ExecuteNonQuery();
            }
            return (int)LastId(ctx);
        }

        private static void RenameNamed(DbContext ctx, string table, string col, string normCol, int id, string value)
        {
            using (var cmd = new SQLiteCommand(
                "UPDATE " + table + " SET " + col + " = @v, " + normCol + " = @n WHERE id = @id", ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@v", value);
                cmd.Parameters.AddWithValue("@n", ArabicText.Normalize(value));
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        private static void SetActive(DbContext ctx, string table, int id, bool active)
        {
            using (var cmd = new SQLiteCommand(
                "UPDATE " + table + " SET is_active = @a WHERE id = @id", ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@a", active ? 1 : 0);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        private static long LastId(DbContext ctx)
        {
            using (var cmd = new SQLiteCommand("SELECT last_insert_rowid();", ctx.Connection))
            {
                return (long)cmd.ExecuteScalar();
            }
        }

        private static string EscapeLike(string s)
        {
            return s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        }
    }
}

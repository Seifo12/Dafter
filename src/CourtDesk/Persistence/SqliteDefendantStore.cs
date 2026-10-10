using System.Collections.Generic;
using System.Data.SQLite;
using System.Text;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;

namespace CourtDesk.Persistence
{
    public sealed class SqliteDefendantStore : IDefendantStore
    {
        private const string Cols = "id, full_name, name_norm, national_id, case_number, case_year, " +
            "case_type, charges, court_id, circuit_id, custody_location_id, session_date, status, " +
            "detained_since, notes, is_deleted, row_version";

        private const string ColsD = "d.id, d.full_name, d.name_norm, d.national_id, d.case_number, d.case_year, " +
            "d.case_type, d.charges, d.court_id, d.circuit_id, d.custody_location_id, d.session_date, d.status, " +
            "d.detained_since, d.notes, d.is_deleted, d.row_version";

        Defendant IDefendantStore.GetById(ISession s, int id) { return GetById((DbContext)s, id); }
        int IDefendantStore.Insert(ISession s, Defendant d) { return Insert((DbContext)s, d); }
        bool IDefendantStore.UpdateFull(ISession s, Defendant d) { return UpdateFull((DbContext)s, d); }
        bool IDefendantStore.SetDeleted(ISession s, int id, int v, bool d) { return SetDeleted((DbContext)s, id, v, d); }
        List<Defendant> IDefendantStore.ListDetainedOn(ISession s, string d) { return ListDetainedOn((DbContext)s, d); }
        List<Defendant> IDefendantStore.ListOverdue(ISession s, string d) { return ListOverdue((DbContext)s, d); }
        int IDefendantStore.CountOn(ISession s, string d) { return CountOn((DbContext)s, d); }
        int IDefendantStore.CountNoDate(ISession s) { return CountNoDate((DbContext)s); }
        int IDefendantStore.CountActive(ISession s) { return CountActive((DbContext)s); }
        List<Defendant> IDefendantStore.ListActive(ISession s) { return ListActive((DbContext)s); }
        List<DefendantRow> IDefendantStore.ListPendingOn(ISession s, string d) { return ListPendingOn((DbContext)s, d); }
        List<DefendantRow> IDefendantStore.ListPendingAll(ISession s) { return ListPendingAll((DbContext)s); }
        List<DefendantRow> IDefendantStore.ListOverdueRows(ISession s, string d) { return ListOverdueRows((DbContext)s, d); }
        List<DecidedRow> IDefendantStore.ListDecidedOn(ISession s, string d) { return ListDecidedOn((DbContext)s, d); }
        List<DecidedRow> IDefendantStore.ListDecidedAll(ISession s) { return ListDecidedAll((DbContext)s); }
        List<DefendantRow> IDefendantStore.ListActiveRows(ISession s) { return ListActiveRows((DbContext)s); }
        List<DefendantRow> IDefendantStore.ListRowsByIds(ISession s, int[] ids) { return ListRowsByIds((DbContext)s, ids); }
        List<Defendant> IDefendantStore.ListDeleted(ISession s) { return ListDeleted((DbContext)s); }
        string IDefendantStore.CourtName(ISession s, int? id) { return CourtName((DbContext)s, id); }
        string IDefendantStore.CircuitName(ISession s, int? id) { return CircuitName((DbContext)s, id); }
        string IDefendantStore.CustodyName(ISession s, int? id) { return CustodyName((DbContext)s, id); }

        public List<DefendantRow> ListPendingOn(DbContext ctx, string dateIso)
        {
            const string sql = "SELECT " + ColsD + ", c.name, cc.name, cu.name FROM defendants d " +
                "LEFT JOIN courts c ON c.id = d.court_id " +
                "LEFT JOIN circuits cc ON cc.id = d.circuit_id " +
                "LEFT JOIN custody_locations cu ON cu.id = d.custody_location_id " +
                "WHERE d.is_deleted = 0 AND d.status = 'DETAINED' AND d.session_date = @d " +
                "ORDER BY COALESCE(c.sort_order, 9999), COALESCE(c.name_norm, ''), " +
                "COALESCE(cc.name_norm, ''), d.name_norm";
            return RowQuery(ctx, sql, "@d", dateIso);
        }

        public List<DefendantRow> ListPendingAll(DbContext ctx)
        {
            const string sql = "SELECT " + ColsD + ", c.name, cc.name, cu.name FROM defendants d " +
                "LEFT JOIN courts c ON c.id = d.court_id " +
                "LEFT JOIN circuits cc ON cc.id = d.circuit_id " +
                "LEFT JOIN custody_locations cu ON cu.id = d.custody_location_id " +
                "WHERE d.is_deleted = 0 AND d.status = 'DETAINED' " +
                "ORDER BY COALESCE(c.sort_order, 9999), COALESCE(c.name_norm, ''), " +
                "COALESCE(cc.name_norm, ''), d.name_norm";
            return RowQuery(ctx, sql, null, null);
        }

        public List<DefendantRow> ListOverdueRows(DbContext ctx, string dateIso)
        {
            const string sql = "SELECT " + ColsD + ", c.name, cc.name, cu.name FROM defendants d " +
                "LEFT JOIN courts c ON c.id = d.court_id " +
                "LEFT JOIN circuits cc ON cc.id = d.circuit_id " +
                "LEFT JOIN custody_locations cu ON cu.id = d.custody_location_id " +
                "WHERE d.is_deleted = 0 AND d.status = 'DETAINED' AND d.session_date < @d " +
                "ORDER BY d.session_date ASC, d.name_norm";
            return RowQuery(ctx, sql, "@d", dateIso);
        }

        public List<DecidedRow> ListDecidedOn(DbContext ctx, string heldDateIso)
        {
            var out_ = new List<DecidedRow>();
            const string sql =
                "SELECT d.id, d.full_name, d.name_norm, d.national_id, d.case_number, d.case_year, " +
                "d.case_type, d.charges, d.court_id, d.circuit_id, d.custody_location_id, d.session_date, " +
                "d.status, d.detained_since, d.notes, d.is_deleted, d.row_version, " +
                "e.id, e.decision_code, e.next_session_date, e.court_name_at_event, e.circuit_name_at_event, e.bail_amount " +
                "FROM session_events e JOIN defendants d ON d.id = e.defendant_id " +
                "WHERE e.event_type = 'DECISION' AND e.decision_code <> 'REOPEN' " +
                "AND e.session_date_held = @d AND e.undone_by_event_id IS NULL AND d.is_deleted = 0 " +
                "ORDER BY e.id";
            using (var cmd = new SQLiteCommand(sql, ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@d", heldDateIso);
                using (SQLiteDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        out_.Add(ReadDecidedRow(r));
                    }
                }
            }
            return out_;
        }

        public List<DecidedRow> ListDecidedAll(DbContext ctx)
        {
            var out_ = new List<DecidedRow>();
            const string sql =
                "SELECT d.id, d.full_name, d.name_norm, d.national_id, d.case_number, d.case_year, " +
                "d.case_type, d.charges, d.court_id, d.circuit_id, d.custody_location_id, d.session_date, " +
                "d.status, d.detained_since, d.notes, d.is_deleted, d.row_version, " +
                "e.id, e.decision_code, e.next_session_date, e.court_name_at_event, e.circuit_name_at_event, e.bail_amount " +
                "FROM session_events e JOIN defendants d ON d.id = e.defendant_id " +
                "WHERE e.event_type = 'DECISION' AND e.decision_code <> 'REOPEN' " +
                "AND e.undone_by_event_id IS NULL AND d.is_deleted = 0 " +
                "ORDER BY e.id";
            using (var cmd = new SQLiteCommand(sql, ctx.Connection))
            {
                using (SQLiteDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        out_.Add(ReadDecidedRow(r));
                    }
                }
            }
            return out_;
        }

        private static DecidedRow ReadDecidedRow(SQLiteDataReader r)
        {
            return new DecidedRow
            {
                D = ReadRow(r),
                EventId = r.GetInt64(17),
                DecisionCode = Str(r, 18),
                NextSessionDate = Str(r, 19),
                CourtNameAtEvent = Str(r, 20),
                CircuitNameAtEvent = Str(r, 21),
                BailAmount = r.FieldCount > 22 && !r.IsDBNull(22)
                    ? (int?)r.GetInt32(22) : null
            };
        }

        public List<DefendantRow> ListActiveRows(DbContext ctx)
        {
            const string sql = "SELECT " + ColsD + ", c.name, cc.name, cu.name FROM defendants d " +
                "LEFT JOIN courts c ON c.id = d.court_id " +
                "LEFT JOIN circuits cc ON cc.id = d.circuit_id " +
                "LEFT JOIN custody_locations cu ON cu.id = d.custody_location_id " +
                "WHERE d.is_deleted = 0 ORDER BY d.id";
            return RowQuery(ctx, sql, null, null);
        }

        public List<DefendantRow> ListRowsByIds(DbContext ctx, int[] ids)
        {
            var out_ = new List<DefendantRow>();
            if (ids == null || ids.Length == 0)
            {
                return out_;
            }
            var sb = new StringBuilder("SELECT ");
            sb.Append(ColsD);
            sb.Append(", c.name, cc.name, cu.name FROM defendants d ");
            sb.Append("LEFT JOIN courts c ON c.id = d.court_id ");
            sb.Append("LEFT JOIN circuits cc ON cc.id = d.circuit_id ");
            sb.Append("LEFT JOIN custody_locations cu ON cu.id = d.custody_location_id WHERE d.id IN (");
            for (int i = 0; i < ids.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }
                sb.Append("@i").Append(i);
            }
            sb.Append(')');
            using (var cmd = new SQLiteCommand(sb.ToString(), ctx.Connection))
            {
                for (int i = 0; i < ids.Length; i++)
                {
                    cmd.Parameters.AddWithValue("@i" + i, ids[i]);
                }
                using (SQLiteDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        out_.Add(new DefendantRow
                        {
                            D = ReadRow(r),
                            CourtName = Str(r, 17),
                            CircuitName = Str(r, 18),
                            CustodyName = Str(r, 19)
                        });
                    }
                }
            }
            return out_;
        }

        public List<Defendant> ListDeleted(DbContext ctx)
        {
            return Query(ctx, "SELECT " + Cols + " FROM defendants WHERE is_deleted = 1 ORDER BY id", null, null);
        }

        public string CourtName(DbContext ctx, int? id)
        {
            return NameOf(ctx, "courts", id);
        }

        public string CircuitName(DbContext ctx, int? id)
        {
            return NameOf(ctx, "circuits", id);
        }

        public string CustodyName(DbContext ctx, int? id)
        {
            return NameOf(ctx, "custody_locations", id);
        }

        private static string NameOf(DbContext ctx, string table, int? id)
        {
            if (!id.HasValue)
            {
                return null;
            }
            using (var cmd = new SQLiteCommand("SELECT name FROM " + table + " WHERE id = @id", ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@id", id.Value);
                object v = cmd.ExecuteScalar();
                return v == null ? null : v.ToString();
            }
        }

        private static List<DefendantRow> RowQuery(DbContext ctx, string sql, string param, string value)
        {
            var out_ = new List<DefendantRow>();
            using (var cmd = new SQLiteCommand(sql, ctx.Connection))
            {
                if (param != null)
                {
                    cmd.Parameters.AddWithValue(param, value);
                }
                using (SQLiteDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        out_.Add(new DefendantRow
                        {
                            D = ReadRow(r),
                            CourtName = Str(r, 17),
                            CircuitName = Str(r, 18),
                            CustodyName = Str(r, 19)
                        });
                    }
                }
            }
            return out_;
        }
        List<DuplicateHit> IDefendantStore.FindDuplicates(ISession s, string n, string c, int y, int e)
        {
            return FindDuplicates((DbContext)s, n, c, y, e);
        }

        public Defendant GetById(DbContext ctx, int id)
        {
            using (var cmd = new SQLiteCommand("SELECT " + Cols + " FROM defendants WHERE id = @id", ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (SQLiteDataReader r = cmd.ExecuteReader())
                {
                    if (!r.Read())
                    {
                        return null;
                    }
                    return ReadRow(r);
                }
            }
        }

        public int Insert(DbContext ctx, Defendant d)
        {
            const string sql =
                "INSERT INTO defendants (full_name, name_norm, national_id, case_number, case_year, " +
                "case_type, charges, court_id, circuit_id, custody_location_id, session_date, status, " +
                "detained_since, notes, is_deleted, row_version, created_at_utc, updated_at_utc) " +
                "VALUES (@n, @nn, @nid, @cn, @cy, @ct, @ch, @court, @circ, @cust, @sess, @st, @since, @notes, 0, 1, @now, @now)";
            using (var cmd = new SQLiteCommand(sql, ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@n", d.FullName);
                cmd.Parameters.AddWithValue("@nn", ArabicText.Normalize(d.FullName));
                cmd.Parameters.AddWithValue("@nid", N(d.NationalId));
                cmd.Parameters.AddWithValue("@cn", d.CaseNumber);
                cmd.Parameters.AddWithValue("@cy", d.CaseYear);
                cmd.Parameters.AddWithValue("@ct", d.CaseType ?? "");
                cmd.Parameters.AddWithValue("@ch", d.Charges ?? "");
                cmd.Parameters.AddWithValue("@court", N(d.CourtId));
                cmd.Parameters.AddWithValue("@circ", N(d.CircuitId));
                cmd.Parameters.AddWithValue("@cust", N(d.CustodyLocationId));
                cmd.Parameters.AddWithValue("@sess", N(Iso(d.SessionDate)));
                cmd.Parameters.AddWithValue("@st", d.Status);
                cmd.Parameters.AddWithValue("@since", N(Iso(d.DetainedSince)));
                cmd.Parameters.AddWithValue("@notes", d.Notes ?? "");
                cmd.Parameters.AddWithValue("@now", ctx.UtcNowIso);
                cmd.ExecuteNonQuery();
            }
            d.Id = (int)LastId(ctx);
            d.RowVersion = 1;
            d.NameNorm = ArabicText.Normalize(d.FullName);
            return d.Id;
        }

        public bool UpdateFull(DbContext ctx, Defendant d)
        {
            const string sql =
                "UPDATE defendants SET full_name = @n, name_norm = @nn, national_id = @nid, " +
                "case_number = @cn, case_year = @cy, case_type = @ct, charges = @ch, court_id = @court, " +
                "circuit_id = @circ, custody_location_id = @cust, session_date = @sess, status = @st, " +
                "detained_since = @since, notes = @notes, is_deleted = @del, " +
                "row_version = row_version + 1, updated_at_utc = @now " +
                "WHERE id = @id AND row_version = @v";
            using (var cmd = new SQLiteCommand(sql, ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@n", d.FullName);
                cmd.Parameters.AddWithValue("@nn", ArabicText.Normalize(d.FullName));
                cmd.Parameters.AddWithValue("@nid", N(d.NationalId));
                cmd.Parameters.AddWithValue("@cn", d.CaseNumber);
                cmd.Parameters.AddWithValue("@cy", d.CaseYear);
                cmd.Parameters.AddWithValue("@ct", d.CaseType ?? "");
                cmd.Parameters.AddWithValue("@ch", d.Charges ?? "");
                cmd.Parameters.AddWithValue("@court", N(d.CourtId));
                cmd.Parameters.AddWithValue("@circ", N(d.CircuitId));
                cmd.Parameters.AddWithValue("@cust", N(d.CustodyLocationId));
                cmd.Parameters.AddWithValue("@sess", N(Iso(d.SessionDate)));
                cmd.Parameters.AddWithValue("@st", d.Status);
                cmd.Parameters.AddWithValue("@since", N(Iso(d.DetainedSince)));
                cmd.Parameters.AddWithValue("@notes", d.Notes ?? "");
                cmd.Parameters.AddWithValue("@del", d.IsDeleted ? 1 : 0);
                cmd.Parameters.AddWithValue("@now", ctx.UtcNowIso);
                cmd.Parameters.AddWithValue("@id", d.Id);
                cmd.Parameters.AddWithValue("@v", d.RowVersion);
                if (cmd.ExecuteNonQuery() != 1)
                {
                    return false;
                }
            }
            d.RowVersion += 1;
            d.NameNorm = ArabicText.Normalize(d.FullName);
            return true;
        }

        public bool SetDeleted(DbContext ctx, int id, int expectedVersion, bool deleted)
        {
            using (var cmd = new SQLiteCommand(
                "UPDATE defendants SET is_deleted = @del, row_version = row_version + 1, updated_at_utc = @now " +
                "WHERE id = @id AND row_version = @v", ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@del", deleted ? 1 : 0);
                cmd.Parameters.AddWithValue("@now", ctx.UtcNowIso);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@v", expectedVersion);
                return cmd.ExecuteNonQuery() == 1;
            }
        }

        public List<Defendant> ListDetainedOn(DbContext ctx, string dateIso)
        {
            const string sql = "SELECT " + ColsD + " FROM defendants d " +
                "LEFT JOIN courts c ON c.id = d.court_id LEFT JOIN circuits cc ON cc.id = d.circuit_id " +
                "WHERE d.is_deleted = 0 AND d.status = 'DETAINED' AND d.session_date = @d " +
                "ORDER BY COALESCE(c.sort_order, 9999), COALESCE(c.name_norm, ''), " +
                "COALESCE(cc.name_norm, ''), d.name_norm";
            return Query(ctx, sql, "@d", dateIso);
        }

        public List<Defendant> ListOverdue(DbContext ctx, string dateIso)
        {
            const string sql = "SELECT " + Cols + " FROM defendants " +
                "WHERE is_deleted = 0 AND status = 'DETAINED' AND session_date < @d " +
                "ORDER BY session_date ASC, name_norm";
            return Query(ctx, sql, "@d", dateIso);
        }

        public int CountOn(DbContext ctx, string dateIso)
        {
            return Count(ctx,
                "SELECT COUNT(*) FROM defendants WHERE is_deleted = 0 AND status = 'DETAINED' AND session_date = @d",
                "@d", dateIso);
        }

        public int CountNoDate(DbContext ctx)
        {
            return Count(ctx,
                "SELECT COUNT(*) FROM defendants WHERE is_deleted = 0 AND status = 'DETAINED' AND session_date IS NULL",
                null, null);
        }

        public int CountActive(DbContext ctx)
        {
            return Count(ctx, "SELECT COUNT(*) FROM defendants WHERE is_deleted = 0", null, null);
        }

        public List<Defendant> ListActive(DbContext ctx)
        {
            return Query(ctx, "SELECT " + Cols + " FROM defendants WHERE is_deleted = 0 ORDER BY id", null, null);
        }

        public List<Defendant> ListAll(DbContext ctx)
        {
            return Query(ctx, "SELECT " + Cols + " FROM defendants ORDER BY id", null, null);
        }

        public List<DuplicateHit> FindDuplicates(DbContext ctx, string nameNorm, string caseNumber, int caseYear, int excludeId)
        {
            var out_ = new List<DuplicateHit>();
            const string sql = "SELECT id, full_name, case_number, case_year FROM defendants " +
                "WHERE is_deleted = 0 AND name_norm = @n AND case_number = @c AND case_year = @y AND id <> @e";
            using (var cmd = new SQLiteCommand(sql, ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@n", nameNorm);
                cmd.Parameters.AddWithValue("@c", caseNumber);
                cmd.Parameters.AddWithValue("@y", caseYear);
                cmd.Parameters.AddWithValue("@e", excludeId);
                using (SQLiteDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        out_.Add(new DuplicateHit
                        {
                            Id = r.GetInt32(0),
                            FullName = r.GetString(1),
                            CaseNumber = r.GetString(2),
                            CaseYear = r.GetInt32(3)
                        });
                    }
                }
            }
            return out_;
        }

        internal static Defendant ReadRow(SQLiteDataReader r)
        {
            return new Defendant
            {
                Id = r.GetInt32(0),
                FullName = r.GetString(1),
                NameNorm = r.GetString(2),
                NationalId = Str(r, 3),
                CaseNumber = r.GetString(4),
                CaseYear = r.GetInt32(5),
                CaseType = r.GetString(6),
                Charges = r.GetString(7),
                CourtId = Int(r, 8),
                CircuitId = Int(r, 9),
                CustodyLocationId = Int(r, 10),
                SessionDate = Date(Str(r, 11)),
                Status = r.GetString(12),
                DetainedSince = Date(Str(r, 13)),
                Notes = r.GetString(14),
                IsDeleted = r.GetInt32(15) != 0,
                RowVersion = r.GetInt32(16)
            };
        }

        private static List<Defendant> Query(DbContext ctx, string sql, string param, string value)
        {
            var out_ = new List<Defendant>();
            using (var cmd = new SQLiteCommand(sql, ctx.Connection))
            {
                if (param != null)
                {
                    cmd.Parameters.AddWithValue(param, value);
                }
                using (SQLiteDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        out_.Add(ReadRow(r));
                    }
                }
            }
            return out_;
        }

        private static int Count(DbContext ctx, string sql, string param, string value)
        {
            using (var cmd = new SQLiteCommand(sql, ctx.Connection))
            {
                if (param != null)
                {
                    cmd.Parameters.AddWithValue(param, value);
                }
                return System.Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        private static long LastId(DbContext ctx)
        {
            using (var cmd = new SQLiteCommand("SELECT last_insert_rowid();", ctx.Connection))
            {
                return (long)cmd.ExecuteScalar();
            }
        }

        internal static object N(object v) { return v ?? (object)System.DBNull.Value; }

        internal static object Iso(LocalDate? d) { return d.HasValue ? (object)d.Value.ToIso() : System.DBNull.Value; }

        internal static string Str(SQLiteDataReader r, int i) { return r.IsDBNull(i) ? null : r.GetString(i); }

        internal static int? Int(SQLiteDataReader r, int i) { return r.IsDBNull(i) ? (int?)null : r.GetInt32(i); }

        internal static long? Lng(SQLiteDataReader r, int i) { return r.IsDBNull(i) ? (long?)null : r.GetInt64(i); }

        internal static LocalDate? Date(string iso) { return iso == null ? (LocalDate?)null : LocalDate.Parse(iso); }
    }
}

using System.Collections.Generic;
using System.Data.SQLite;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;

namespace CourtDesk.Persistence
{
    public sealed class SqliteEventStore : IEventStore
    {
        private const string Cols = "id, request_uuid, defendant_id, event_type, decision_code, " +
            "session_date_held, next_session_date, reason_text, bail_amount, note, court_name_at_event, " +
            "circuit_name_at_event, before_json, after_json, undoes_event_id, undone_by_event_id, " +
            "operator_name, occurred_at_utc, occurred_local_date";

        bool IEventStore.ExistsByRequestUuid(ISession s, string u) { return ExistsByRequestUuid((DbContext)s, u); }
        SessionEvent IEventStore.GetByRequestUuid(ISession s, string u) { return GetByRequestUuid((DbContext)s, u); }
        long IEventStore.Insert(ISession s, SessionEvent e) { return Insert((DbContext)s, e); }
        SessionEvent IEventStore.Get(ISession s, long id) { return Get((DbContext)s, id); }
        long IEventStore.LatestId(ISession s, int d) { return LatestId((DbContext)s, d); }
        void IEventStore.MarkUndone(ISession s, long e, long u) { MarkUndone((DbContext)s, e, u); }
        List<SessionEvent> IEventStore.ListByDefendant(ISession s, int d) { return ListByDefendant((DbContext)s, d); }
        List<SessionEvent> IEventStore.ListDecisionsOn(ISession s, string d) { return ListDecisionsOn((DbContext)s, d); }
        int IEventStore.CountAll(ISession s) { return CountAll((DbContext)s); }
        List<SessionEvent> IEventStore.ListAllNewestFirst(ISession s) { return ListAllNewestFirst((DbContext)s); }

        public List<SessionEvent> ListAllNewestFirst(DbContext ctx)
        {
            var out_ = new List<SessionEvent>();
            using (var cmd = new SQLiteCommand("SELECT " + Cols + " FROM session_events ORDER BY id DESC", ctx.Connection))
            using (SQLiteDataReader r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    out_.Add(ReadRow(r));
                }
            }
            return out_;
        }

        public bool ExistsByRequestUuid(DbContext ctx, string uuid)
        {
            using (var cmd = new SQLiteCommand(
                "SELECT COUNT(*) FROM session_events WHERE request_uuid = @u", ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@u", uuid);
                return System.Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        public SessionEvent GetByRequestUuid(DbContext ctx, string uuid)
        {
            using (var cmd = new SQLiteCommand("SELECT " + Cols + " FROM session_events WHERE request_uuid = @u", ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@u", uuid);
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

        public long Insert(DbContext ctx, SessionEvent e)
        {
            const string sql =
                "INSERT INTO session_events (request_uuid, defendant_id, event_type, decision_code, " +
                "session_date_held, next_session_date, reason_text, bail_amount, note, court_name_at_event, " +
                "circuit_name_at_event, before_json, after_json, undoes_event_id, operator_name, " +
                "occurred_at_utc, occurred_local_date) VALUES (@u, @d, @t, @c, @held, @next, @reason, " +
                "@bail, @note, @court, @circ, @before, @after, @undoes, @op, @at, @local)";
            using (var cmd = new SQLiteCommand(sql, ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@u", SqliteDefendantStore.N(e.RequestUuid));
                cmd.Parameters.AddWithValue("@d", e.DefendantId);
                cmd.Parameters.AddWithValue("@t", e.EventType);
                cmd.Parameters.AddWithValue("@c", SqliteDefendantStore.N(e.DecisionCode));
                cmd.Parameters.AddWithValue("@held", SqliteDefendantStore.N(e.SessionDateHeld));
                cmd.Parameters.AddWithValue("@next", SqliteDefendantStore.N(e.NextSessionDate));
                cmd.Parameters.AddWithValue("@reason", SqliteDefendantStore.N(e.ReasonText));
                cmd.Parameters.AddWithValue("@bail", SqliteDefendantStore.N(e.BailAmount));
                cmd.Parameters.AddWithValue("@note", SqliteDefendantStore.N(e.Note));
                cmd.Parameters.AddWithValue("@court", SqliteDefendantStore.N(e.CourtNameAtEvent));
                cmd.Parameters.AddWithValue("@circ", SqliteDefendantStore.N(e.CircuitNameAtEvent));
                cmd.Parameters.AddWithValue("@before", SqliteDefendantStore.N(e.BeforeJson));
                cmd.Parameters.AddWithValue("@after", e.AfterJson);
                cmd.Parameters.AddWithValue("@undoes", SqliteDefendantStore.N(e.UndoesEventId));
                cmd.Parameters.AddWithValue("@op", e.OperatorName);
                cmd.Parameters.AddWithValue("@at", e.OccurredAtUtc);
                cmd.Parameters.AddWithValue("@local", e.OccurredLocalDate);
                cmd.ExecuteNonQuery();
            }
            using (var cmd = new SQLiteCommand("SELECT last_insert_rowid();", ctx.Connection))
            {
                e.Id = (long)cmd.ExecuteScalar();
            }
            return e.Id;
        }

        public SessionEvent Get(DbContext ctx, long id)
        {
            using (var cmd = new SQLiteCommand("SELECT " + Cols + " FROM session_events WHERE id = @id", ctx.Connection))
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

        public long LatestId(DbContext ctx, int defendantId)
        {
            using (var cmd = new SQLiteCommand(
                "SELECT COALESCE(MAX(id), 0) FROM session_events WHERE defendant_id = @d", ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@d", defendantId);
                return (long)cmd.ExecuteScalar();
            }
        }

        public void MarkUndone(DbContext ctx, long eventId, long undoId)
        {
            using (var cmd = new SQLiteCommand(
                "UPDATE session_events SET undone_by_event_id = @u WHERE id = @id", ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@u", undoId);
                cmd.Parameters.AddWithValue("@id", eventId);
                if (cmd.ExecuteNonQuery() != 1)
                {
                    throw new InvariantException("mark undone affected 0 rows");
                }
            }
        }

        public List<SessionEvent> ListByDefendant(DbContext ctx, int defendantId)
        {
            var out_ = new List<SessionEvent>();
            using (var cmd = new SQLiteCommand(
                "SELECT " + Cols + " FROM session_events WHERE defendant_id = @d ORDER BY id", ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@d", defendantId);
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

        public List<SessionEvent> ListDecisionsOn(DbContext ctx, string heldDateIso)
        {
            var out_ = new List<SessionEvent>();
            const string sql = "SELECT " + Cols + " FROM session_events " +
                "WHERE event_type = 'DECISION' AND decision_code <> 'REOPEN' " +
                "AND session_date_held = @d AND undone_by_event_id IS NULL ORDER BY id";
            using (var cmd = new SQLiteCommand(sql, ctx.Connection))
            {
                cmd.Parameters.AddWithValue("@d", heldDateIso);
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

        public int CountAll(DbContext ctx)
        {
            using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM session_events", ctx.Connection))
            {
                return System.Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        internal static SessionEvent ReadRow(SQLiteDataReader r)
        {
            return new SessionEvent
            {
                Id = r.GetInt64(0),
                RequestUuid = SqliteDefendantStore.Str(r, 1),
                DefendantId = r.GetInt32(2),
                EventType = r.GetString(3),
                DecisionCode = SqliteDefendantStore.Str(r, 4),
                SessionDateHeld = SqliteDefendantStore.Str(r, 5),
                NextSessionDate = SqliteDefendantStore.Str(r, 6),
                ReasonText = SqliteDefendantStore.Str(r, 7),
                BailAmount = r.IsDBNull(8) ? (int?)null : r.GetInt32(8),
                Note = SqliteDefendantStore.Str(r, 9),
                CourtNameAtEvent = SqliteDefendantStore.Str(r, 10),
                CircuitNameAtEvent = SqliteDefendantStore.Str(r, 11),
                BeforeJson = SqliteDefendantStore.Str(r, 12),
                AfterJson = r.GetString(13),
                UndoesEventId = SqliteDefendantStore.Lng(r, 14),
                UndoneByEventId = SqliteDefendantStore.Lng(r, 15),
                OperatorName = r.GetString(16),
                OccurredAtUtc = r.GetString(17),
                OccurredLocalDate = r.GetString(18)
            };
        }
    }
}

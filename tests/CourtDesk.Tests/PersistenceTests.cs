using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;
using CourtDesk.Persistence;
using Xunit;

namespace CourtDesk.Tests
{
    internal sealed class FakeClock : IClock
    {
        public LocalDate Today { get; set; }
        private DateTime _utc;

        public FakeClock()
        {
            Today = new LocalDate(2026, 10, 7);
            _utc = new DateTime(2026, 10, 7, 8, 0, 0, DateTimeKind.Utc);
        }

        public DateTime UtcNow
        {
            get
            {
                _utc = _utc.AddSeconds(1);
                return _utc;
            }
        }

        public void ResetTo(DateTime utc)
        {
            _utc = utc;
        }
    }

    internal static class TestDb
    {
        public static string NewHome()
        {
            string h = Path.Combine(Path.GetTempPath(), "cdt-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(h);
            return h;
        }

        public static void Wipe(string home)
        {
            try
            {
                Directory.Delete(home, true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        public static RecoveryResult Open(string home, FakeClock clock)
        {
            return DatabaseRecovery.OpenWithRecovery(home, (IClock)clock, null);
        }

        public static RecoveryResult Open(string home, IClock clock)
        {
            return DatabaseRecovery.OpenWithRecovery(home, clock, null);
        }

        public static SQLiteConnection Own(string dbPath)
        {
            var c = new SQLiteConnection("Data Source=" + dbPath + ";Version=3;Pooling=False");
            c.Open();
            using (var cmd = new SQLiteCommand("PRAGMA foreign_keys=ON;", c))
            {
                cmd.ExecuteNonQuery();
            }
            return c;
        }

        public static void Exec(SQLiteConnection c, string sql)
        {
            using (var cmd = new SQLiteCommand(sql, c))
            {
                cmd.ExecuteNonQuery();
            }
        }
    }

    [Trait("Category", "Persistence")]
    public sealed class PersistenceTests : IDisposable
    {
        private readonly List<string> _homes = new List<string>();
        private readonly List<DbGateway> _gateways = new List<DbGateway>();

        public void Dispose()
        {
            foreach (DbGateway g in _gateways)
            {
                try { g.Close(); }
                catch (Exception) { }
            }
            foreach (string h in _homes)
            {
                TestDb.Wipe(h);
            }
        }

        private FakeClock Clock()
        {
            return new FakeClock();
        }

        private DbGateway Open(string home, FakeClock clock)
        {
            _homes.Add(home);
            RecoveryResult rec = TestDb.Open(home, clock);
            _gateways.Add(rec.Gateway);
            return rec.Gateway;
        }

        private static Defendant NewDefendant()
        {
            return new Defendant
            {
                FullName = "Test Person",
                NationalId = null,
                CaseNumber = "12345",
                CaseYear = 2026,
                CaseType = "",
                Charges = "",
                CourtId = null,
                CircuitId = null,
                CustodyLocationId = null,
                SessionDate = new LocalDate(2026, 10, 6),
                Status = DefendantStatuses.Detained,
                DetainedSince = new LocalDate(2026, 9, 1),
                Notes = "",
                IsDeleted = false,
                RowVersion = 0
            };
        }

        private static long DecideTx(DbGateway db, FakeClock clock, IDefendantStore defendants,
            IEventStore events, int id, DecisionRequest req, string faultPoint)
        {
            return db.Write(s =>
            {
                Defendant row = defendants.GetById(s, id);
                ValidationResult vr = DecisionRules.Validate(row, req, clock.Today);
                if (vr.HasErrors)
                {
                    throw new ValidationException(vr);
                }
                if (faultPoint == "before-insert")
                {
                    throw new InvalidOperationException("injected");
                }
                DefendantSnapshot after = DecisionRules.Apply(DefendantSnapshot.FromDefendant(row), req, clock.Today);
                var ev = new SessionEvent
                {
                    RequestUuid = req.RequestUuid,
                    DefendantId = id,
                    EventType = EventTypes.Decision,
                    DecisionCode = req.Code,
                    SessionDateHeld = row.SessionDate.Value.ToIso(),
                    NextSessionDate = req.NextDate.HasValue ? req.NextDate.Value.ToIso() : null,
                    BeforeJson = SnapshotJson.Write(DefendantSnapshot.FromDefendant(row)),
                    AfterJson = SnapshotJson.Write(after),
                    OperatorName = "t",
                    OccurredAtUtc = "2026-10-07T08:00:00Z",
                    OccurredLocalDate = "2026-10-07"
                };
                long eid = events.Insert(s, ev);
                if (faultPoint == "after-insert")
                {
                    throw new InvalidOperationException("injected");
                }
                after.CopyTo(row);
                if (!defendants.UpdateFull(s, row))
                {
                    throw new ConcurrencyException();
                }
                if (faultPoint == "after-update")
                {
                    throw new InvalidOperationException("injected");
                }
                Defendant check = defendants.GetById(s, id);
                if (SnapshotJson.Write(DefendantSnapshot.FromDefendant(check)) != SnapshotJson.Write(after))
                {
                    throw new InvariantException("read-back");
                }
                if (events.LatestId(s, id) != eid)
                {
                    throw new InvariantException("latest");
                }
                return eid;
            });
        }

        [Fact]
        public void PragmasReadBackAndSchemaMigrated()
        {
            string home = TestDb.NewHome();
            _homes.Add(home);
            string db = DbPaths.Db(home);
            DbPaths.EnsureAll(home);
            SqliteBootstrap.PrepareDatabase(db, Clock());
            using (SQLiteConnection c = TestDb.Own(db))
            {
                Assert.Equal("wal", Scalar(c, "PRAGMA journal_mode;").ToLowerInvariant());
                Assert.Equal(0x43444B31L, Convert.ToInt64(Scalar(c, "PRAGMA application_id;")));
                Assert.Equal("3", Scalar(c, "PRAGMA user_version;"));
                Assert.Equal("ok", Scalar(c, "PRAGMA quick_check;"));
                foreach (string t in new string[] { "app_meta", "settings", "courts", "circuits", "custody_locations", "postponement_reasons", "charge_suggestions", "defendants", "session_events", "roster_prints", "medical_notes", "medical_images" })
                {
                    Assert.Equal(1, Convert.ToInt32(Scalar(c,
                        "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '" + t + "'")));
                }
            }
        }

        [Fact]
        public void ConstraintsAndTriggersReject()
        {
            string home = TestDb.NewHome();
            _homes.Add(home);
            string db = DbPaths.Db(home);
            DbPaths.EnsureAll(home);
            SqliteBootstrap.PrepareDatabase(db, Clock());
            using (SQLiteConnection c = TestDb.Own(db))
            {
                TestDb.Exec(c, "INSERT INTO courts (name, name_norm, created_at_utc) VALUES ('A', 'a', 't')");
                TestDb.Exec(c, "INSERT INTO courts (name, name_norm, created_at_utc) VALUES ('B', 'b', 't')");
                TestDb.Exec(c, "INSERT INTO circuits (court_id, name, name_norm, created_at_utc) VALUES (1, 'C', 'c', 't')");
                TestDb.Exec(c, InsertDefSql());
                Assert.Throws<SQLiteException>(() => TestDb.Exec(c, "DELETE FROM defendants WHERE id = 1"));
                Assert.Throws<SQLiteException>(() => TestDb.Exec(c,
                    "INSERT INTO defendants (full_name, name_norm, case_number, case_year, court_id, circuit_id, created_at_utc, updated_at_utc) " +
                    "VALUES ('X Y', 'x y', '9', 2026, 2, 1, 't', 't')"));
                Assert.Throws<SQLiteException>(() => TestDb.Exec(c,
                    "INSERT INTO defendants (full_name, name_norm, case_number, case_year, status, session_date, created_at_utc, updated_at_utc) " +
                    "VALUES ('X Y', 'x y', '9', 2026, 'RELEASED', '2026-10-01', 't', 't')"));
                Assert.Throws<SQLiteException>(() => TestDb.Exec(c,
                    "INSERT INTO defendants (full_name, name_norm, national_id, case_number, case_year, created_at_utc, updated_at_utc) " +
                    "VALUES ('X Y', 'x y', '123', '9', 2026, 't', 't')"));
                Assert.Throws<SQLiteException>(() => TestDb.Exec(c,
                    "INSERT INTO defendants (full_name, name_norm, case_number, case_year, created_at_utc, updated_at_utc) " +
                    "VALUES ('X Y', 'x y', '9A', 2026, 't', 't')"));
                TestDb.Exec(c, InsertEventSql("CREATED", null, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));
                Assert.Throws<SQLiteException>(() => TestDb.Exec(c, "DELETE FROM session_events WHERE id = 1"));
                Assert.Throws<SQLiteException>(() => TestDb.Exec(c, "UPDATE session_events SET note = 'x' WHERE id = 1"));
                Assert.Throws<SQLiteException>(() => TestDb.Exec(c, InsertEventSql("CREATED", null, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")));
                TestDb.Exec(c, "INSERT INTO session_events (defendant_id, event_type, undoes_event_id, before_json, after_json, operator_name, occurred_at_utc, occurred_local_date) VALUES (1, 'UNDO', 1, '{}', '{}', 't', 't', 't')");
                TestDb.Exec(c, "UPDATE session_events SET undone_by_event_id = 2 WHERE id = 1");
                Assert.Throws<SQLiteException>(() => TestDb.Exec(c, "UPDATE session_events SET note = 'y' WHERE id = 1"));
                TestDb.Exec(c, InsertEventSql("DECISION", "POSTPONE", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"));
                Assert.Throws<SQLiteException>(() => TestDb.Exec(c, InsertEventSql("DECISION", null, null)));
                Assert.Throws<SQLiteException>(() => TestDb.Exec(c,
                    "INSERT INTO session_events (request_uuid, defendant_id, event_type, decision_code, " +
                    "before_json, after_json, operator_name, occurred_at_utc, occurred_local_date) VALUES " +
                    "('cccccccccccccccccccccccccccccccc', 1, 'DECISION', 'POSTPONE', '{}', '{}', 't', 't', 't')"));
            }
        }

        [Fact]
        public void DecideIsAtomicAtEveryStep()
        {
            var clock = Clock();
            string home = TestDb.NewHome();
            DbGateway db = Open(home, clock);
            IDefendantStore defendants = new SqliteDefendantStore();
            IEventStore events = new SqliteEventStore();
            int id = db.Write(s => defendants.Insert(s, NewDefendant()));
            string uuid0 = new string('1', 32);
            db.Write(s =>
            {
                events.Insert(s, new SessionEvent
                {
                    RequestUuid = uuid0,
                    DefendantId = id,
                    EventType = EventTypes.Created,
                    AfterJson = SnapshotJson.Write(DefendantSnapshot.FromDefendant(defendants.GetById(s, id))),
                    OperatorName = "t",
                    OccurredAtUtc = "2026-10-07T08:00:00Z",
                    OccurredLocalDate = "2026-10-07"
                });
                return 0;
            });
            string beforeJson = db.Read(s => SnapshotJson.Write(DefendantSnapshot.FromDefendant(defendants.GetById(s, id))));
            long latestBefore = db.Read(s => events.LatestId(s, id));
            long seqBefore = db.CurrentChangeSeq();

            foreach (string point in new string[] { "before-insert", "after-insert", "after-update" })
            {
                var req = ValidRenew(id, 1);
                Assert.Throws<InvalidOperationException>(() => DecideTx(db, clock, defendants, events, id, req, point));
                Assert.Equal(beforeJson, db.Read(s => SnapshotJson.Write(DefendantSnapshot.FromDefendant(defendants.GetById(s, id)))));
                Assert.Equal(latestBefore, db.Read(s => events.LatestId(s, id)));
                Assert.Equal(seqBefore, db.CurrentChangeSeq());
            }

            long eid = DecideTx(db, clock, defendants, events, id, ValidRenew(id, 1), null);
            Assert.True(eid > latestBefore);
            Assert.Equal(seqBefore + 1, db.CurrentChangeSeq());
            Defendant row = db.Read(s => defendants.GetById(s, id));
            Assert.Equal(new LocalDate(2026, 10, 20), row.SessionDate);
            Assert.Empty(db.CheckInvariants());
        }

        [Fact]
        public void RequestUuidIsIdempotentAndVersionIsOptimistic()
        {
            var clock = Clock();
            string home = TestDb.NewHome();
            DbGateway db = Open(home, clock);
            IDefendantStore defendants = new SqliteDefendantStore();
            IEventStore events = new SqliteEventStore();
            int id = db.Write(s => defendants.Insert(s, NewDefendant()));
            string uuid = new string('2', 32);
            db.Write(s =>
            {
                events.Insert(s, new SessionEvent
                {
                    RequestUuid = uuid,
                    DefendantId = id,
                    EventType = EventTypes.Created,
                    AfterJson = SnapshotJson.Write(DefendantSnapshot.FromDefendant(defendants.GetById(s, id))),
                    OperatorName = "t",
                    OccurredAtUtc = "2026-10-07T08:00:00Z",
                    OccurredLocalDate = "2026-10-07"
                });
                return 0;
            });
            Assert.True(db.Read(s => events.ExistsByRequestUuid(s, uuid)));
            Assert.NotNull(db.Read(s => events.GetByRequestUuid(s, uuid)));
            Assert.Throws<DbException>(() => db.Write(s =>
            {
                events.Insert(s, new SessionEvent
                {
                    RequestUuid = uuid,
                    DefendantId = id,
                    EventType = EventTypes.Edited,
                    AfterJson = "{}",
                    OperatorName = "t",
                    OccurredAtUtc = "t",
                    OccurredLocalDate = "t"
                });
                return 0;
            }));

            bool stale = db.Read(s =>
            {
                Defendant row = defendants.GetById(s, id);
                row.RowVersion = 999;
                return defendants.UpdateFull(s, row);
            });
            Assert.False(stale);
            bool gone = db.Read(s => defendants.SetDeleted(s, id, 999, true));
            Assert.False(gone);
            bool ok = db.Write(s => defendants.SetDeleted(s, id, 1, true));
            Assert.True(ok);
            Assert.True(db.Read(s => defendants.GetById(s, id)).IsDeleted);
            db.Write(s =>
            {
                Defendant row = defendants.GetById(s, id);
                events.Insert(s, new SessionEvent
                {
                    RequestUuid = new string('7', 32),
                    DefendantId = id,
                    EventType = EventTypes.Deleted,
                    BeforeJson = SnapshotJson.Write(DeletedView(false, row)),
                    AfterJson = SnapshotJson.Write(DefendantSnapshot.FromDefendant(row)),
                    OperatorName = "t",
                    OccurredAtUtc = "2026-10-07T08:00:00Z",
                    OccurredLocalDate = "2026-10-07"
                });
                return 0;
            });
            Assert.Empty(db.CheckInvariants());
        }

        [Fact]
        public void UndoRoundTripAndDoubleMarkBlocked()
        {
            var clock = Clock();
            string home = TestDb.NewHome();
            DbGateway db = Open(home, clock);
            IDefendantStore defendants = new SqliteDefendantStore();
            IEventStore events = new SqliteEventStore();
            int id = db.Write(s => defendants.Insert(s, NewDefendant()));
            db.Write(s =>
            {
                events.Insert(s, new SessionEvent
                {
                    RequestUuid = new string('3', 32),
                    DefendantId = id,
                    EventType = EventTypes.Created,
                    AfterJson = SnapshotJson.Write(DefendantSnapshot.FromDefendant(defendants.GetById(s, id))),
                    OperatorName = "t",
                    OccurredAtUtc = "2026-10-07T08:00:00Z",
                    OccurredLocalDate = "2026-10-07"
                });
                return 0;
            });
            string createdJson = db.Read(s => SnapshotJson.Write(DefendantSnapshot.FromDefendant(defendants.GetById(s, id))));
            long did = DecideTx(db, clock, defendants, events, id, ValidRenew(id, 1), null);

            bool undone = db.Write(s =>
            {
                SessionEvent ev = events.Get(s, did);
                Defendant row = defendants.GetById(s, id);
                if (SnapshotJson.Write(DefendantSnapshot.FromDefendant(row)) != ev.AfterJson)
                {
                    throw new InvariantException("drift");
                }
                long undoId = events.Insert(s, new SessionEvent
                {
                    RequestUuid = new string('4', 32),
                    DefendantId = id,
                    EventType = EventTypes.Undo,
                    UndoesEventId = ev.Id,
                    BeforeJson = ev.AfterJson,
                    AfterJson = ev.BeforeJson,
                    OperatorName = "t",
                    OccurredAtUtc = "2026-10-07T08:00:00Z",
                    OccurredLocalDate = "2026-10-07"
                });
                DefendantSnapshot restored = SnapshotJson.Read(ev.BeforeJson);
                restored.CopyTo(row);
                if (!defendants.UpdateFull(s, row))
                {
                    throw new ConcurrencyException();
                }
                events.MarkUndone(s, ev.Id, undoId);
                return true;
            });
            Assert.True(undone);
            Assert.Equal(createdJson, db.Read(s => SnapshotJson.Write(DefendantSnapshot.FromDefendant(defendants.GetById(s, id)))));
            Assert.Throws<DbException>(() => db.Write(s =>
            {
                events.MarkUndone(s, did, 424242);
                return 0;
            }));
            long did2 = DecideTx(db, clock, defendants, events, id, ValidRenew(id, 3), null);
            Assert.True(did2 > did);
            Assert.Empty(db.CheckInvariants());
        }

        [Fact]
        public void InvariantsHoldAfterRandomOps()
        {
            var clock = Clock();
            string home = TestDb.NewHome();
            DbGateway db = Open(home, clock);
            IDefendantStore defendants = new SqliteDefendantStore();
            IEventStore events = new SqliteEventStore();
            var rnd = new Random(42);
            var ids = new List<int>();
            int uuid = 0;
            for (int i = 0; i < 5000; i++)
            {
                int pick = rnd.Next(100);
                try
                {
                    if (pick < 45 || ids.Count == 0)
                    {
                        var d = NewDefendant();
                        d.FullName = "Person " + i + " Name";
                        d.CaseNumber = (1000 + i).ToString();
                        string u = NextUuid(ref uuid);
                        int id = 0;
                        db.Write(s =>
                        {
                            id = defendants.Insert(s, d);
                            events.Insert(s, new SessionEvent
                            {
                                RequestUuid = u,
                                DefendantId = id,
                                EventType = EventTypes.Created,
                                AfterJson = SnapshotJson.Write(DefendantSnapshot.FromDefendant(d)),
                                OperatorName = "t",
                                OccurredAtUtc = "2026-10-07T08:00:00Z",
                                OccurredLocalDate = "2026-10-07"
                            });
                            return 0;
                        });
                        ids.Add(id);
                    }
                    else
                    {
                        int id = ids[rnd.Next(ids.Count)];
                        db.Write(s =>
                        {
                            Defendant row = defendants.GetById(s, id);
                            if (row == null)
                            {
                                return 0;
                            }
                            long latest = events.LatestId(s, id);
                            SessionEvent ev = latest == 0 ? null : events.Get(s, latest);
                            int op = rnd.Next(3);
                            if (op == 0)
                            {
                                string b = SnapshotJson.Write(DefendantSnapshot.FromDefendant(row));
                                row.Notes = "n" + i;
                                if (defendants.UpdateFull(s, row))
                                {
                                    events.Insert(s, Ev(u: NextUuid(ref uuid), id: id, type: EventTypes.Edited, before: b,
                                        after: SnapshotJson.Write(DefendantSnapshot.FromDefendant(row))));
                                }
                            }
                            else if (op == 1 && row.Status == DefendantStatuses.Detained
                                && row.SessionDate.HasValue && row.SessionDate.Value <= clock.Today)
                            {
                                var req = ValidRenew(id, row.RowVersion);
                                ValidationResult vr = DecisionRules.Validate(row, req, clock.Today);
                                if (!vr.HasErrors)
                                {
                                    DefendantSnapshot after = DecisionRules.Apply(DefendantSnapshot.FromDefendant(row), req, clock.Today);
                                    string b = SnapshotJson.Write(DefendantSnapshot.FromDefendant(row));
                                    events.Insert(s, Ev(u: NextUuid(ref uuid), id: id, type: EventTypes.Decision,
                                        code: req.Code, held: row.SessionDate.Value.ToIso(),
                                        next: req.NextDate.Value.ToIso(), before: b,
                                        after: SnapshotJson.Write(after)));
                                    after.CopyTo(row);
                                    defendants.UpdateFull(s, row);
                                }
                            }
                            else if (op == 2 && ev != null && !ev.UndoneByEventId.HasValue
                                && (ev.EventType == EventTypes.Decision || ev.EventType == EventTypes.Edited)
                                && SnapshotJson.Write(DefendantSnapshot.FromDefendant(row)) == ev.AfterJson)
                            {
                                long undoId = events.Insert(s, Ev(u: NextUuid(ref uuid), id: id, type: EventTypes.Undo,
                                    before: ev.AfterJson, after: ev.BeforeJson, undoes: ev.Id));
                                DefendantSnapshot restored = SnapshotJson.Read(ev.BeforeJson);
                                restored.CopyTo(row);
                                if (defendants.UpdateFull(s, row))
                                {
                                    events.MarkUndone(s, ev.Id, undoId);
                                }
                            }
                            return 0;
                        });
                    }
                }
                catch (DbException) { }
                catch (ValidationException) { }
                catch (ConcurrencyException) { }
            }
            Assert.Empty(db.CheckInvariants());
        }

        [Fact]
        public void BackupIsSelfContained()
        {
            var clock = Clock();
            string home = TestDb.NewHome();
            DbGateway db = Open(home, clock);
            IDefendantStore defendants = new SqliteDefendantStore();
            IEventStore events = new SqliteEventStore();
            db.Write(s =>
            {
                int id = defendants.Insert(s, NewDefendant());
                events.Insert(s, new SessionEvent
                {
                    RequestUuid = new string('5', 32),
                    DefendantId = id,
                    EventType = EventTypes.Created,
                    AfterJson = SnapshotJson.Write(DefendantSnapshot.FromDefendant(defendants.GetById(s, id))),
                    OperatorName = "t",
                    OccurredAtUtc = "2026-10-07T08:00:00Z",
                    OccurredLocalDate = "2026-10-07"
                });
                return 0;
            });
            var engine = new SqliteBackupEngine(db, clock, home);
            string dest = Path.Combine(home, "backups", "exit", "exit-test.db");
            BackupReport rep = engine.Snapshot(dest);
            Assert.True(File.Exists(dest));
            Assert.False(File.Exists(dest + "-wal"));
            Assert.Equal(rep.SizeBytes, new FileInfo(dest).Length);
            using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
            using (Stream fs = File.OpenRead(dest))
            {
                Assert.Equal(BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant(), rep.Sha256);
            }
            Assert.Equal(1, rep.DefendantCount);
            Assert.Equal(1, rep.EventCount);
            using (SQLiteConnection c = TestDb.Own(dest))
            {
                Assert.Equal("delete", Scalar(c, "PRAGMA journal_mode;").ToLowerInvariant());
                Assert.Equal("ok", Scalar(c, "PRAGMA quick_check;"));
            }
        }

        [Fact]
        public void RestoreSwapHappyPathAndBoundaries()
        {
            var clock = Clock();
            string home = TestDb.NewHome();
            _homes.Add(home);
            DbPaths.EnsureAll(home);
            string dbPath = DbPaths.Db(home);
            string marker = DbPaths.RestoreMarker(home);

            DbGateway db = Open(home, clock);
            IDefendantStore defendants = new SqliteDefendantStore();
            db.Write(s => { defendants.Insert(s, NewDefendant()); return 0; });
            var engine = new SqliteBackupEngine(db, clock, home);
            string cand = Path.Combine(home, "cand.db");
            engine.Snapshot(cand);
            db.Write(s => { defendants.Insert(s, NewDefendant()); return 0; });
            Assert.Equal(2, db.Read(s => defendants.CountActive(s)));
            foreach (DbGateway g in _gateways) { g.Close(); }
            _gateways.Clear();

            RestoreSwap.Install(dbPath, marker, DbPaths.PreOpDir(home), cand, clock);
            Assert.False(File.Exists(marker));
            Assert.False(File.Exists(dbPath + ".replaced"));
            Assert.Equal(1, Directory.GetFiles(DbPaths.PreOpDir(home), "pre-restore-*.db").Length);
            DbGateway db2 = Open(home, clock);
            Assert.Equal(1, db2.Read(s => defendants.CountActive(s)));

            File.WriteAllText(marker, "deadbeef");
            File.WriteAllText(dbPath + ".restoring", "junk");
            RestoreSwap.ResolvePending(dbPath, marker);
            Assert.False(File.Exists(marker));
            Assert.False(File.Exists(dbPath + ".restoring"));
            Assert.Equal(1, db2.Read(s => defendants.CountActive(s)));

            foreach (DbGateway g in _gateways) { g.Close(); }
            _gateways.Clear();
            File.WriteAllBytes(dbPath, new byte[] { 1, 2, 3, 4 });
            File.Copy(cand, dbPath + ".replaced", true);
            File.WriteAllText(marker, "deadbeef2");
            RestoreSwap.ResolvePending(dbPath, marker);
            Assert.False(File.Exists(marker));
            DbGateway db3 = Open(home, clock);
            Assert.Equal(1, db3.Read(s => defendants.CountActive(s)));
        }

        [Fact]
        public void StartupRecoveryRestoresNewestVerified()
        {
            var clock = Clock();
            string homeA = TestDb.NewHome();
            _homes.Add(homeA);
            DbGateway dbA = Open(homeA, clock);
            IDefendantStore defendants = new SqliteDefendantStore();
            IEventStore events = new SqliteEventStore();
            dbA.Write(s =>
            {
                int id = defendants.Insert(s, NewDefendant());
                events.Insert(s, new SessionEvent
                {
                    RequestUuid = new string('6', 32),
                    DefendantId = id,
                    EventType = EventTypes.Created,
                    AfterJson = SnapshotJson.Write(DefendantSnapshot.FromDefendant(defendants.GetById(s, id))),
                    OperatorName = "t",
                    OccurredAtUtc = "2026-10-07T08:00:00Z",
                    OccurredLocalDate = "2026-10-07"
                });
                return 0;
            });
            string homeB = TestDb.NewHome();
            _homes.Add(homeB);
            DbPaths.EnsureAll(homeB);
            new SqliteBackupEngine(dbA, clock, homeA).Snapshot(Path.Combine(DbPaths.AutoDir(homeB), "auto-0.db"));
            foreach (DbGateway g in _gateways) { g.Close(); }
            _gateways.Clear();

            File.WriteAllBytes(DbPaths.Db(homeB), new byte[] { 5, 6, 7, 8, 9, 10 });
            RecoveryResult rec = TestDb.Open(homeB, clock);
            _gateways.Add(rec.Gateway);
            Assert.True(rec.WasRecovered);
            Assert.Equal(1, rec.Gateway.Read(s => defendants.CountActive(s)));
            Assert.Empty(rec.Gateway.CheckInvariants());

            string homeC = TestDb.NewHome();
            _homes.Add(homeC);
            DbPaths.EnsureAll(homeC);
            File.Copy(Path.Combine(DbPaths.AutoDir(homeB), "auto-0.db"), DbPaths.Db(homeC));
            byte[] raw = File.ReadAllBytes(DbPaths.Db(homeC));
            raw[100] = (byte)(raw[100] ^ 0xFF);
            File.WriteAllBytes(DbPaths.Db(homeC), raw);
            File.Copy(Path.Combine(DbPaths.AutoDir(homeB), "auto-0.db"), Path.Combine(DbPaths.AutoDir(homeC), "auto-0.db"));
            RecoveryResult recC = TestDb.Open(homeC, clock);
            _gateways.Add(recC.Gateway);
            Assert.True(recC.WasRecovered);

            string homeD = TestDb.NewHome();
            _homes.Add(homeD);
            DbPaths.EnsureAll(homeD);
            File.WriteAllBytes(DbPaths.Db(homeD), new byte[] { 1, 1, 1 });
            Assert.Throws<NoBackupException>(() => TestDb.Open(homeD, clock));
        }

        [Fact]
        public void UncleanExitIsNoticedOnce()
        {
            var clock = Clock();
            string home = TestDb.NewHome();
            RecoveryResult first = TestDb.Open(home, clock);
            _homes.Add(home);
            _gateways.Add(first.Gateway);
            Assert.False(first.WasUnclean);
            first.Gateway.Close();
            _gateways.Clear();
            RecoveryResult second = TestDb.Open(home, clock);
            _gateways.Add(second.Gateway);
            Assert.False(second.WasUnclean);
            second.Gateway.Close();
            _gateways.Clear();
            using (SQLiteConnection raw = TestDb.Own(DbPaths.Db(home)))
            {
                using (var cmd = new SQLiteCommand(
                    "UPDATE app_meta SET value = '0' WHERE key = 'clean_shutdown'", raw))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            RecoveryResult third = TestDb.Open(home, clock);
            _gateways.Add(third.Gateway);
            Assert.True(third.WasUnclean);
        }

        [Fact]
        public void SecondOpenerGetsBusy()
        {
            var clock = Clock();
            string home = TestDb.NewHome();
            Open(home, clock);
            Assert.Throws<BusyException>(() => SqliteBootstrap.PrepareDatabase(DbPaths.Db(home), clock));
        }

        private static DecisionRequest ValidRenew(int id, int version)
        {
            return new DecisionRequest
            {
                RequestUuid = Guid.NewGuid().ToString("N"),
                DefendantId = id,
                ExpectedVersion = version,
                Code = DecisionCodes.RenewDetention,
                NextDate = new LocalDate(2026, 10, 20)
            };
        }

        private static DefendantSnapshot DeletedView(bool deleted, Defendant row)
        {
            DefendantSnapshot s = DefendantSnapshot.FromDefendant(row);
            s.IsDeleted = deleted;
            return s;
        }

        private static SessionEvent Ev(string u, int id, string type, string code = null, string held = null,
            string next = null, string before = null, string after = null, long? undoes = null)
        {
            return new SessionEvent
            {
                RequestUuid = u,
                DefendantId = id,
                EventType = type,
                DecisionCode = code,
                SessionDateHeld = held,
                NextSessionDate = next,
                BeforeJson = before,
                AfterJson = after ?? "{}",
                UndoesEventId = undoes,
                OperatorName = "t",
                OccurredAtUtc = "2026-10-07T08:00:00Z",
                OccurredLocalDate = "2026-10-07"
            };
        }

        private static string NextUuid(ref int counter)
        {
            counter++;
            return counter.ToString("x").PadLeft(32, '0');
        }

        private static string InsertDefSql()
        {
            return "INSERT INTO defendants (full_name, name_norm, case_number, case_year, created_at_utc, updated_at_utc) " +
                "VALUES ('AB CD', 'ab cd', '123', 2026, 't', 't')";
        }

        private static string InsertEventSql(string type, string code, string uuid)
        {
            return "INSERT INTO session_events (request_uuid, defendant_id, event_type, decision_code, " +
                "session_date_held, before_json, after_json, operator_name, occurred_at_utc, occurred_local_date) VALUES (" +
                (uuid == null ? "NULL" : "'" + uuid + "'") + ", 1, '" + type + "', " +
                (code == null ? "NULL" : "'" + code + "'") + ", " +
                (type == "DECISION" ? "'2026-10-06'" : "NULL") + ", " +
                (type == "CREATED" ? "NULL" : "'{}'") + ", '{}', 't', 't', 't')";
        }

        private static string Scalar(SQLiteConnection c, string sql)
        {
            using (var cmd = new SQLiteCommand(sql, c))
            {
                object v = cmd.ExecuteScalar();
                return v == null ? null : v.ToString();
            }
        }

        [Fact]
        public void ReadInsideDataChangedDoesNotDeadlock()
        {
            var clock = Clock();
            string home = TestDb.NewHome();
            _homes.Add(home);
            DbGateway db = Open(home, clock);
            int reads = 0;
            db.DataChanged += delegate(long seq)
            {
                reads = db.Read(s => 1);
            };
            var task = System.Threading.Tasks.Task.Run(new Action(delegate
            {
                db.Write(s => 0);
            }));
            Assert.True(task.Wait(15000), "write with re-entrant read deadlocked");
            for (int i = 0; i < 100 && reads == 0; i++)
            {
                System.Threading.Thread.Sleep(20);
            }
            Assert.Equal(1, reads);
        }

        [Fact]
        public void CleanExitDoesNotReportUncleanOnReopen()        {
            var clock = new FakeClock();
            string home = TestDb.NewHome();
            try
            {
                RecoveryResult first = TestDb.Open(home, clock);
                first.Gateway.Close();
                RecoveryResult second = TestDb.Open(home, clock);
                try
                {
                    Assert.False(second.WasRecovered);
                    Assert.False(second.WasUnclean);
                }
                finally
                {
                    second.Gateway.Close();
                }
            }
            finally
            {
                TestDb.Wipe(home);
            }
        }
    }
}

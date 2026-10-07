using System;
using System.Collections.Generic;
using System.IO;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;
using CourtDesk.Persistence;
using CourtDesk.Resources;

namespace CrashHarness
{
    internal static class Program
    {
        private sealed class Opts
        {
            public string Home;
            public int Ops = 30;
            public int Seed = 1;
            public string Ack;
            public int BackupEvery;
            public string BackupDir;
        }

        private static int Main(string[] args)
        {
            try
            {
                Opts o = Parse(args);
                return Run(o);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("harness: " + e.Message);
                return 2;
            }
        }

        private static Opts Parse(string[] args)
        {
            var o = new Opts();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--home" && i + 1 < args.Length) { o.Home = args[++i]; }
                else if (args[i] == "--ops" && i + 1 < args.Length) { o.Ops = int.Parse(args[++i]); }
                else if (args[i] == "--seed" && i + 1 < args.Length) { o.Seed = int.Parse(args[++i]); }
                else if (args[i] == "--ack" && i + 1 < args.Length) { o.Ack = args[++i]; }
                else if (args[i] == "--backup-every" && i + 1 < args.Length) { o.BackupEvery = int.Parse(args[++i]); }
                else if (args[i] == "--backup-dir" && i + 1 < args.Length) { o.BackupDir = args[++i]; }
            }
            if (string.IsNullOrEmpty(o.Home) || string.IsNullOrEmpty(o.Ack))
            {
                throw new ArgumentException("need --home and --ack");
            }
            return o;
        }

        private static int Run(Opts o)
        {
            var clock = new SystemClock();
            RecoveryResult rec = DatabaseRecovery.OpenWithRecovery(o.Home, clock, null);
            DbGateway db = rec.Gateway;
            var defendants = new SqliteDefendantStore();
            var events = new SqliteEventStore();
            var lookups = new SqliteLookupStore();
            var engine = new SqliteBackupEngine(db, clock, o.Home);
            db.Write(ctx =>
            {
                SeedData.SeedLookups((DbContext)ctx, Strings.SeedCourts, Strings.SeedCustody, Strings.SeedReasons);
                return 0;
            });
            int courtId = db.Read(ctx => FirstCourt((DbContext)ctx, lookups));
            var rnd = new Random(o.Seed);
            var ids = new List<int>();
            int backupTick = 0;
            using (var ack = new FileStream(o.Ack, FileMode.Append, FileAccess.Write, FileShare.Read))
            using (var ackw = new StreamWriter(ack))
            {
                for (int i = 0; i < o.Ops; i++)
                {
                    int pick = rnd.Next(100);
                    string uuid = Guid.NewGuid().ToString("N");
                    bool committed;
                    if (pick < 40 || ids.Count == 0)
                    {
                        ids.Add(Create(db, defendants, events, clock, courtId, rnd, i, uuid));
                        committed = true;
                    }
                    else if (pick < 60)
                    {
                        committed = Edit(db, defendants, events, clock, rnd, ids, uuid);
                    }
                    else if (pick < 85)
                    {
                        committed = Decide(db, defendants, events, clock, rnd, ids, uuid);
                    }
                    else
                    {
                        committed = Undo(db, defendants, events, clock, rnd, ids, uuid);
                    }
                    if (!committed)
                    {
                        continue;
                    }
                    ackw.WriteLine(uuid);
                    ackw.Flush();
                    ack.Flush(true);
                    if (o.BackupEvery > 0 && !string.IsNullOrEmpty(o.BackupDir))
                    {
                        backupTick++;
                        if (backupTick >= o.BackupEvery)
                        {
                            backupTick = 0;
                            if (!Directory.Exists(o.BackupDir))
                            {
                                Directory.CreateDirectory(o.BackupDir);
                            }
                            engine.Snapshot(Path.Combine(o.BackupDir, "auto-" + i + ".db"));
                        }
                    }
                }
            }
            db.Close();
            return 0;
        }

        private static int FirstCourt(DbContext ctx, SqliteLookupStore lookups)
        {
            List<CourtRow> courts = lookups.Courts(ctx, true);
            if (courts.Count == 0)
            {
                throw new InvalidOperationException("no courts");
            }
            return courts[0].Id;
        }

        private static int Create(DbGateway db, SqliteDefendantStore defendants, SqliteEventStore events,
            IClock clock, int courtId, Random rnd, int i, string uuid)
        {
            var d = new Defendant
            {
                FullName = "Harness " + rnd.Next(1000000) + " Case " + i,
                NationalId = null,
                CaseNumber = (100000 + rnd.Next(800000)).ToString(),
                CaseYear = 2026,
                CaseType = "",
                Charges = "charge " + i,
                CourtId = courtId,
                CircuitId = null,
                CustodyLocationId = null,
                SessionDate = clock.Today.AddDays(-rnd.Next(4)),
                Status = DefendantStatuses.Detained,
                DetainedSince = clock.Today.AddDays(-rnd.Next(30)),
                Notes = "n" + i,
                IsDeleted = false,
                RowVersion = 0
            };
            return db.Write(ctx =>
            {
                var c = (DbContext)ctx;
                int id = defendants.Insert(c, d);
                events.Insert(c, new SessionEvent
                {
                    RequestUuid = uuid,
                    DefendantId = id,
                    EventType = EventTypes.Created,
                    BeforeJson = null,
                    AfterJson = SnapshotJson.Write(DefendantSnapshot.FromDefendant(d)),
                    OperatorName = "h",
                    OccurredAtUtc = c.UtcNowIso,
                    OccurredLocalDate = c.LocalDateIso
                });
                return id;
            });
        }

        private static bool Edit(DbGateway db, SqliteDefendantStore defendants, SqliteEventStore events,
            IClock clock, Random rnd, List<int> ids, string uuid)
        {
            int id = ids[rnd.Next(ids.Count)];
            db.Write(ctx =>
            {
                var c = (DbContext)ctx;
                Defendant row = defendants.GetById(c, id);
                if (row == null)
                {
                    throw new InvalidOperationException("missing " + id);
                }
                string before = SnapshotJson.Write(DefendantSnapshot.FromDefendant(row));
                row.Notes = "edited " + uuid.Substring(0, 8);
                if (!defendants.UpdateFull(c, row))
                {
                    throw new ConcurrencyException();
                }
                events.Insert(c, new SessionEvent
                {
                    RequestUuid = uuid,
                    DefendantId = id,
                    EventType = EventTypes.Edited,
                    BeforeJson = before,
                    AfterJson = SnapshotJson.Write(DefendantSnapshot.FromDefendant(row)),
                    OperatorName = "h",
                    OccurredAtUtc = c.UtcNowIso,
                    OccurredLocalDate = c.LocalDateIso
                });
                return true;
            });
            return true;
        }

        private static bool Decide(DbGateway db, SqliteDefendantStore defendants, SqliteEventStore events,
            IClock clock, Random rnd, List<int> ids, string uuid)
        {
            int id = ids[rnd.Next(ids.Count)];
            return db.Write(ctx =>
            {
                var c = (DbContext)ctx;
                Defendant row = defendants.GetById(c, id);
                if (row == null)
                {
                    throw new InvalidOperationException("missing " + id);
                }
                if (row.Status == DefendantStatuses.Detained
                    && (!row.SessionDate.HasValue || row.SessionDate.Value > clock.Today))
                {
                    return false;
                }
                var req = new DecisionRequest
                {
                    RequestUuid = uuid,
                    DefendantId = id,
                    ExpectedVersion = row.RowVersion
                };
                if (row.Status == DefendantStatuses.Detained)
                {
                    int pick = rnd.Next(5);
                    if (pick <= 1)
                    {
                        req.Code = DecisionCodes.RenewDetention;
                        req.NextDate = clock.Today.AddDays(7);
                    }
                    else if (pick == 2)
                    {
                        req.Code = DecisionCodes.Postpone;
                        req.NextDate = clock.Today.AddDays(15);
                    }
                    else if (pick == 3)
                    {
                        req.Code = DecisionCodes.Release;
                    }
                    else
                    {
                        req.Code = DecisionCodes.Judgment;
                    }
                }
                else
                {
                    req.Code = DecisionCodes.Reopen;
                    req.NextDate = clock.Today.AddDays(7);
                }
                if (req.NextDate.HasValue && req.NextDate.Value.IsWeekend)
                {
                    req.NextDate = req.NextDate.Value.AddDays(2);
                    if (req.NextDate.Value.IsWeekend)
                    {
                        req.NextDate = req.NextDate.Value.AddDays(1);
                    }
                }
                ValidationResult vr = DecisionRules.Validate(row, req, clock.Today);
                if (vr.HasErrors)
                {
                    throw new ValidationException(vr);
                }
                DefendantSnapshot before = DefendantSnapshot.FromDefendant(row);
                DefendantSnapshot after = DecisionRules.Apply(DefendantSnapshot.FromDefendant(row), req, clock.Today);
                string held = row.Status == DefendantStatuses.Detained && row.SessionDate.HasValue
                    ? row.SessionDate.Value.ToIso() : clock.Today.ToIso();
                var ev = new SessionEvent
                {
                    RequestUuid = uuid,
                    DefendantId = id,
                    EventType = EventTypes.Decision,
                    DecisionCode = req.Code,
                    SessionDateHeld = held,
                    NextSessionDate = req.NextDate.HasValue ? req.NextDate.Value.ToIso() : null,
                    BeforeJson = SnapshotJson.Write(before),
                    AfterJson = SnapshotJson.Write(after),
                    OperatorName = "h",
                    OccurredAtUtc = c.UtcNowIso,
                    OccurredLocalDate = c.LocalDateIso
                };
                long eventId = events.Insert(c, ev);
                after.CopyTo(row);
                if (!defendants.UpdateFull(c, row))
                {
                    throw new ConcurrencyException();
                }
                Defendant check = defendants.GetById(c, id);
                if (SnapshotJson.Write(DefendantSnapshot.FromDefendant(check)) != SnapshotJson.Write(after))
                {
                    throw new InvariantException("read-back mismatch");
                }
                if (events.LatestId(c, id) != eventId)
                {
                    throw new InvariantException("latest mismatch");
                }
                return true;
            });
        }

        private static bool Undo(DbGateway db, SqliteDefendantStore defendants, SqliteEventStore events,
            IClock clock, Random rnd, List<int> ids, string uuid)
        {
            int id = ids[rnd.Next(ids.Count)];
            return db.Write(ctx =>
            {
                var c = (DbContext)ctx;
                long latest = events.LatestId(c, id);
                if (latest == 0)
                {
                    return false;
                }
                SessionEvent ev = events.Get(c, latest);
                if (ev.UndoneByEventId.HasValue)
                {
                    return false;
                }
                if (ev.EventType != EventTypes.Decision && ev.EventType != EventTypes.Edited)
                {
                    return false;
                }
                Defendant row = defendants.GetById(c, id);
                if (SnapshotJson.Write(DefendantSnapshot.FromDefendant(row)) != ev.AfterJson)
                {
                    throw new InvariantException("undo drift");
                }
                DefendantSnapshot restored = SnapshotJson.Read(ev.BeforeJson);
                var undo = new SessionEvent
                {
                    RequestUuid = uuid,
                    DefendantId = id,
                    EventType = EventTypes.Undo,
                    UndoesEventId = ev.Id,
                    BeforeJson = ev.AfterJson,
                    AfterJson = ev.BeforeJson,
                    OperatorName = "h",
                    OccurredAtUtc = c.UtcNowIso,
                    OccurredLocalDate = c.LocalDateIso
                };
                long undoId = events.Insert(c, undo);
                restored.CopyTo(row);
                if (!defendants.UpdateFull(c, row))
                {
                    throw new ConcurrencyException();
                }
                events.MarkUndone(c, ev.Id, undoId);
                return true;
            });
        }
    }
}

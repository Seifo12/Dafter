using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.IO;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;
using CourtDesk.Persistence;

namespace CourtDesk.Bootstrap
{
    public static class SelfTest
    {
        public static int Run(string homeArg)
        {
            string home = homeArg;
            if (string.IsNullOrEmpty(home))
            {
                home = Path.Combine(Path.GetTempPath(),
                    "courtdesk-selftest-" + Guid.NewGuid().ToString("N"));
            }
            var sw = Stopwatch.StartNew();
            try
            {
                NativeExtractReport native = NativeExtractor.Extract(home);
                if (!native.Ok)
                {
                    Console.Error.WriteLine("FAIL native: " + native.Detail);
                    return 1;
                }
                Console.WriteLine("native ok (" + native.Written.Count + " written, "
                    + native.Verified.Count + " verified)");

                var clock = new SystemClock();
                RecoveryResult rec = DatabaseRecovery.OpenWithRecovery(home, clock, new SynchronizationContext());
                try
                {
                    DbGateway db = rec.Gateway;
                    Console.WriteLine("schema ok (user_version="
                        + DbPaths.SupportedSchemaVersion + ")");
                    List<string> bad = db.CheckInvariants();
                    if (bad.Count > 0)
                    {
                        Console.Error.WriteLine("FAIL invariants: " + string.Join(",", bad.ToArray()));
                        return 2;
                    }
                    Console.WriteLine("invariants ok (I1-I6)");

                    var defendants = new SqliteDefendantStore();
                    var events = new SqliteEventStore();
                    var settings = new SettingsService(db, new SqliteSettingsStore());
                    db.Write(s =>
                    {
                        SeedData.SeedLookups((DbContext)s,
                            CourtDesk.Resources.Strings.SeedCourts,
                            CourtDesk.Resources.Strings.SeedCustody,
                            CourtDesk.Resources.Strings.SeedReasons);
                        return 0;
                    });
                    var svc = new DefendantService(db, defendants, events, clock, settings,
                        new NullLogger());
                    var decide = new DecisionService(db, defendants, events, clock, settings);
                    var undo = new UndoService(db, defendants, events, clock, settings);
                    CreateResult created = svc.Create(new DefendantInput
                    {
                        FullName = "selftest",
                        CaseNumber = "9999",
                        CaseYear = "2026",
                        SessionDate = clock.Today
                    }, Guid.NewGuid().ToString("N"), true);
                    Defendant d = svc.Get(created.Id);
                    DecideResult res = decide.Decide(new DecisionRequest
                    {
                        RequestUuid = Guid.NewGuid().ToString("N"),
                        DefendantId = d.Id,
                        ExpectedVersion = d.RowVersion,
                        Code = DecisionCodes.Release,
                        NextDate = null
                    });
                    if (res.AlreadyApplied)
                    {
                        Console.Error.WriteLine("FAIL decide: already applied");
                        return 3;
                    }
                    UndoResult back = undo.Undo(res.EventId, Guid.NewGuid().ToString("N"));
                    if (back.UndoEventId == 0)
                    {
                        Console.Error.WriteLine("FAIL undo");
                        return 4;
                    }
                    Console.WriteLine("ops ok (create, decide, undo)");

                    var engine = new SqliteBackupEngine(db, clock, home);
                    string snap = Path.Combine(home, "tmp", "selftest-snapshot.db");
                    Directory.CreateDirectory(Path.GetDirectoryName(snap));
                    BackupReport report = engine.Snapshot(snap);
                    if (report == null || string.IsNullOrEmpty(report.Sha256))
                    {
                        Console.Error.WriteLine("FAIL backup");
                        return 5;
                    }
                    RestoreSwap.VerifyBackup(report.Path);
                    Console.WriteLine("backup ok (sha256=" + report.Sha256.Substring(0, 12) + "...)");

                    bad = db.CheckInvariants();
                    if (bad.Count > 0)
                    {
                        Console.Error.WriteLine("FAIL invariants after ops: "
                            + string.Join(",", bad.ToArray()));
                        return 6;
                    }
                    Console.WriteLine("invariants ok after ops");
                }
                finally
                {
                    rec.Gateway.Close();
                }
                sw.Stop();
                Console.WriteLine("selftest OK in "
                    + sw.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture) + " ms, home="
                    + home);
                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("FAIL " + e.GetType().Name + ": " + e.Message);
                Console.Error.WriteLine(e.StackTrace);
                return 9;
            }
        }
    }
}
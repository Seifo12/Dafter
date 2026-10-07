using System;
using System.IO;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;
using CourtDesk.Persistence;
using CourtDesk.Platform;
using System.Collections.Generic;
using Xunit;

namespace CourtDesk.Tests
{
    [Trait("Category", "Resilience")]
    public sealed class ResilienceTests
    {
        private sealed class CountingActions : IBackupActions
        {
            public int Auto;
            public int Exit;
            public int Session;
            public int Reminder;
            public string LastPath;

            public void OnAutoSnapshot(string path)
            {
                Auto++;
                LastPath = path;
            }

            public void OnExitSnapshot(string path)
            {
                Exit++;
                LastPath = path;
            }

            public void OnSessionEnding() { Session++; }

            public void OnUsbReminder() { Reminder++; }
        }

        private sealed class Ctx
        {
            public string Home;
            public DbGateway Db;
            public BackupService Backups;
            public BackupScheduler Scheduler;
            public CountingActions Actions;
            public ILogger Log;
        }

        private static Ctx Open(string today)
        {
            var clock0 = new FixedClock();
            var ctx = new Ctx();
            ctx.Home = TestDb.NewHome();
            RecoveryResult rec = TestDb.Open(ctx.Home, new FixedClock());
            ctx.Db = rec.Gateway;
            var settings = new SettingsService(ctx.Db, new SqliteSettingsStore());
            ctx.Db.Write(s =>
            {
                SeedData.SeedLookups((DbContext)s,
                    CourtDesk.Resources.Strings.SeedCourts,
                    CourtDesk.Resources.Strings.SeedCustody,
                    CourtDesk.Resources.Strings.SeedReasons);
                return 0;
            });
            var engine = new SqliteBackupEngine(ctx.Db, clock0, ctx.Home);
            var drives = new PortsDriveProvider();
            var meta = new SqliteMetaStore();
            var clock = new FixedClock();
            ctx.Backups = new BackupService(ctx.Db, engine, drives, meta, clock, settings,
                BackupService.ForHome(ctx.Home), new NullLogger());
            ctx.Actions = new CountingActions();
            ctx.Log = new NullLogger();
            ctx.Scheduler = new BackupScheduler(ctx.Backups, ctx.Log, ctx.Actions);
            return ctx;
        }

        private static void Close(Ctx c)
        {
            try
            {
                c.Db.Close();
            }
            catch (Exception)
            {
            }
            TestDb.Wipe(c.Home);
        }

        [Fact]
        public void AutoSnapshotWritesVerifiedRingFile()
        {
            Ctx c = Open("auto");
            try
            {
                Assert.True(c.Scheduler.RunAutoSnapshotNow());
                Assert.Equal(1, c.Actions.Auto);
                Assert.True(File.Exists(c.Actions.LastPath));
                RestoreSwap.VerifyBackup(c.Actions.LastPath);
            }
            finally
            {
                Close(c);
            }
        }

        [Fact]
        public void AutoSnapshotRotatesTheTenFileRing()
        {
            Ctx c = Open("ring");
            try
            {
                for (int i = 0; i < BackupService.AutoRingSize + 4; i++)
                {
                    c.Scheduler.RunAutoSnapshotNow();
                }
                string[] files = Directory.GetFiles(
                    BackupService.ForHome(c.Home).AutoDir, "auto-*.db");
                Assert.True(files.Length <= BackupService.AutoRingSize,
                    "ring grew to " + files.Length);
            }
            finally
            {
                Close(c);
            }
        }

        [Fact]
        public void ExitSnapshotSkipsWhenNothingChanged()
        {
            Ctx c = Open("exit");
            try
            {
                Assert.True(c.Scheduler.RunExitSnapshotNow());
                Assert.Equal(1, c.Actions.Exit);
                Assert.False(c.Scheduler.RunExitSnapshotNow());
                Assert.Equal(1, c.Actions.Exit);
            }
            finally
            {
                Close(c);
            }
        }

        [Fact]
        public void SessionEndingTakesAnExitSnapshot()
        {
            Ctx c = Open("sess");
            try
            {
                c.Scheduler.RunSessionEnding();
                Assert.Equal(1, c.Actions.Exit);
                Assert.Equal(1, c.Actions.Session);
            }
            finally
            {
                Close(c);
            }
        }

        [Fact]
        public void RestoreFromSnapshotKeepsTheRosterIntact()
        {
            Ctx c = Open("restore");
            try
            {
                var defendants = new SqliteDefendantStore();
                var events = new SqliteEventStore();
                var settings = new SettingsService(c.Db, new SqliteSettingsStore());
                var clock = new FixedClock();
                var svc = new DefendantService(c.Db, defendants, events, clock, settings, new NullLogger());
                svc.Create(new DefendantInput
                {
                    FullName = "\u0623\u062d\u0645\u062f \u0645\u062d\u0645\u062f",
                    CaseNumber = "9001",
                    CaseYear = "2026",
                    SessionDate = new LocalDate(2026, 10, 7)
                }, Guid.NewGuid().ToString("N"), true);
                Assert.True(c.Scheduler.RunExitSnapshotNow());
                string backup = c.Actions.LastPath;

                int before = c.Db.Read(s => ((IDefendantStore)defendants).CountActive(s));
                svc.Create(new DefendantInput
                {
                    FullName = "\u0633\u0639\u062f \u0628\u0639\u062f \u0627\u0644\u0644\u0647",
                    CaseNumber = "9002",
                    CaseYear = "2026",
                    SessionDate = new LocalDate(2026, 10, 7)
                }, Guid.NewGuid().ToString("N"), true);
                int after = c.Db.Read(s => ((IDefendantStore)defendants).CountActive(s));
                Assert.Equal(before + 1, after);

var paths = BackupService.ForHome(c.Home);
                string dbPath = DbPaths.Db(c.Home);
                c.Db.Close();
                RestoreSwap.Install(dbPath, DbPaths.RestoreMarker(c.Home),
                    paths.PreOpDir, backup, clock);
                RestoreSwap.ResolvePending(dbPath, DbPaths.RestoreMarker(c.Home));

                RecoveryResult reopened = TestDb.Open(c.Home, clock);
                try
                {
                    int restored = reopened.Gateway.Read(s => ((IDefendantStore)defendants).CountActive(s));
                    Assert.Equal(before, restored);
                    List<string> bad = reopened.Gateway.CheckInvariants();
                    Assert.True(bad.Count == 0, string.Join(",", bad.ToArray()));
                }
                finally
                {
                    reopened.Gateway.Close();
                }
            }
            finally
            {
                Close(c);
            }
        }

        [Fact]
        public void FileLoggerWritesDailyFileAndRotates()
        {
            string home = TestDb.NewHome();
            try
            {
                var log = new FileLogger(home);
                log.Info("test.info", "hello");
                log.Warn("test.warn", "careful");
                log.Error("test.error", "boom");
                Assert.True(File.Exists(log.TodayPath()));
                string text = File.ReadAllText(log.TodayPath());
                Assert.Contains("test.info", text);
                Assert.Contains("test.warn", text);
                Assert.Contains("boom", text);
            }
            finally
            {
                TestDb.Wipe(home);
            }
        }

        [Fact]
        public void SingleInstanceGuardReportsOnlyOnePrimary()
        {
            string name = "Local\\CourtDesk.Test." + Guid.NewGuid().ToString("N");
            using (var a = new SingleInstanceGuard(name))
            {
                Assert.True(a.IsPrimary);
                using (var b = new SingleInstanceGuard(name))
                {
                    Assert.False(b.IsPrimary);
                }
            }
            using (var c = new SingleInstanceGuard(name))
            {
                Assert.True(c.IsPrimary);
            }
        }

        [Fact]
        public void InputAssistNormalizesArabicDigits()
        {
            Assert.Equal("1002/2026",
                InputLanguageAssist.NormalizeDigits("\u0661\u0660\u0660\u0662/2026"));
            Assert.Equal("123", InputLanguageAssist.NormalizeDigits("123"));
            Assert.True(InputLanguageAssist.HasArabicDigits("\u0665"));
            Assert.False(InputLanguageAssist.HasArabicDigits("555"));
        }

        [Fact]
public void ClockRegressionIsFlagged()
        {
            Ctx c = Open("clock");
            try
            {
                IMetaStore meta0 = new SqliteMetaStore();
                var stored = new MovableClock(new LocalDate(2026, 10, 7),
                    new DateTime(2026, 10, 7, 8, 0, 0, DateTimeKind.Utc));
                var svc = new ClockSanityService(c.Db, stored, meta0, new LocalDate(2026, 10, 7));

                c.Db.Write(s =>
                {
                    meta0.SetMeta(s, ClockSanityService.MaxSeenKey,
                        "2026-10-07T08:00:00Z");
                    return 0;
                });

                ClockVerdict ok = svc.Check();
                Assert.False(ok.Suspect, "clock matching max_seen must not be suspect");

                stored.Today2 = new LocalDate(2026, 10, 1);
                stored.Utc2 = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
                ClockVerdict verdict = svc.Check();
                Assert.True(verdict.Suspect, "regression not flagged");
                Assert.Equal(ClockReasons.Regression, verdict.Reason);

                stored.Today2 = new LocalDate(2020, 1, 1);
                stored.Utc2 = new DateTime(2020, 1, 1, 8, 0, 0, DateTimeKind.Utc);
                ClockVerdict before = svc.Check();
                Assert.True(before.Suspect, "date before build must be suspect");
                Assert.True(before.Reason == ClockReasons.BeforeBuild
                    || before.Reason == ClockReasons.Regression, "reason " + before.Reason);
            }
            finally
            {
                Close(c);
            }
        }

        private sealed class MovableClock : IClock
        {
            private readonly LocalDate _today;
            private readonly DateTime _utc;

            public LocalDate Today2;
            public DateTime Utc2;

            public MovableClock(LocalDate today, DateTime utc)
            {
                _today = today;
                _utc = utc;
                Today2 = today;
                Utc2 = utc;
            }

            public LocalDate Today { get { return Today2; } }

            public DateTime UtcNow { get { return Utc2; } }
        }

    }
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;
using CourtDesk.Persistence;
using Xunit;

namespace CourtDesk.Tests
{
    internal sealed class FakeDriveProvider : IDriveProvider
    {
        public readonly List<DriveItem> Drives = new List<DriveItem>();

        public DriveItem[] List()
        {
            return Drives.ToArray();
        }

        public DriveItem Find(string name)
        {
            foreach (DriveItem d in Drives)
            {
                if (d.Name == name)
                {
                    return d;
                }
            }
            return null;
        }

        public DriveItem Add(string name, DriveKind kind, long free)
        {
            var d = new DriveItem
            {
                Name = name,
                Kind = kind,
                FreeBytes = free,
                TotalBytes = free * 4,
                VolumeLabel = "T" + name.Length
            };
            Drives.Add(d);
            return d;
        }
    }

    internal sealed class ThrowingFaults : IDecisionFaults
    {
        private readonly string _point;

        public ThrowingFaults(string point)
        {
            _point = point;
        }

        public void Hit(string point)
        {
            if (point == _point)
            {
                throw new InjectedFailureException(point);
            }
        }
    }

    public sealed class InjectedFailureException : Exception
    {
        public InjectedFailureException(string point) : base("injected " + point) { }
    }

    [Trait("Category", "Services")]
    public sealed class ServiceTests : IDisposable
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

        private sealed class Env
        {
            public string Home;
            public DbGateway Db;
            public FakeClock Clock;
            public SettingsService Settings;
            public IDefendantStore Defendants;
            public IEventStore Events;
            public SqliteLookupStore Lookups;
            public ISettingsStore SettingStore;
            public IMetaStore Meta;
        }

        private Env NewEnv(string startDateIso = null)
        {
            var env = new Env();
            env.Home = TestDb.NewHome();
            _homes.Add(env.Home);
            env.Clock = new FakeClock();
            if (startDateIso != null)
            {
                env.Clock.Today = LocalDate.Parse(startDateIso);
            }
            RecoveryResult rec = TestDb.Open(env.Home, env.Clock);
            env.Db = rec.Gateway;
            _gateways.Add(env.Db);
            env.Defendants = new SqliteDefendantStore();
            env.Events = new SqliteEventStore();
            env.Lookups = new SqliteLookupStore();
            env.SettingStore = new SqliteSettingsStore();
            env.Meta = new SqliteMetaStore();
            env.Settings = new SettingsService(env.Db, env.SettingStore);
            env.Db.Write(s =>
            {
                SeedData.SeedLookups((DbContext)s,
                    CourtDesk.Resources.Strings.SeedCourts,
                    CourtDesk.Resources.Strings.SeedCustody,
                    CourtDesk.Resources.Strings.SeedReasons);
                return 0;
            });
            return env;
        }

        private static DefendantInput Input(string name, string caseNumber, string year = "2026")
        {
            return new DefendantInput
            {
                FullName = name,
                NationalId = null,
                CaseNumber = caseNumber,
                CaseYear = year,
                CaseType = "A",
                Charges = "C",
                CourtId = null,
                CircuitId = null,
                CustodyLocationId = null,
                SessionDate = new LocalDate(2026, 10, 6),
                DetainedSince = new LocalDate(2026, 9, 1),
                Notes = ""
            };
        }

        private static string Uuid()
        {
            return Guid.NewGuid().ToString("N");
        }

        [Fact]
        public void SettingsSeedDefaultsAndRoundTrip()
        {
            Env e = NewEnv();
            Assert.Equal("100", e.Settings.Get(SettingKeys.FontScale));
            Assert.Equal("DETAINED", e.Settings.Get(SettingKeys.LastFilter));
            Assert.Equal("7,15,30,45", e.Settings.Get(SettingKeys.QuickOffsets));
            Assert.Equal("1", e.Settings.Get(SettingKeys.PrintDirect));
            Assert.Equal("7", e.Settings.Get(SettingKeys.UsbBackupReminderDays));
            Assert.Equal(CourtDesk.Resources.Strings.RosterMinistry, e.Settings.Get(SettingKeys.RosterHeaderLine1));
            Assert.Equal(CourtDesk.Resources.Strings.OperatorUnknown, e.Settings.OperatorOrUnknown());
            e.Settings.Set(SettingKeys.OperatorName, "Officer");
            Assert.Equal("Officer", e.Settings.OperatorOrUnknown());
            e.Settings.Set(SettingKeys.OperatorName, "   ");
            Assert.Equal(CourtDesk.Resources.Strings.OperatorUnknown, e.Settings.OperatorOrUnknown());
            Assert.Equal(new int[] { 7, 15, 30, 45 }, e.Settings.QuickOffsets());
            e.Settings.Set(SettingKeys.QuickOffsets, "10, 20 ,bogus, 0");
            Assert.Equal(new int[] { 10, 20 }, e.Settings.QuickOffsets());
        }

        [Fact]
        public void CreateEditVoidRestoreLifecycle()
        {
            Env e = NewEnv();
            var svc = new DefendantService(e.Db, e.Defendants, e.Events, e.Clock, e.Settings, new NullLogger());
            int courtId = e.Db.Read(s => e.Lookups.Courts((DbContext)s, true))[0].Id;
            DefendantInput in1 = Input("Ahmed Hassan", "1234");
            in1.CourtId = courtId;
            CreateResult created = svc.Create(in1, Uuid(), false);
            Assert.Empty(created.Duplicates);
            Defendant row = svc.Get(created.Id);
            Assert.Equal("Ahmed Hassan", row.FullName);
            Assert.Equal(1, row.RowVersion);
            Assert.Equal(DefendantStatuses.Detained, row.Status);

            CreateResult dup = svc.Create(Input("Ahmed Hassan", "1234"), Uuid(), true);
            Assert.Single(dup.Duplicates);
            Assert.NotEqual(created.Id, dup.Id);

            DefendantInput in2 = Input("Ahmed Hassan Ali", "4321");
            in2.CourtId = courtId;
            in2.SessionDate = new LocalDate(2026, 10, 20);
            Assert.True(svc.Edit(created.Id, 1, in2, Uuid()));
            row = svc.Get(created.Id);
            Assert.Equal("Ahmed Hassan Ali", row.FullName);
            Assert.Equal(2, row.RowVersion);
            Assert.Throws<StaleRecordException>(() => svc.Edit(created.Id, 1, in2, Uuid()));

            Assert.True(svc.Void(created.Id, 2, Uuid()));
            Assert.True(svc.Get(created.Id).IsDeleted);
            Assert.Single(svc.Trash());
            Assert.True(svc.Restore(created.Id, 3, Uuid()));
            Assert.False(svc.Get(created.Id).IsDeleted);
            Assert.Empty(svc.Trash());
            Assert.Empty(((IChangeSeqSource)e.Db).CheckInvariants());
        }

        [Fact]
        public void EditWithNoChangeWritesNoEvent()
        {
            Env e = NewEnv();
            var svc = new DefendantService(e.Db, e.Defendants, e.Events, e.Clock, e.Settings, new NullLogger());
            int id = svc.Create(Input("Ahmed Hassan", "1234"), Uuid(), true).Id;
            long eventsBefore = e.Db.Read(s => e.Events.CountAll(s));
            Assert.True(svc.Edit(id, 1, Input("Ahmed Hassan", "1234"), Uuid()));
            Assert.Equal(eventsBefore, e.Db.Read(s => e.Events.CountAll(s)));
        }

        [Fact]
        public void CreateValidationBlocksBadFields()
        {
            Env e = NewEnv();
            var svc = new DefendantService(e.Db, e.Defendants, e.Events, e.Clock, e.Settings, new NullLogger());
            FieldValidationException ex = Assert.Throws<FieldValidationException>(
                () => svc.Create(Input("A", "12A", "19x"), Uuid(), true));
            Assert.Contains(FieldErrors.Name, ex.Errors);
            Assert.Contains(FieldErrors.CaseNumber, ex.Errors);
            Assert.Contains(FieldErrors.CaseYear, ex.Errors);

            DefendantInput nat = Input("Ahmed Hassan", "1234");
            nat.NationalId = "123";
            Assert.Contains(FieldErrors.NationalId,
                Assert.Throws<FieldValidationException>(() => svc.Create(nat, Uuid(), true)).Errors);
            nat.NationalId = "29801011234567";
            Assert.Empty(svc.Create(nat, Uuid(), true).Duplicates);
        }

        [Fact]
        public void DecideCommitsAndIsIdempotent()
        {
            Env e = NewEnv();
            var svc = new DefendantService(e.Db, e.Defendants, e.Events, e.Clock, e.Settings, new NullLogger());
            int courtId = e.Db.Read(s => e.Lookups.Courts((DbContext)s, true))[0].Id;
            DefendantInput in1 = Input("Ahmed Hassan", "1234");
            in1.CourtId = courtId;
            int id = svc.Create(in1, Uuid(), true).Id;
            var decisions = new DecisionService(e.Db, e.Defendants, e.Events, e.Clock, e.Settings);
            var req = new DecisionRequest
            {
                RequestUuid = Uuid(),
                DefendantId = id,
                ExpectedVersion = 1,
                Code = DecisionCodes.RenewDetention,
                NextDate = new LocalDate(2026, 10, 20)
            };
            DecideResult first = decisions.Decide(req);
            foreach (SessionEvent dbg in e.Db.Read(s => e.Events.ListByDefendant(s, id)))
            {
                Console.WriteLine("DIAG ev=" + dbg.Id + " type=" + dbg.EventType + " uuid=" + dbg.RequestUuid + " code=" + dbg.DecisionCode);
            }
            Assert.False(first.AlreadyApplied);
            Assert.Equal(courtId, (int?)e.Db.Read(s => e.Defendants.GetById(s, id).CourtId));
            Assert.Equal(new LocalDate(2026, 10, 20), svc.Get(id).SessionDate);
            Assert.Equal(2, svc.Get(id).RowVersion);
            long seq = e.Db.CurrentChangeSeq();

            DecideResult second = decisions.Decide(req);
            Assert.True(second.AlreadyApplied);
            Assert.Equal(first.EventId, second.EventId);
            Assert.Equal(seq, e.Db.CurrentChangeSeq());
            Assert.Empty(((IChangeSeqSource)e.Db).CheckInvariants());
        }

        [Fact]
        public void DecideFaultInjectionLeavesNoVisibleChange()
        {
            string[] points = new string[]
            {
                "open-tx", "read-row", "after-validate", "after-apply",
                "before-insert-event", "after-insert-event", "after-update-row", "after-verify"
            };
            foreach (string point in points)
            {
                Env e = NewEnv();
                var svc = new DefendantService(e.Db, e.Defendants, e.Events, e.Clock, e.Settings, new NullLogger());
                int id = svc.Create(Input("Ahmed Hassan", "1234"), Uuid(), true).Id;
                string before = e.Db.Read(s => SnapshotJson.Write(DefendantSnapshot.FromDefendant(e.Defendants.GetById(s, id))));
                long eventsBefore = e.Db.Read(s => e.Events.CountAll(s));
                long seqBefore = e.Db.CurrentChangeSeq();
                var decisions = new DecisionService(e.Db, e.Defendants, e.Events, e.Clock, e.Settings, new ThrowingFaults(point));
                var req = new DecisionRequest
                {
                    RequestUuid = Uuid(),
                    DefendantId = id,
                    ExpectedVersion = 1,
                    Code = DecisionCodes.Postpone,
                    NextDate = new LocalDate(2026, 10, 20)
                };
                Assert.ThrowsAny<Exception>(() => decisions.Decide(req));
                Assert.Equal(before, e.Db.Read(s => SnapshotJson.Write(DefendantSnapshot.FromDefendant(e.Defendants.GetById(s, id)))));
                Assert.Equal(eventsBefore, e.Db.Read(s => e.Events.CountAll(s)));
                Assert.Equal(seqBefore, e.Db.CurrentChangeSeq());
                Assert.Empty(((IChangeSeqSource)e.Db).CheckInvariants());
                foreach (DbGateway g in _gateways)
                {
                    g.Close();
                }
                _gateways.Clear();
            }
        }

        [Fact]
        public void DecideMovesStatusesAndReopen()
        {
            Env e = NewEnv();
            var svc = new DefendantService(e.Db, e.Defendants, e.Events, e.Clock, e.Settings, new NullLogger());
            var decisions = new DecisionService(e.Db, e.Defendants, e.Events, e.Clock, e.Settings);
            int id = svc.Create(Input("Ahmed Hassan", "1234"), Uuid(), true).Id;
            decisions.Decide(new DecisionRequest
            {
                RequestUuid = Uuid(), DefendantId = id, ExpectedVersion = 1,
                Code = DecisionCodes.ReleaseBail, BailAmount = 5000
            });
            Defendant row = svc.Get(id);
            Assert.Equal(DefendantStatuses.Released, row.Status);
            Assert.False(row.SessionDate.HasValue);
            Assert.Throws<DecisionValidationException>(() => decisions.Decide(new DecisionRequest
            {
                RequestUuid = Uuid(), DefendantId = id, ExpectedVersion = 2,
                Code = DecisionCodes.Release
            }));
            decisions.Decide(new DecisionRequest
            {
                RequestUuid = Uuid(), DefendantId = id, ExpectedVersion = 2,
                Code = DecisionCodes.Reopen, NextDate = new LocalDate(2026, 10, 20)
            });
            row = svc.Get(id);
            Assert.Equal(DefendantStatuses.Detained, row.Status);
            Assert.Equal(new LocalDate(2026, 10, 20), row.SessionDate);
            Assert.Empty(((IChangeSeqSource)e.Db).CheckInvariants());
        }

        [Fact]
        public void UndoRulesAreExact()
        {
            Env e = NewEnv();
            var svc = new DefendantService(e.Db, e.Defendants, e.Events, e.Clock, e.Settings, new NullLogger());
            var decisions = new DecisionService(e.Db, e.Defendants, e.Events, e.Clock, e.Settings);
            var undo = new UndoService(e.Db, e.Defendants, e.Events, e.Clock, e.Settings);
            int id = svc.Create(Input("Ahmed Hassan", "1234"), Uuid(), true).Id;
            long createdEvent = e.Db.Read(s => e.Events.LatestId(s, id));
            Assert.False(undo.CanUndo(createdEvent));

            DecideResult d = decisions.Decide(new DecisionRequest
            {
                RequestUuid = Uuid(), DefendantId = id, ExpectedVersion = 1,
                Code = DecisionCodes.Postpone, NextDate = new LocalDate(2026, 10, 20)
            });
            Assert.True(undo.CanUndo(d.EventId));
            undo.Undo(d.EventId, Uuid());
            Assert.Equal(new LocalDate(2026, 10, 6), svc.Get(id).SessionDate);
            Assert.False(undo.CanUndo(d.EventId));
            Assert.Throws<UndoNotAllowedException>(() => undo.Undo(d.EventId, Uuid()));

            DecideResult d2 = decisions.Decide(new DecisionRequest
            {
                RequestUuid = Uuid(), DefendantId = id, ExpectedVersion = 3, Code = DecisionCodes.Judgment
            });
            Assert.True(undo.CanUndo(d2.EventId));
            undo.Undo(d2.EventId, Uuid());
            Assert.Equal(DefendantStatuses.Detained, svc.Get(id).Status);
            Assert.Empty(((IChangeSeqSource)e.Db).CheckInvariants());
        }

        [Fact]
        public void TodaySnapshotMatchesQuerySemantics()
        {
            Env e = NewEnv("2026-10-07");
            var svc = new DefendantService(e.Db, e.Defendants, e.Events, e.Clock, e.Settings, new NullLogger());
            var today = new TodayService(e.Db, e.Defendants, e.Clock, e.Settings);
            var decisions = new DecisionService(e.Db, e.Defendants, e.Events, e.Clock, e.Settings);

            DefendantInput todays = Input("T One", "1001");
            todays.SessionDate = new LocalDate(2026, 10, 7);
            int a = svc.Create(todays, Uuid(), true).Id;
            DefendantInput overdue = Input("O One", "1002");
            overdue.SessionDate = new LocalDate(2026, 10, 1);
            int b = svc.Create(overdue, Uuid(), true).Id;
            DefendantInput tom = Input("M One", "1003");
            tom.SessionDate = new LocalDate(2026, 10, 8);
            int c = svc.Create(tom, Uuid(), true).Id;
            DefendantInput none = Input("N One", "1004");
            none.SessionDate = null;
            svc.Create(none, Uuid(), true);

            TodaySnapshot snap = today.Snapshot();
            Assert.Equal(new LocalDate(2026, 10, 7), snap.Date);
            Assert.Single(snap.Pending);
            Assert.Equal(a, snap.Pending[0].D.Id);
            Assert.Single(snap.Overdue);
            Assert.Equal(b, snap.Overdue[0].D.Id);
            Assert.Equal(1, snap.TomorrowCount);
            Assert.Equal(1, snap.NoDateCount);
            Assert.Equal(1, snap.Total);

            decisions.Decide(new DecisionRequest
            {
                RequestUuid = Uuid(), DefendantId = b, ExpectedVersion = 1,
                Code = DecisionCodes.RenewDetention, NextDate = new LocalDate(2026, 10, 20)
            });
            snap = today.Snapshot();
            Assert.Empty(snap.Overdue);
            Assert.Empty(snap.Decided);
            Assert.Equal(1, snap.Total);
            Assert.Equal(a, snap.Pending[0].D.Id);

            decisions.Decide(new DecisionRequest
            {
                RequestUuid = Uuid(), DefendantId = a, ExpectedVersion = 1, Code = DecisionCodes.Release
            });
            snap = today.Snapshot();
            Assert.Empty(snap.Pending);
            Assert.Single(snap.Decided);
            Assert.Equal(DecisionCodes.Release, snap.Decided[0].DecisionCode);
            Assert.Equal(1, snap.Total);

            e.Clock.Today = new LocalDate(2026, 10, 8);
            snap = today.Snapshot();
            Assert.Equal(new LocalDate(2026, 10, 8), snap.Date);
            Assert.Single(snap.Pending);
            Assert.Equal(c, snap.Pending[0].D.Id);
            Assert.Empty(snap.Decided);
            Assert.Equal(1, snap.NoDateCount);
        }

        [Fact]
        public void RosterSetIncludesDecidedOnesAndExcludesUndone()
        {
            Env e = NewEnv("2026-10-07");
            var svc = new DefendantService(e.Db, e.Defendants, e.Events, e.Clock, e.Settings, new NullLogger());
            var decisions = new DecisionService(e.Db, e.Defendants, e.Events, e.Clock, e.Settings);
            var roster = new RosterService(e.Db, e.Defendants, e.Events, e.Clock, e.Settings);
            DefendantInput p1 = Input("P One", "2001");
            p1.SessionDate = new LocalDate(2026, 10, 7);
            int a = svc.Create(p1, Uuid(), true).Id;
            DefendantInput p2 = Input("P Two", "2002");
            p2.SessionDate = new LocalDate(2026, 10, 7);
            int b = svc.Create(p2, Uuid(), true).Id;
            Assert.Equal(2, roster.Build(new LocalDate(2026, 10, 7)).Count);

            decisions.Decide(new DecisionRequest
            {
                RequestUuid = Uuid(), DefendantId = a, ExpectedVersion = 1,
                Code = DecisionCodes.RenewDetention, NextDate = new LocalDate(2026, 10, 20)
            });
            RosterModel m = roster.Build(new LocalDate(2026, 10, 7));
            Assert.Equal(2, m.Count);
            RosterEntry decided = null;
            foreach (RosterEntry row in m.Entries)
            {
                if (row.DefendantId == a)
                {
                    decided = row;
                }
            }
            Assert.NotNull(decided);
            Assert.True(decided.FromDecision);

            decisions.Decide(new DecisionRequest
            {
                RequestUuid = Uuid(), DefendantId = b, ExpectedVersion = 1, Code = DecisionCodes.Judgment
            });
            Assert.Equal(2, roster.Build(new LocalDate(2026, 10, 7)).Count);

            var undo = new UndoService(e.Db, e.Defendants, e.Events, e.Clock, e.Settings);
            long evId = e.Db.Read(s => e.Events.LatestId(s, a));
            undo.Undo(evId, Uuid());
            m = roster.Build(new LocalDate(2026, 10, 7));
            Assert.Equal(2, m.Count);
            RosterEntry backToPending = null;
            foreach (RosterEntry row in m.Entries)
            {
                if (row.DefendantId == a)
                {
                    backToPending = row;
                }
            }
            Assert.NotNull(backToPending);
            Assert.False(backToPending.FromDecision);
        }

        [Fact]
        public void SearchIndexFiltersAndSorts()
        {
            Env e = NewEnv("2026-10-07");
            var svc = new DefendantService(e.Db, e.Defendants, e.Events, e.Clock, e.Settings, new NullLogger());
            var decisions = new DecisionService(e.Db, e.Defendants, e.Events, e.Clock, e.Settings);
            var index = new SearchIndex(e.Db, e.Defendants, e.Clock,
                new string[] { CourtDesk.Resources.Strings.StatusDetained, CourtDesk.Resources.Strings.StatusReleased, CourtDesk.Resources.Strings.StatusJudged });
            DefendantInput od = Input("عبد الله محمد", "3001");
            od.SessionDate = new LocalDate(2026, 10, 1);
            int a = svc.Create(od, Uuid(), true).Id;
            DefendantInput td = Input("Today Person", "3002");
            td.SessionDate = new LocalDate(2026, 10, 7);
            int b = svc.Create(td, Uuid(), true).Id;
            DefendantInput up = Input("عبدالله علي", "3003");
            up.SessionDate = new LocalDate(2026, 10, 15);
            int c = svc.Create(up, Uuid(), true).Id;
            index.Rebuild();
            Assert.Equal(3, index.Count);

            List<SearchRow> rows = index.Filter("", Filters.Detained, null);
            Assert.Equal(3, rows.Count);
            Assert.Equal(a, rows[0].Id);
            Assert.Equal(b, rows[1].Id);
            Assert.Equal(c, rows[2].Id);

            Assert.Equal(2, index.Filter("عبدالله", Filters.All, null).Count);
            Assert.Equal(2, index.Filter("عبد الله", Filters.All, null).Count);
            Assert.Single(index.Filter("محمد", Filters.All, null));
            Assert.Equal(3, index.Filter("300", Filters.All, null).Count);
            Assert.Equal(1, index.Filter("3001", Filters.All, null).Count);
            Assert.Equal(0, index.Filter("9999999", Filters.All, null).Count);
            Assert.Single(index.Filter("Today", Filters.All, null));

            decisions.Decide(new DecisionRequest
            {
                RequestUuid = Uuid(), DefendantId = b, ExpectedVersion = 1, Code = DecisionCodes.Release
            });
            index.Rebuild();
            Assert.Equal(1, index.CountFilter(Filters.Released));
            Assert.Equal(2, index.CountFilter(Filters.Detained));
            Assert.Equal(3, index.CountFilter(Filters.All));        }

        [Fact]
        public void SearchIndexIsFastAtTwentyThousand()
        {
            Env e = NewEnv("2026-10-07");
            var index = new SearchIndex(e.Db, e.Defendants, e.Clock, new string[] { "d", "r", "j" });
            e.Db.Write(s =>
            {
                var store = new SqliteDefendantStore();
                for (int i = 0; i < 20000; i++)
                {
                    var row = new Defendant
                    {
                        FullName = "Name" + i + " Abdallah",
                        NationalId = null,
                        CaseNumber = (200000 + i).ToString(CultureInfo.InvariantCulture),
                        CaseYear = 2026,
                        CaseType = "T" + (i % 7),
                        Charges = "Charge" + (i % 31),
                        CourtId = null,
                        CircuitId = null,
                        CustodyLocationId = null,
                        SessionDate = new LocalDate(2026, 10, 7).AddDays(i % 20 - 5),
                        Status = DefendantStatuses.Detained,
                        DetainedSince = new LocalDate(2026, 9, 1),
                        Notes = "note" + (i % 13),
                        IsDeleted = false,
                        RowVersion = 0
                    };
                    store.Insert((DbContext)s, row);
                }
                return 0;
            });
            index.Rebuild();
            Assert.Equal(20000, index.Count);
            var sw = new Stopwatch();
            sw.Start();
            List<SearchRow> hits = index.Filter("abdallah", Filters.All, null);
            sw.Stop();
            Assert.Equal(20000, hits.Count);
            Assert.True(sw.ElapsedMilliseconds < 50, "filter took " + sw.ElapsedMilliseconds + "ms");

            sw.Reset();
            sw.Start();
            index.Filter("name1999", Filters.Detained, null);
            sw.Stop();
            Assert.True(sw.ElapsedMilliseconds < 50, "narrow filter took " + sw.ElapsedMilliseconds + "ms");
        }

        [Fact]
        public void BackupTiersAndUsbExport()
        {
            Env e = NewEnv();
            var svc = new DefendantService(e.Db, e.Defendants, e.Events, e.Clock, e.Settings, new NullLogger());
            var drives = new FakeDriveProvider();
            var engine = new SqliteBackupEngine(e.Db, e.Clock, e.Home);
            var backup = new BackupService(e.Db, engine, drives, e.Meta, e.Clock, e.Settings,
                BackupService.ForHome(e.Home), new NullLogger());
            for (int i = 0; i < 12; i++)
            {
                svc.Create(Input("Person " + i, "4" + i), Uuid(), true);
            }
            for (int i = 0; i < 10; i++)
            {
                backup.TakeAutoSnapshot();
            }
            Assert.Equal(10, new DirectoryInfo(BackupService.ForHome(e.Home).AutoDir).GetFiles("auto-*.db").Length);
            Assert.NotNull(backup.TakeExitSnapshot());
            Assert.Null(backup.TakeExitSnapshot());
            Assert.True(backup.UsbReminderDue());

            Assert.Empty(backup.RemovableDrives());
            string usbRoot = Path.Combine(e.Home, "usb");
            Directory.CreateDirectory(usbRoot);
            DriveItem usb = drives.Add(usbRoot, DriveKind.Removable, 512L * 1024 * 1024);
            Assert.Single(backup.RemovableDrives());
            UsbExportResult ok = backup.ExportUsb(usb);
            Assert.True(ok.Ok);
            Assert.True(File.Exists(ok.FinalPath));
            Assert.False(File.Exists(ok.FinalPath + ".partial"));
            Assert.False(backup.UsbReminderDue());
            Assert.Equal(0, backup.UsbAgeDays());

            string tinyRoot = Path.Combine(e.Home, "tiny");
            Directory.CreateDirectory(tinyRoot);
            DriveItem tiny = drives.Add(tinyRoot, DriveKind.Removable, 1024);
            UsbExportResult full = backup.ExportUsb(tiny);
            Assert.False(full.Ok);
            Assert.Equal(UsbFailure.Full, full.Failure);
            Assert.Equal(UsbFailure.Removed, backup.ExportUsb(null).Failure);
            Assert.NotEmpty(backup.ListLocalBackups());
        }

        [Fact]
        public void ClockSanityDetectsRegressionAndOldDate()
        {
            Env e = NewEnv();
            var clock = new ClockSanityService(e.Db, e.Clock, e.Meta, new LocalDate(2026, 10, 1));
            e.Clock.ResetTo(new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc));
            e.Db.Write(s => 0);
            e.Clock.ResetTo(new DateTime(2026, 10, 7, 8, 0, 0, DateTimeKind.Utc));
            Assert.True(clock.Check().Suspect);
            Assert.Equal(ClockReasons.Regression, clock.Check().Reason);

            e.Clock.ResetTo(new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc));
            e.Clock.Today = new LocalDate(2026, 1, 1);
            e.Db.Write(s => 0);
            e.Clock.ResetTo(new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc));
            ClockVerdict before = clock.Check();
            Assert.True(before.Suspect);
            Assert.Equal(ClockReasons.BeforeBuild, before.Reason);

            e.Clock.Today = new LocalDate(2026, 10, 5);
            e.Clock.ResetTo(new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc));
            e.Db.Write(s => 0);
            e.Clock.ResetTo(new DateTime(2026, 10, 5, 9, 30, 0, DateTimeKind.Utc));
            Assert.False(clock.Check().Suspect);
        }

        [Fact]
        public void CsvExportHasBomArabicHeadersAndWesternDates()
        {
            Env e = NewEnv();
            var svc = new DefendantService(e.Db, e.Defendants, e.Events, e.Clock, e.Settings, new NullLogger());
            var csv = new CsvExportService(e.Db, e.Defendants, e.Clock);
            svc.Create(Input("Name, With Comma", "5555"), Uuid(), true);
            string path = Path.Combine(e.Home, "out", "x.csv");
            csv.Export(path);
            byte[] raw = File.ReadAllBytes(path);
            Assert.Equal(0xEF, raw[0]);
            Assert.Equal(0xBB, raw[1]);
            Assert.Equal(0xBF, raw[2]);
            string text = new System.Text.UTF8Encoding(true).GetString(raw, 3, raw.Length - 3);
            Assert.StartsWith(CourtDesk.Resources.Strings.CsvHeaders, text);
            Assert.Contains("\"Name, With Comma\"", text);
            Assert.Contains("06/10/2026", text);
            Assert.Contains("01/09/2026", text);
            Assert.Contains("\r\n", text);
            Assert.False(File.Exists(path + ".partial"));
        }

        [Fact]
        public void LookupServiceAddsAndNormalizes()
        {
            Env e = NewEnv();
            var lookups = new LookupService(e.Db, e.Lookups);
            int courtA = lookups.AddCourt("محكمة مركز");
            int courtB = lookups.AddCourt("محكمه مركز");
            Assert.Equal(courtA, courtB);
            int circuit = lookups.AddCircuit(courtA, "دائرة 1");
            Assert.Single(lookups.Circuits(courtA, true));
            Assert.Empty(lookups.Circuits(courtB + 1000, true));
            lookups.RenameCourt(courtA, "محكمة مركز معدلة");
            Assert.Contains(lookups.Courts(true), c => c.Id == courtA && c.Name == "محكمة مركز معدلة");
            lookups.SetCourtActive(courtA, false);
            Assert.Empty(lookups.Courts(true).FindAll(c => c.Id == courtA));
            Assert.NotEmpty(lookups.Courts(false).FindAll(c => c.Id == courtA));
            Assert.NotEmpty(lookups.Reasons(true));
            Assert.NotEmpty(lookups.CustodyLocations(true));
            lookups.RecordChargeUse("سرقة");
            lookups.RecordChargeUse("سرقة");
            lookups.RecordChargeUse("اغتيال");
            Assert.Contains("سرقة", lookups.SuggestCharges("سر", 5));
            Assert.NotEqual(0, circuit);
        }

        [Fact]
        public void ScheduleServiceCountsDays()
        {
            Env e = NewEnv("2026-10-07");
            var svc = new DefendantService(e.Db, e.Defendants, e.Events, e.Clock, e.Settings, new NullLogger());
            var schedule = new ScheduleService(e.Db, e.Defendants, e.Clock);
            DefendantInput in1 = Input("S One", "6001");
            in1.SessionDate = new LocalDate(2026, 10, 9);
            svc.Create(in1, Uuid(), true);
            Assert.Equal(1, schedule.CountForDay(new LocalDate(2026, 10, 9)));
            Assert.Equal(0, schedule.CountForDay(new LocalDate(2026, 10, 8)));
            Assert.Single(schedule.Day(new LocalDate(2026, 10, 9)));
            List<DateCount> days = schedule.NextDays(new LocalDate(2026, 10, 7), 5);
            Assert.Equal(5, days.Count);
            Assert.Equal(new LocalDate(2026, 10, 7), days[0].Date);
            Assert.Equal(1, days[2].Count);
        }
    }
}

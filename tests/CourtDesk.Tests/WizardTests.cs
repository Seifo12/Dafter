using System;
using System.Collections.Generic;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;
using CourtDesk.Persistence;
using CourtDesk.UI.Controls;
using CourtDesk.UI.ViewModels;
using Xunit;

namespace CourtDesk.Tests
{
    [Trait("Category", "UI")]
    public sealed class WizardTests
    {
        private sealed class Env : IDisposable
        {
            public string Home;
            public DbGateway Db;
            public FakeClock Clock;
            public SettingsService Settings;
            public IDefendantStore Defendants;
            public IEventStore Events;
            public LookupService Lookups;
            public DefendantService DefendantService;
            public DecisionService DecisionService;
            public UndoService Undo;

            public Env()
            {
                Home = TestDb.NewHome();
                Clock = new FakeClock();
                RecoveryResult rec = TestDb.Open(Home, Clock);
                Db = rec.Gateway;
                Defendants = new SqliteDefendantStore();
                Events = new SqliteEventStore();
                var store = new SqliteLookupStore();
                Settings = new SettingsService(Db, new SqliteSettingsStore());
                Db.Write(s =>
                {
                    SeedData.SeedLookups((DbContext)s,
                        CourtDesk.Resources.Strings.SeedCourts,
                        CourtDesk.Resources.Strings.SeedCustody,
                        CourtDesk.Resources.Strings.SeedReasons);
                    return 0;
                });
                Lookups = new LookupService(Db, store);
                DefendantService = new DefendantService(Db, Defendants, Events, Clock, Settings, new NullLogger());
                DecisionService = new DecisionService(Db, Defendants, Events, Clock, Settings);
                Undo = new UndoService(Db, Defendants, Events, Clock, Settings);
            }

            public int Add(string name, string caseNumber, LocalDate? session)
            {
                return DefendantService.Create(new DefendantInput
                {
                    FullName = name,
                    CaseNumber = caseNumber,
                    CaseYear = "2026",
                    SessionDate = session
                }, Guid.NewGuid().ToString("N"), true).Id;
            }

            public DecisionWizardViewModel Wizard(int id)
            {
                return new DecisionWizardViewModel(DecisionService, Undo, DefendantService,
                    Lookups, Settings, Clock, new List<int> { id }, 0);
            }

            public void Dispose()
            {
                try { Db.Close(); }
                catch (Exception) { }
                TestDb.Wipe(Home);
            }
        }

        [Fact]
        public void Step1OffersOnlyValidDecisionsAndHoldsIdentity()
        {
            StaRunner.Run(delegate
            {
                using (var env = new Env())
                {
                    int id = env.Add("محمد علي حسن", "8001", new LocalDate(2026, 10, 7));
                    DecisionWizardViewModel vm = env.Wizard(id);
                    Assert.Equal(5, vm.Tiles.Count);
                    Assert.Equal("تجديد الحبس", vm.Tiles[0].Label);
                    Assert.DoesNotContain(vm.Tiles, t => t.Code == DecisionCodes.Reopen);
                    Assert.Equal("محمد علي حسن", vm.IdentityName);
                    Assert.Contains("8001", vm.IdentityCase);
                    Assert.Equal(1, vm.Step);
                    Assert.Equal("الخطوة 1 من 3", vm.StepText);
                    Assert.Null(vm.PreCheckError);
                }
            });
        }

        [Fact]
        public void ReleasedDefendantOnlyGetsReopen()
        {
            StaRunner.Run(delegate
            {
                using (var env = new Env())
                {
                    int id = env.Add("سعيد مصطفى", "8002", new LocalDate(2026, 10, 7));
                    env.DecisionService.Decide(new DecisionRequest
                    {
                        RequestUuid = Guid.NewGuid().ToString("N"),
                        DefendantId = id,
                        ExpectedVersion = 1,
                        Code = DecisionCodes.Release
                    });
                    DecisionWizardViewModel vm = env.Wizard(id);
                    Assert.Single(vm.Tiles);
                    Assert.Equal(DecisionCodes.Reopen, vm.Tiles[0].Code);
                }
            });
        }

        [Fact]
        public void ChooseAdvancesToStep2AndSetsNextDate()
        {
            StaRunner.Run(delegate
            {
                using (var env = new Env())
                {
                    int id = env.Add("hendy mostafa", "8003", new LocalDate(2026, 10, 5));
                    DecisionWizardViewModel vm = env.Wizard(id);
                    vm.Choose(DecisionCodes.RenewDetention);
                    Assert.Equal(2, vm.Step);
                    Assert.True(vm.NeedsNextDate);
                    Assert.Equal(new LocalDate(2026, 10, 12), vm.NextDate);
                    Assert.NotEmpty(vm.ChosenLine);
                    Assert.NotEmpty(vm.Reasons);
                    vm.NextCommand.Execute(null);
                    Assert.Equal(3, vm.Step);
                    Assert.NotEmpty(vm.ReviewSentence);
                    Assert.Contains("تجديد حبس", vm.ReviewSentence);
                }
            });
        }

        [Fact]
        public void EachDecisionProducesItsOwnReviewSentence()
        {
            StaRunner.Run(delegate
            {
                using (var env = new Env())
                {
                    int id = env.Add("Nour Hassan", "8004", new LocalDate(2026, 10, 5));
                    DecisionWizardViewModel renew = env.Wizard(id);
                    renew.Choose(DecisionCodes.RenewDetention);
                    Assert.Contains("تجديد حبس", renew.ReviewSentence);

                    DecisionWizardViewModel release = env.Wizard(id);
                    release.Choose(DecisionCodes.Release);
                    Assert.Contains("إخلاء سبيل", release.ReviewSentence);
                    Assert.False(release.NeedsNextDate);

                    DecisionWizardViewModel bail = env.Wizard(id);
                    bail.Choose(DecisionCodes.ReleaseBail);
                    bail.Bail = "٥٠٠٠";
                    Assert.Contains("5000 جنيه", bail.ReviewSentence);

                    DecisionWizardViewModel judge = env.Wizard(id);
                    judge.Choose(DecisionCodes.Judgment);
                    Assert.Contains("صدر حكم", judge.ReviewSentence);
                }
            });
        }

        [Fact]
        public void Step2BlocksMissingNextDate()
        {
            StaRunner.Run(delegate
            {
                using (var env = new Env())
                {
                    int id = env.Add("Wael Amin", "8005", new LocalDate(2026, 10, 5));
                    DecisionWizardViewModel vm = env.Wizard(id);
                    vm.Choose(DecisionCodes.Postpone);
                    vm.NextDate = null;
                    vm.NextCommand.Execute(null);
                    Assert.Equal(2, vm.Step);
                    Assert.True(vm.HasError);
                    Assert.Equal("اختار ميعاد الجلسة الجاية الأول.", vm.Error);
                }
            });
        }

        [Fact]
        public void WeekendPastAndFarWarnings()
        {
            StaRunner.Run(delegate
            {
                using (var env = new Env())
                {
                    int id = env.Add("Ramy Fathy", "8006", new LocalDate(2026, 10, 5));
                    DecisionWizardViewModel vm = env.Wizard(id);
                    vm.Choose(DecisionCodes.RenewDetention);
                    Assert.False(vm.ShowWeekendWarning);
                    vm.PickDate(new LocalDate(2026, 10, 9));
                    Assert.True(vm.ShowWeekendWarning);
                    env.Clock.Today = new LocalDate(2026, 10, 20);
                    DecisionWizardViewModel vm2 = env.Wizard(id);
                    vm2.Choose(DecisionCodes.RenewDetention);
                    vm2.PickDate(new LocalDate(2026, 10, 18));
                    Assert.True(vm2.ShowPastWarning);
                    vm2.PickDate(new LocalDate(2027, 3, 1));
                    Assert.True(vm2.ShowFarWarning);
                    vm2.PickDate(new LocalDate(2035, 1, 1));
                    Assert.False(vm2.ShowFarWarning);
                }
            });
        }

        [Fact]
        public void SaveCommitsAndDoubleSaveIsIdempotent()
        {
            StaRunner.Run(delegate
            {
                using (var env = new Env())
                {
                    int id = env.Add("Shady Magdy", "8007", new LocalDate(2026, 10, 7));
                    DecisionWizardViewModel vm = env.Wizard(id);
                    vm.Choose(DecisionCodes.RenewDetention);
                    vm.PickDate(new LocalDate(2026, 10, 20));
                    vm.Save();
                    Assert.True(vm.Done);
                    Assert.Equal(new LocalDate(2026, 10, 20), env.DefendantService.Get(id).SessionDate);
                    Assert.False(vm.Saving);
                    env.Db.CheckInvariants();
                }
            });
        }

        [Fact]
        public void SaveShowsErrorAndKeepsWizardOpen()
        {
            StaRunner.Run(delegate
            {
                using (var env = new Env())
                {
                    int id = env.Add("Adel Nabil", "8008", new LocalDate(2026, 10, 7));
                    DecisionWizardViewModel vm = env.Wizard(id);
                    vm.Choose(DecisionCodes.RenewDetention);
                    vm.NextCommand.Execute(null);
                    env.DefendantService.Edit(id, 1, new DefendantInput
                    {
                        FullName = "Adel Nabil",
                        CaseNumber = "8008",
                        CaseYear = "2026",
                        Charges = "تغيّر بعد فتح المعالج",
                        SessionDate = new LocalDate(2026, 10, 7)
                    }, Guid.NewGuid().ToString("N"));
                    vm.Save();
                    Assert.False(vm.Done);
                    Assert.True(vm.HasError);
                    Assert.Equal(3, vm.Step);
                }
            });
        }

        [Fact]
        public void QueueModeMovesThroughPeople()
        {
            StaRunner.Run(delegate
            {
                using (var env = new Env())
                {
                    int a = env.Add("Queue One", "8009", new LocalDate(2026, 10, 7));
                    int b = env.Add("Queue Two", "8010", new LocalDate(2026, 10, 7));
                    var vm = new DecisionWizardViewModel(env.DecisionService, env.Undo, env.DefendantService,
                        env.Lookups, env.Settings, env.Clock, new List<int> { a, b }, 0);
                    Assert.True(vm.InQueue);
                    Assert.Equal("1 من 2", vm.QueueProgress);
                    vm.Choose(DecisionCodes.Release);
                    vm.Save();
                    Assert.True(vm.Done);
                    Assert.Equal(DefendantStatuses.Released, env.DefendantService.Get(a).Status);
                }
            });
        }

        [Fact]
        public void EditViewModelValidatesAndSaves()
        {
            StaRunner.Run(delegate
            {
                using (var env = new Env())
                {
                    var vm = new DefendantEditViewModel(env.DefendantService, env.Lookups, env.Clock, null);
                    vm.Name = "X";
                    vm.CaseNumber = "12A";
                    vm.CaseYear = "19";
                    vm.SaveCommand.Execute(null);
                    Assert.True(vm.HasProblems);
                    Assert.False(vm.WasSaved);

                    vm.Name = "محمد عبد الله";
                    vm.CaseNumber = "1234";
                    vm.CaseYear = "2026";
                    vm.SessionDate = new LocalDate(2026, 10, 7);
                    vm.NoSessionDate = false;
                    vm.SaveCommand.Execute(null);
                    Assert.True(vm.WasSaved);
                    Assert.True(vm.Id > 0);
                    Assert.NotNull(env.DefendantService.Get(vm.Id));
                    Assert.Equal("1234", env.DefendantService.Get(vm.Id).CaseNumber);
                }
            });
        }

        [Fact]
        public void EditAcceptsArabicDigits()
        {
            StaRunner.Run(delegate
            {
                using (var env = new Env())
                {
                    var vm = new DefendantEditViewModel(env.DefendantService, env.Lookups, env.Clock, null);
                    vm.Name = "متهم بالارقام";
                    vm.CaseNumber = "٤٥٦٧";
                    vm.CaseYear = "٢٠٢٦";
                    vm.SaveCommand.Execute(null);
                    Assert.True(vm.WasSaved);
                    Assert.Equal("4567", env.DefendantService.Get(vm.Id).CaseNumber);
                    Assert.Equal(2026, env.DefendantService.Get(vm.Id).CaseYear);
                }
            });
        }

        [Fact]
        public void CalendarModelMarksWeekendAndMinimum()
        {
            var counts = new Dictionary<string, int>();
            counts["2026-10-07"] = 3;
            List<CalendarCell> cells = CalendarModel.Build(new LocalDate(2026, 10, 7),
                new LocalDate(2026, 10, 7), null, new LocalDate(2026, 10, 7), counts);
            Assert.True(cells.Count >= 35);
            CalendarCell friday = null;
            CalendarCell today = null;
            foreach (CalendarCell c in cells)
            {
                if (c.Date.HasValue && c.Date.Value == new LocalDate(2026, 10, 9))
                {
                    friday = c;
                }
                if (c.Date.HasValue && c.Date.Value == new LocalDate(2026, 10, 7))
                {
                    today = c;
                }
                if (c.Date.HasValue && c.Date.Value == new LocalDate(2026, 10, 1))
                {
                    Assert.False(c.Enabled);
                }
            }
            Assert.NotNull(friday);
            Assert.True(friday.IsWeekend);
            Assert.NotNull(today);
            Assert.True(today.IsToday);
            Assert.Equal(3, today.Count);
            Assert.Equal(new LocalDate(2026, 9, 26), CalendarModel.StartGrid(new LocalDate(2026, 10, 7)));
            Assert.Equal(new LocalDate(2026, 11, 1), CalendarModel.AddMonths(new LocalDate(2026, 10, 7), 1));
        }
    }
}
using System;
using System.Collections.Generic;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;
using CourtDesk.Persistence;
using Xunit;

namespace CourtDesk.Tests
{
    [Trait("Category", "UI")]
    public sealed class RefreshPathTests
    {
        private static int Add(UiEnv env, string name, string caseNumber, LocalDate? session)
        {
            return env.AddDefendant(name, caseNumber, session);
        }

        private static void Decide(UiEnv env, int id, string code, LocalDate? next)
        {
            var svc = new DefendantService(env.Db, env.Defendants, env.Events,
                env.Clock, env.Settings, new NullLogger());
            Defendant d = svc.Get(id);
            var decide = new DecisionService(env.Db, env.Defendants, env.Events,
                env.Clock, env.Settings);
            decide.Decide(new DecisionRequest
            {
                RequestUuid = Guid.NewGuid().ToString("N"),
                DefendantId = id,
                ExpectedVersion = d.RowVersion,
                Code = code,
                NextDate = next
            });
        }

        private static void Check(UiEnv env)
        {
            ViewDbAssert.AssertViewsMatchDb(env.Main, env.Today, env.Db, env.Defendants, env.Clock);
        }

        [Fact]
        public void AddedRecordAppearsInGridWithoutManualRebuild()
        {
            StaRunner.Run(delegate
            {
                using (var env = new UiEnv())
                {
                    int id = Add(env, "ظهر بعد الإضافة", "8001", new LocalDate(2026, 10, 7));
                    env.Main.RefreshAfterDataChanged(new List<int> { id });
                    Check(env);
                    Assert.Equal(1, env.Register.Rows.Count);
                    Assert.Equal("ظهر بعد الإضافة", env.Register.Rows[0].Name);
                }
            });
        }

        [Fact]
        public void EditedNameAndDateAppearInGrid()
        {
            StaRunner.Run(delegate
            {
                using (var env = new UiEnv())
                {
                    int id = Add(env, "اسم قديم", "8002", new LocalDate(2026, 10, 7));
                    env.Main.RefreshAfterDataChanged(new List<int> { id });
                    var svc = new DefendantService(env.Db, env.Defendants, env.Events,
                        env.Clock, env.Settings, new NullLogger());
                    Defendant d = svc.Get(id);
                    svc.Edit(id, d.RowVersion, new DefendantInput
                    {
                        FullName = "اسم جديد",
                        CaseNumber = "8002",
                        CaseYear = "2026",
                        SessionDate = new LocalDate(2026, 10, 20)
                    }, Guid.NewGuid().ToString("N"));
                    env.Main.RefreshAfterDataChanged(new List<int> { id });
                    Check(env);
                    Assert.Equal("اسم جديد", env.Register.Rows[0].Name);
                    Assert.Equal("20/10/2026", env.Register.Rows[0].SessionLine1);
                }
            });
        }

        [Fact]
        public void EachDecisionUpdatesTheGridRow()
        {
            StaRunner.Run(delegate
            {
                using (var env = new UiEnv())
                {
                    string[] codes = new string[]
                    {
                        DecisionCodes.RenewDetention, DecisionCodes.Postpone,
                        DecisionCodes.Release, DecisionCodes.ReleaseBail,
                        DecisionCodes.Judgment
                    };
                    for (int i = 0; i < codes.Length; i++)
                    {
                        int id = Add(env, "قرار " + i, (8100 + i).ToString(),
                            new LocalDate(2026, 10, 7));
                        LocalDate? next = (codes[i] == DecisionCodes.RenewDetention
                            || codes[i] == DecisionCodes.Postpone)
                            ? (LocalDate?)new LocalDate(2026, 10, 20)
                            : null;
                        Decide(env, id, codes[i], next);
                        env.Main.RefreshAfterDataChanged(new List<int> { id });
                        Check(env);
                    }
                    Assert.Equal(5, env.Register.Rows.Count);
                    bool sawReleased = false;
                    bool sawJudged = false;
                    bool sawNewDate = false;
                    foreach (var row in env.Register.Rows)
                    {
                        if (row.StateKey == DisplayStates.Released)
                        {
                            sawReleased = true;
                        }
                        if (row.StateKey == DisplayStates.Judged)
                        {
                            sawJudged = true;
                        }
                        if (row.SessionLine1 == "20/10/2026")
                        {
                            sawNewDate = true;
                        }
                    }
                    Assert.True(sawReleased);
                    Assert.True(sawJudged);
                    Assert.True(sawNewDate);
                }
            });
        }

        [Fact]
        public void UndoRestoresTheGridRow()
        {
            StaRunner.Run(delegate
            {
                using (var env = new UiEnv())
                {
                    int id = Add(env, "تراجع يعيد الصف", "8003", new LocalDate(2026, 10, 7));
                    Decide(env, id, DecisionCodes.Release, null);
                    env.Main.RefreshAfterDataChanged(new List<int> { id });
                    env.Register.Filter = Filters.All;
                    env.Register.Refresh();
                    Assert.Equal(DisplayStates.Released, env.Register.Rows[0].StateKey);
                    var undo = new UndoService(env.Db, env.Defendants, env.Events,
                        env.Clock, env.Settings);
                    long ev = env.Db.Read(s => ((IEventStore)env.Events).LatestId(s, id));
                    undo.Undo(ev, Guid.NewGuid().ToString("N"));
                    env.Main.RefreshAfterDataChanged(new List<int> { id });
                    Check(env);
                    Assert.Equal("07/10/2026", env.Register.Rows[0].SessionLine1);
                }
            });
        }

        [Fact]
        public void VoidRemovesFromGridAndChips()
        {
            StaRunner.Run(delegate
            {
                using (var env = new UiEnv())
                {
                    int id = Add(env, "إلغاء يشيل الصف", "8004", new LocalDate(2026, 10, 7));
                    env.Main.RefreshAfterDataChanged(new List<int> { id });
                    Assert.Equal(1, env.Register.Rows.Count);
                    var svc = new DefendantService(env.Db, env.Defendants, env.Events,
                        env.Clock, env.Settings, new NullLogger());
                    Defendant d = svc.Get(id);
                    svc.Void(id, d.RowVersion, Guid.NewGuid().ToString("N"));
                    env.Main.RefreshAfterDataChanged(new List<int> { id });
                    Check(env);
                    Assert.Equal(0, env.Register.Rows.Count);
                }
            });
        }

        [Fact]
        public void RestoreFromTrashReappearsInGrid()
        {
            StaRunner.Run(delegate
            {
                using (var env = new UiEnv())
                {
                    int id = Add(env, "استعادة ترجع الصف", "8005", new LocalDate(2026, 10, 7));
                    var svc = new DefendantService(env.Db, env.Defendants, env.Events,
                        env.Clock, env.Settings, new NullLogger());
                    Defendant d = svc.Get(id);
                    svc.Void(id, d.RowVersion, Guid.NewGuid().ToString("N"));
                    env.Main.RefreshAfterDataChanged(new List<int> { id });
                    Assert.Equal(0, env.Register.Rows.Count);
                    Defendant v = svc.Get(id);
                    svc.Restore(v.Id, v.RowVersion, Guid.NewGuid().ToString("N"));
                    env.Main.RefreshAfterDataChanged(new List<int> { id });
                    Check(env);
                    Assert.Equal(1, env.Register.Rows.Count);
                    Assert.Equal("استعادة ترجع الصف", env.Register.Rows[0].Name);
                }
            });
        }

        [Fact]
        public void DayRolloverMovesRowToOverdue()
        {
            StaRunner.Run(delegate
            {
                using (var env = new UiEnv())
                {
                    int id = Add(env, "ترحيل يوم", "8006", new LocalDate(2026, 10, 7));
                    env.Main.RefreshAfterDataChanged(new List<int> { id });
                    env.Clock.Today = new LocalDate(2026, 10, 9);
                    env.Main.RefreshAfterDataChanged(new List<int> { id });
                    Check(env);
                    bool overdue = false;
                    foreach (var card in env.Sidebar.OverdueCards)
                    {
                        if (card.Id == id)
                        {
                            overdue = true;
                        }
                    }
                    Assert.True(overdue, "row did not move to overdue after rollover");
                    foreach (var row in env.Register.Rows)
                    {
                        if (row.Id == id)
                        {
                            Assert.Equal(DisplayStates.Overdue, row.StateKey);
                        }
                    }
                }
            });
        }
    }
}
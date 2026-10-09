using System.Collections.Generic;
using System.Windows;
using CourtDesk.Domain;
using Xunit;

namespace CourtDesk.Tests
{
    [Trait("Category", "UI")]
    public sealed class ScheduleViewTests
    {
        [Fact]
        public void ScheduleOffersTodayAndTomorrow()
        {
            StaRunner.Run(delegate
            {
                using (var env = new DialogEnv())
                {
                    env.Clock.Today = new LocalDate(2026, 10, 8);
                    env.Add("اليوم", "6801", new LocalDate(2026, 10, 8));
                    env.Add("بكرة", "6802", new LocalDate(2026, 10, 9));
                    env.Refresh();
                    var svc = new CourtDesk.Application.ScheduleService(env.Db,
                        env.Defendants, env.Clock);
                    var vm = new CourtDesk.UI.ViewModels.ScheduleViewModel(svc, env.Clock);
                    Assert.True(vm.Upcoming.Count >= 2, "upcoming rows: " + vm.Upcoming.Count);
                    Assert.Equal(CourtDesk.Resources.Strings.DateToday,
                        vm.Upcoming[0].Text.Split(':')[0].Trim());
                    Assert.Equal(CourtDesk.Resources.Strings.DateTomorrow,
                        vm.Upcoming[1].Text.Split(':')[0].Trim());
                    Assert.Equal(1, vm.Upcoming[0].Count);
                    Assert.Equal(1, vm.Upcoming[1].Count);
                    vm.TomorrowCommand.Execute(null);
                    Assert.Equal(new LocalDate(2026, 10, 9), vm.Selected);
                    Assert.Equal(1, vm.Day.Count);
                    Assert.True(vm.Upcoming[1].IsSelected, "tomorrow not marked selected");
                    vm.TodayCommand.Execute(null);
                    Assert.Equal(new LocalDate(2026, 10, 8), vm.Selected);
                    Assert.Equal(1, vm.Day.Count);
                }
            });
        }
    }
}
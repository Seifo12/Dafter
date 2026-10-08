using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace CourtDesk.Tests
{
    [Trait("Category", "UI")]
    public sealed class HistoryViewTests
    {
        [Fact]
        public void TimestampsAreReadableAndNotBidiReordered()
        {
            StaRunner.Run(delegate
            {
                using (var env = new DialogEnv())
                {
                    env.Add("سجل", "6701", new CourtDesk.Domain.LocalDate(2026, 10, 7));
                    env.Refresh();
                    var vm = new CourtDesk.UI.ViewModels.HistoryViewModel(env.Db,
                        env.Events, env.Defendants);
                    vm.Refresh();
                    Assert.True(vm.Rows.Count >= 1, "no history rows");
                    foreach (CourtDesk.UI.ViewModels.HistoryRow r in vm.Rows)
                    {
                        Assert.True(r.When.StartsWith("\u200E"), "no LRM in when: " + r.When);
                        Assert.False(r.When.StartsWith("\u200E20:"),
                            "when starts with time (bidi reorder): " + r.When);
                        if (r.NextDate != "")
                        {
                            Assert.True(r.NextDate.StartsWith("\u200E"), "no LRM in date");
                            Assert.False(r.NextDate.Contains("-"),
                                "date not formatted for RTL: " + r.NextDate);
                        }
                    }
                }
            });
        }

        [Fact]
        public void HistoryGridHasColumnPerField()
        {
            StaRunner.Run(delegate
            {
                var view = new CourtDesk.UI.Views.HistoryView();
                view.Measure(new Size(1200, 700));
                view.Arrange(new Rect(0, 0, 1200, 700));
                view.UpdateLayout();
                var grid = (DataGrid)view.FindName("Grid");
                Assert.Equal(6, grid.Columns.Count);
                var headers = new List<string>();
                foreach (DataGridColumn c in grid.Columns)
                {
                    headers.Add(c.Header as string);
                }
                Assert.Contains(CourtDesk.Resources.Strings.HistWhen, headers);
                Assert.DoesNotContain(CourtDesk.Resources.Strings.GridState, headers);
                Assert.True(grid.Columns[1].Width.Value > 0, "name column has no width");
            });
        }
    }
}
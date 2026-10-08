using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CourtDesk.Domain;
using CourtDesk.UI.Controls;
using Xunit;

namespace CourtDesk.Tests
{
    [Trait("Category", "UI")]
    public sealed class CalendarClickTests
    {
        [Fact]
        public void ClickingDaySelectsItAndRaisesEvent()
        {
            StaRunner.Run(delegate
            {
                var cal = new BigCalendar();
                cal.Today = new LocalDate(2026, 10, 8);
                cal.Minimum = new LocalDate(2025, 10, 8);
                cal.ShowMonth(new LocalDate(2026, 10, 1));
                LocalDate picked = default(LocalDate);
                bool raised = false;
                cal.DateSelected += delegate(LocalDate d)
                {
                    picked = d;
                    raised = true;
                };
                var host = new Window
                {
                    Width = 700,
                    Height = 700,
                    Left = -32000,
                    Top = -32000,
                    ShowActivated = false,
                    FlowDirection = FlowDirection.RightToLeft,
                    Content = cal
                };
                try
                {
                    host.Show();
                    Pump();
                    host.UpdateLayout();
                    Pump();
                    host.UpdateLayout();
                    Button target = FindButtonFor(cal, 15);
                    Assert.True(target != null, "no button for day 15");
                    Assert.True(target.IsEnabled, "day 15 button disabled");
                    target.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, target));
                    Pump();
                    host.UpdateLayout();
                    Assert.True(cal.Selected.HasValue, "selection did not change");
                    Assert.Equal(15, cal.Selected.Value.Day);
                    Assert.True(raised, "DateSelected not raised");
                    Assert.Equal(15, picked.Day);
                }
                finally
                {
                    host.Close();
                }
            });
        }

        private static void Pump()
        {
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Loaded,
                new System.Action(delegate { }));
        }

        private static Button FindButtonFor(DependencyObject root, int day)
        {
            var stack = new Stack<DependencyObject>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                DependencyObject cur = stack.Pop();
                Button b = cur as Button;
                if (b != null)
                {
                    CourtDesk.UI.Controls.CalendarCell cell =
                        b.CommandParameter as CourtDesk.UI.Controls.CalendarCell;
                    if (cell != null && cell.Date.HasValue && cell.Date.Value.Day == day
                        && cell.InMonth)
                    {
                        return b;
                    }
                }
                int n = VisualTreeHelper.GetChildrenCount(cur);
                for (int i = 0; i < n; i++)
                {
                    stack.Push(VisualTreeHelper.GetChild(cur, i));
                }
            }
            return null;
        }
    }
}
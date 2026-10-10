using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;
using CourtDesk.UI.Controls;
using CourtDesk.UI.Views;
using Xunit;

namespace CourtDesk.Tests
{
    [Trait("Category", "UI")]
    public sealed class RegisterViewTests
    {
        private static CheckBox FindRowCheckBox(DataGrid grid, int rowIndex)
        {
            var container = grid.ItemContainerGenerator.ContainerFromIndex(rowIndex) as DependencyObject;
            if (container == null)
            {
                return null;
            }
            var stack = new System.Collections.Generic.Stack<DependencyObject>();
            stack.Push(container);
            while (stack.Count > 0)
            {
                DependencyObject cur = stack.Pop();
                var box = cur as CheckBox;
                if (box != null && box.DataContext is CourtDesk.UI.ViewModels.RegisterRow)
                {
                    return box;
                }
                int n = VisualTreeHelper.GetChildrenCount(cur);
                for (int i = 0; i < n; i++)
                {
                    stack.Push(VisualTreeHelper.GetChild(cur, i));
                }
            }
            return null;
        }

        private static CheckBox FindHeaderCheckBox(DataGrid grid)
        {
            foreach (DataGridColumn c in grid.Columns)
            {
                var box = c.Header as CheckBox;
                if (box != null)
                {
                    return box;
                }
            }
            return null;
        }

        [Fact]
        public void WatermarkShowsOnlyOnEmptyRegister()
        {
            StaRunner.Run(delegate
            {
                using (var env = new UiEnv())
                {
                    var view = new RegisterView { DataContext = env.Register };
                    view.Measure(new Size(1200, 800));
                    view.Arrange(new Rect(0, 0, 1200, 800));
                    view.UpdateLayout();
                    var mark = view.FindName("WatermarkText") as TextBlock;
                    Assert.NotNull(mark);
                    Assert.Equal(Visibility.Visible, mark.Visibility);
                    Assert.Equal("Made By Saif Mohamed", mark.Text);
                    env.AddDefendant("توقيع", "9301", new LocalDate(2026, 10, 7));
                    env.Refresh();
                    view.UpdateLayout();
                    Assert.Equal(Visibility.Collapsed, mark.Visibility);
                }
            });
        }

        [Fact]
        public void RowCheckboxClickUpdatesCheckedCountAndLeavesNoPhantomRow()
        {
            StaRunner.Run(delegate
            {
                using (var env = new UiEnv())
                {
                    env.AddDefendant("نقرة واحد", "9101", new LocalDate(2026, 10, 7));
                    env.AddDefendant("نقرة اتنين", "9102", new LocalDate(2026, 10, 7));
                    env.Refresh();
                    var view = new RegisterView { DataContext = env.Register };
                    view.Measure(new Size(1200, 800));
                    view.Arrange(new Rect(0, 0, 1200, 800));
                    view.UpdateLayout();
                    Assert.Equal(env.Register.Rows.Count, view.Grid.Items.Count);
                    CheckBox box = FindRowCheckBox(view.Grid, 0);
                    Assert.NotNull(box);
                    box.IsChecked = true;
                    Assert.Equal(1, env.Register.CheckedCount);
                    Assert.Equal(1, env.Register.CheckedIds.Count);
                    box.IsChecked = false;
                    Assert.Equal(0, env.Register.CheckedCount);
                }
            });
        }

        [Fact]
        public void HeaderCheckboxSelectsAndClearsAllVisible()
        {
            StaRunner.Run(delegate
            {
                using (var env = new UiEnv())
                {
                    env.AddDefendant("كل واحد", "9111", new LocalDate(2026, 10, 7));
                    env.AddDefendant("كل اتنين", "9112", new LocalDate(2026, 10, 7));
                    env.Refresh();
                    var view = new RegisterView { DataContext = env.Register };
                    view.Measure(new Size(1200, 800));
                    view.Arrange(new Rect(0, 0, 1200, 800));
                    view.UpdateLayout();
                    CheckBox head = FindHeaderCheckBox(view.Grid);
                    Assert.NotNull(head);
                    head.IsChecked = true;
                    Assert.Equal(2, env.Register.CheckedCount);
                    head.IsChecked = false;
                    Assert.Equal(0, env.Register.CheckedCount);
                }
            });
        }

        [Fact]
        public void BulkVoidRemovesEveryCheckedRecord()
        {
            using (var env = new DialogEnv())
            {
                int a = env.Add("حذف واحد", "9201", env.Clock.Today);
                int b = env.Add("حذف اتنين", "9202", env.Clock.Today);
                int c = env.Add("يبقى", "9203", env.Clock.Today);
                var like = new CourtDesk.UI.UiCoordinator.CompositionLike();
                like.Defendants = new DefendantService(env.Db, env.Defendants, env.Events,
                    env.Clock, env.Settings, new NullLogger());
                var coord = new CourtDesk.UI.UiCoordinator(like);
                int done = coord.VoidMany(new System.Collections.Generic.List<int> { a, b });
                Assert.Equal(2, done);
                Assert.Equal(1, env.Db.Read(s => env.Defendants.CountActive(s)));
                Assert.True(c > 0);
            }
        }

        [Fact]
        public void RendersZeroThreeAndTwoHundredRows()
        {
            StaRunner.Run(delegate
            {
                using (var env = new UiEnv())
                {
                    var view = new RegisterView { DataContext = env.Register };
                    Assert.Equal(0, env.Register.Rows.Count);
                    Assert.True(env.Register.EmptyResult);

                    env.AddDefendant("Ahmed Hassan Ali", "1001", new LocalDate(2026, 10, 7));
                    env.AddDefendant("Mona Adel", "1002", new LocalDate(2026, 10, 1));
                    env.AddDefendant("Karim Said", "1003", null);
                    env.Refresh();
                    Assert.Equal(2, env.Register.Rows.Count);
                    Assert.Equal("Mona Adel", env.Register.Rows[0].Name);
                    Assert.Equal("Ahmed Hassan Ali", env.Register.Rows[1].Name);

                    for (int i = 0; i < 200; i++)
                    {
                        env.AddDefendant("Bulk Person " + i, (5000 + i).ToString(), new LocalDate(2026, 10, 7));
                    }
                    env.Refresh();
                    Assert.True(env.Register.Rows.Count >= 200);
                    view.Measure(new Size(1000, 700));
                    view.Arrange(new Rect(0, 0, 1000, 700));
                    view.UpdateLayout();
                    Assert.Equal(6, view.Grid.Columns.Count);
                    env.Register.Wide = true;
                    view.UpdateLayout();
                    Assert.True(view.Grid.Columns.Count >= 7);
                }
            });
        }

        [Fact]
        public void SearchFiltersOnEveryKeystroke()
        {
            StaRunner.Run(delegate
            {
                using (var env = new UiEnv())
                {
                    env.AddDefendant("Ahmed Hassan Ali", "1001", new LocalDate(2026, 10, 7));
                    env.AddDefendant("Mona Adel Ibrahim", "1002", new LocalDate(2026, 10, 7));
                    env.Refresh();
                    Assert.Equal(2, env.Register.Rows.Count);
                    env.Register.SearchText = "mona";
                    Assert.Equal(1, env.Register.Rows.Count);
                    Assert.Equal("Mona Adel Ibrahim", env.Register.Rows[0].Name);
                    env.Register.SearchText = "1001";
                    Assert.Equal(1, env.Register.Rows.Count);
                    env.Register.SearchText = "zzz_no_match";
                    Assert.Equal(0, env.Register.Rows.Count);
                    Assert.True(env.Register.EmptyResult);
                    env.Register.SearchText = "";
                    Assert.Equal(2, env.Register.Rows.Count);
                }
            });
        }

        [Fact]
        public void ChipsSwitchFilterAndSortToggles()
        {
            StaRunner.Run(delegate
            {
                using (var env = new UiEnv())
                {
                    env.AddDefendant("Det One", "2001", new LocalDate(2026, 10, 7));
                    env.AddDefendant("No Date One", "2002", null);
                    env.Refresh();
                    Assert.Equal(1, env.Register.Rows.Count);
                    env.Register.Filter = Filters.All;
                    Assert.Equal(2, env.Register.Rows.Count);
                    env.Register.Filter = Filters.NoDate;
                    Assert.Equal(1, env.Register.Rows.Count);
                    env.Register.Filter = Filters.All;
                    env.Register.SortCommand.Execute("name");
                    Assert.Equal(2, env.Register.Rows.Count);
                    string first = env.Register.Rows[0].Name;
                    env.Register.SortCommand.Execute("name");
                    Assert.Equal(2, env.Register.Rows.Count);
                    Assert.NotEqual(first, env.Register.Rows[0].Name);
                }
            });
        }

        [Fact]
        public void RtlAndButtonsHaveArabicContent()
        {
            StaRunner.Run(delegate
            {
                using (var env = new UiEnv())
                {
                    var view = new RegisterView { DataContext = env.Register };
                    view.Measure(new Size(1200, 800));
                    view.Arrange(new Rect(0, 0, 1200, 800));
                    view.UpdateLayout();
                    Assert.Equal(FlowDirection.RightToLeft, view.FlowDirection);
                    var buttons = new List<Button>();
                    CollectButtons(view, buttons);
                    Assert.True(buttons.Count >= 6);
                    foreach (Button b in buttons)
                    {
                        if (b.Visibility != Visibility.Visible)
                        {
                            continue;
                        }
                        if (b.Content == null)
                        {
                            Assert.True(IsInsideDataGrid(b), "button with null content outside grid chrome");
                            continue;
                        }
                        string text = b.Content as string;
                        if (text != null)
                        {
                            Assert.True(text.Length > 0, "empty button content");
                            Assert.True(ContainsArabic(text), "button without Arabic: " + text);
                            continue;
                        }
                        var labels = new List<TextBlock>();
                        CollectText(b, labels);
                        bool arabic = false;
                        foreach (TextBlock t in labels)
                        {
                            if (!string.IsNullOrEmpty(t.Text) && ContainsArabic(t.Text))
                            {
                                arabic = true;
                            }
                        }
                        Assert.True(arabic, "button without Arabic label: " + b.Name);
                    }
                    Button add = FindButton(view, CourtDesk.Resources.Strings.ActionAdd);
                    Assert.True(add != null, "add button not found");
                    var addLabels = new List<TextBlock>();
                    CollectText(add, addLabels);
                    Assert.Contains(addLabels,
                        delegate(TextBlock t) { return t.Text == CourtDesk.Resources.Strings.ActionAdd; });
                }
            });
        }

        [Fact]
        public void StatusChipsRenderEveryState()
        {
            StaRunner.Run(delegate
            {
                string[] states = new string[] { "TODAY", "OVERDUE", "UPCOMING", "RELEASED", "JUDGED", "NO_DATE" };
                foreach (string s in states)
                {
                    var chip = new StatusChip { StateKey = s, Word = "w" };
                    chip.Measure(new Size(200, 60));
                    chip.Arrange(new Rect(0, 0, 200, 60));
                    chip.UpdateLayout();
                    Assert.True(chip.ActualWidth >= 0);
                }
            });
        }

        [Fact]
        public void ZoomChangesLayoutTransform()
        {
            StaRunner.Run(delegate
            {
                using (var env = new UiEnv())
                {
                    Assert.Equal(1.0, env.Main.Zoom);
                    env.Main.ZoomInCommand.Execute(null);
                    Assert.Equal(1.25, env.Main.Zoom);
                    env.Main.ZoomInCommand.Execute(null);
                    Assert.Equal(1.5, env.Main.Zoom);
                    env.Main.ZoomInCommand.Execute(null);
                    Assert.Equal(1.5, env.Main.Zoom);
                    env.Main.ZoomOutCommand.Execute(null);
                    Assert.Equal(1.25, env.Main.Zoom);
                    var window = new MainWindow(env.Main);
                    window.Width = 1200;
                    window.Height = 800;
                    window.Measure(new Size(1200, 800));
                    window.Arrange(new Rect(0, 0, 1200, 800));
                    window.UpdateLayout();
                    var scale = (ScaleTransform)window.Root.LayoutTransform;
                    Assert.Equal(1.25, scale.ScaleX);
                    window.Close();
                }
            });
        }

        private static void CollectButtons(DependencyObject root, List<Button> outp)
        {
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                Button b = child as Button;
                if (b != null)
                {
                    outp.Add(b);
                }
                CollectButtons(child, outp);
            }
        }

        private static bool IsInsideDataGrid(DependencyObject obj)
        {
            DependencyObject at = obj;
            while (at != null)
            {
                if (at is DataGrid)
                {
                    return true;
                }
                at = VisualTreeHelper.GetParent(at);
            }
            return false;
        }

        private static Button FindButton(DependencyObject root, string content)
        {
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                Button b = child as Button;
                if (b != null && (b.Content as string) == content)
                {
                    return b;
                }
                if (b != null)
                {
                    var labels = new List<TextBlock>();
                    CollectText(b, labels);
                    foreach (TextBlock t in labels)
                    {
                        if (t.Text == content)
                        {
                            return b;
                        }
                    }
                }
                Button found = FindButton(child, content);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private static void CollectText(DependencyObject root, List<TextBlock> outp)
        {
            int n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                TextBlock t = child as TextBlock;
                if (t != null)
                {
                    outp.Add(t);
                }
                CollectText(child, outp);
            }
        }

        private static bool ContainsArabic(string s)
        {
            foreach (char c in s)
            {
                if (c >= '؀' && c <= 'ۿ')
                {
                    return true;
                }
            }
            return false;
        }
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CourtDesk.Resources;
using CourtDesk.UI.Controls;
using CourtDesk.UI.ViewModels;

namespace CourtDesk.UI.Views
{
    public partial class RegisterView : UserControl
    {
        public RegisterView()
        {
            InitializeComponent();
            DataContextChanged += delegate(object s, DependencyPropertyChangedEventArgs e) { BuildColumns(); };
            Loaded += delegate(object s, RoutedEventArgs e) { BuildColumns(); };
        }

        public event Action<string> ActionRequested;

        public Action<int> AddRequested;
        public Action<int> EditRequested;
        public Action<int> DecideRequested;
        public Action<int> CardRequested;
        public Action<int> VoidRequested;

        private RegisterViewModel Vm
        {
            get { return DataContext as RegisterViewModel; }
        }

        private void BuildColumns()
        {
            RegisterViewModel vm = Vm;
            if (vm == null || Grid.Columns.Count > 0)
            {
                return;
            }
            AddTextColumn(Strings.GridIndex, "Number", 60, "name", true);
            AddTextColumn(Strings.GridName, "Name", 190, "name", false);
            AddTextColumn(Strings.GridCase, "CaseText", 120, "case", false);
            AddTextColumn(Strings.GridCourt, "CourtText", 150, "court", false);
            AddTextColumn(Strings.GridSession, "SessionText", 150, "session", false);
            Grid.Columns.Add(StateColumn());
            Grid.RowStyle = BuildRowStyle();
            vm.PropertyChanged += delegate(object s, System.ComponentModel.PropertyChangedEventArgs e)
            {
                if (e.PropertyName == "Wide")
                {
                    ApplyWide(vm.Wide);
                }
                if (e.PropertyName == "HintVisible" || e.PropertyName == "EmptyResult")
                {
                    ApplyBands(vm);
                }
            };
            ApplyWide(vm.Wide);
            ApplyBands(vm);
            AddButton.Click += delegate(object s, RoutedEventArgs e) { Fire("add"); };
            EditButton.Click += delegate(object s, RoutedEventArgs e) { Fire("edit"); };
            DecideButton.Click += delegate(object s, RoutedEventArgs e) { Fire("decide"); };
            CardButton.Click += delegate(object s, RoutedEventArgs e) { Fire("card"); };
            VoidButton.Click += delegate(object s, RoutedEventArgs e) { Fire("void"); };
            SearchInput.Focus();
        }

        private void Fire(string what)
        {
            RegisterViewModel vm = Vm;
            int id = vm != null && vm.Selected != null ? vm.Selected.Id : 0;
            switch (what)
            {
                case "add":
                    if (AddRequested != null)
                    {
                        AddRequested(id);
                    }
                    break;
                case "edit":
                    if (id == 0)
                    {
                        vm.ShowHint(CourtDesk.Resources.Strings.HintSelectRow);
                        return;
                    }
                    if (EditRequested != null)
                    {
                        EditRequested(id);
                    }
                    break;
                case "decide":
                    if (id == 0)
                    {
                        vm.ShowHint(CourtDesk.Resources.Strings.HintSelectRow);
                        return;
                    }
                    if (DecideRequested != null)
                    {
                        DecideRequested(id);
                    }
                    break;
                case "card":
                    if (id == 0)
                    {
                        vm.ShowHint(CourtDesk.Resources.Strings.HintSelectRow);
                        return;
                    }
                    if (CardRequested != null)
                    {
                        CardRequested(id);
                    }
                    break;
                case "void":
                    if (id == 0)
                    {
                        vm.ShowHint(CourtDesk.Resources.Strings.HintSelectRow);
                        return;
                    }
                    if (VoidRequested != null)
                    {
                        VoidRequested(id);
                    }
                    break;
            }
        }

        private void ApplyWide(bool wide)
        {
            WideButton.Content = wide ? Strings.GridWideOff : Strings.GridWideOn;
            bool hasCharges = false;
            bool hasCustody = false;
            foreach (DataGridColumn c in Grid.Columns)
            {
                if (HeaderText(c) == Strings.GridCharges)
                {
                    hasCharges = true;
                }
                if (HeaderText(c) == Strings.GridCustody)
                {
                    hasCustody = true;
                }
            }
            if (wide && !hasCharges)
            {
                int at = Grid.Columns.Count - 1;
                Grid.Columns.Insert(at, MakeTextColumn(Strings.GridCharges, "CourtText", 180));
                Grid.Columns.Insert(at + 1, MakeTextColumn(Strings.GridCustody, "CourtText", 130));
            }
            if (!wide && hasCharges)
            {
                for (int i = Grid.Columns.Count - 1; i >= 0; i--)
                {
                    string h = HeaderText(Grid.Columns[i]);
                    if (h == Strings.GridCharges || h == Strings.GridCustody)
                    {
                        Grid.Columns.RemoveAt(i);
                    }
                }
            }
        }

        private static string HeaderText(DataGridColumn col)
        {
            string s = col.Header as string;
            if (s != null)
            {
                return s;
            }
            Button b = col.Header as Button;
            if (b != null)
            {
                return b.Content as string;
            }
            return null;
        }

        private void ApplyBands(RegisterViewModel vm)
        {
            HintBand.Visibility = vm.HintVisible ? Visibility.Visible : Visibility.Collapsed;
            if (vm.EmptyResult)
            {
                if (vm.EmptyInFilter)
                {
                    EmptyText.Text = string.Format(Strings.SearchNoneInFilter, FilterLabel(), vm.AllCount);
                }
                else
                {
                    EmptyText.Text = Strings.SearchNone;
                }
                EmptyText.Visibility = Visibility.Visible;
            }
            else
            {
                EmptyText.Visibility = Visibility.Collapsed;
            }
        }

        private string FilterLabel()
        {
            RegisterViewModel vm = Vm;
            if (vm == null)
            {
                return "";
            }
            foreach (FilterChipItem chip in vm.Chips)
            {
                if (chip.Checked)
                {
                    return chip.Label;
                }
            }
            return "";
        }

        private void AddTextColumn(string header, string path, double width, string sortKey, bool index)
        {
            DataGridTextColumn col = MakeTextColumn(header, path, width);
            col.Header = HeaderButton(header, sortKey);
            Grid.Columns.Add(col);
        }

        private static DataGridTextColumn MakeTextColumn(string header, string path, double width)
        {
            var col = new DataGridTextColumn();
            col.Header = header;
            col.Binding = new Binding(path);
            col.Width = width;
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.FontSizeProperty, (double)20));
            style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
            style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
            style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(8, 4, 8, 4)));
            style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
            col.ElementStyle = style;
            return col;
        }

        private Button HeaderButton(string header, string sortKey)
        {
            var b = new Button();
            b.Content = header;
            b.FontSize = 20;
            b.FontWeight = FontWeights.Bold;
            b.FontFamily = CourtDesk.UI.UiRes.Font(this);
            b.Background = System.Windows.Media.Brushes.Transparent;
            b.BorderThickness = new Thickness(0);
            b.Cursor = System.Windows.Input.Cursors.Hand;
            b.Tag = sortKey;
            b.Click += delegate(object s, RoutedEventArgs e)
            {
                RegisterViewModel vm = Vm;
                if (vm != null)
                {
                    vm.SortCommand.Execute(sortKey);
                }
            };
            return b;
        }

        private DataGridTemplateColumn StateColumn()
        {
            var col = new DataGridTemplateColumn();
            col.Header = HeaderButton(Strings.GridState, "state");
            col.Width = 150;
            var factory = new FrameworkElementFactory(typeof(StatusChip));
            factory.SetBinding(StatusChip.StateKeyProperty, new Binding("StateKey"));
            factory.SetBinding(StatusChip.WordProperty, new Binding("StateWord"));
            col.CellTemplate = new DataTemplate(typeof(StatusChip)) { VisualTree = factory };
            return col;
        }

        private Style BuildRowStyle()
        {
            var style = new Style(typeof(DataGridRow));
            style.Setters.Add(new Setter(DataGridRow.MinHeightProperty, (double)56));
            AddTint(style, "TODAY", "TodayBgBrush");
            AddTint(style, "OVERDUE", "OverdueBgBrush");
            return style;
            AddTint(style, "OVERDUE", "OverdueBgBrush");
            return style;
        }

        private void AddTint(Style style, string key, string brushKey)
        {
            var trigger = new MultiDataTrigger();
            trigger.Conditions.Add(new Condition
            {
                Binding = new Binding("StateKey"),
                Value = key
            });
            trigger.Conditions.Add(new Condition
            {
                Binding = new Binding("IsSelected") { RelativeSource = RelativeSource.Self },
                Value = false
            });
            trigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty,
                CourtDesk.UI.UiRes.Brush(this, brushKey, brushKey == "TodayBgBrush" ? "#FFF1B8" : "#FFD9D4")));
            style.Triggers.Add(trigger);
        }
    }
}

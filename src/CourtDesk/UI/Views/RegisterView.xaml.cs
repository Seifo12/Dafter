using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using CourtDesk.Domain;
using CourtDesk.Resources;
using CourtDesk.UI.Controls;
using CourtDesk.UI.ViewModels;

namespace CourtDesk.UI.Views
{
    public partial class RegisterView : UserControl
    {
        private bool _compact;
private bool _narrow;
        private bool _chromeApplied;
        private bool _selectAllHooked;

        public RegisterView()
        {
            InitializeComponent();
            DataContextChanged += delegate { BuildColumns(); };
            Loaded += delegate { BuildColumns(); };
        }

        public Action<int> AddRequested;
        public Action<int> EditRequested;
        public Action<int> DecideRequested;
        public Action<int> CardRequested;
        public Action<int> VoidRequested;
        public Action<System.Collections.Generic.IReadOnlyList<int>> PrintCheckedRequested;

        private void PrintCheckedClick(object sender, RoutedEventArgs e)
        {
            RegisterViewModel vm = Vm;
            if (vm == null)
            {
                return;
            }
            System.Collections.Generic.IReadOnlyList<int> ids = vm.CheckedIds;
            if (ids.Count == 0)
            {
                return;
            }
            if (PrintCheckedRequested != null)
            {
                PrintCheckedRequested(ids);
            }
        }

        public void ApplyCheckedCount(int n)
        {
            bool show = n > 0;
            PrintCheckedButton.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (show)
            {
                PrintCheckedText.Text = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    Strings.PrintSelected, n);
            }
        }

public void SetCompact(bool compact)
        {
            SetCompact(compact, compact);
        }

        public void SetCompact(bool compact, bool narrow)
        {
            _compact = compact;
            _narrow = narrow;
            if (compact && AddStack != null && AddStack.Parent == null)
            {
                WrapActionContent();
            }
            ApplyButtonLayout(DecideButton, "IconGavel", Strings.ActionDecide);
            ApplyButtonLayout(AddButton, "IconAdd", Strings.ActionAdd);
            ApplyButtonLayout(EditButton, "IconEdit", Strings.ActionEdit);
            ApplyButtonLayout(CardButton, "IconCard", Strings.ActionCard);
            ApplyButtonLayout(VoidButton, "IconTrash", Strings.ActionVoid);
            ActionBar.Height = compact ? 84 : 56;
            Grid.HorizontalScrollBarVisibility = narrow
                ? System.Windows.Controls.ScrollBarVisibility.Auto
                : System.Windows.Controls.ScrollBarVisibility.Hidden;
            if (Grid.Columns.Count > 3)
            {
                // widths by column role, never by position: inserted columns
                // must not shift the layout of the existing ones
                DataGridColumn indexCol = ColByHeader(Strings.GridIndex);
                DataGridColumn checkCol = CheckCol();
                DataGridColumn nameCol = ColByHeader(Strings.GridCase);
                DataGridColumn courtCol = ColByHeader(Strings.GridCourt);
                DataGridColumn sessionCol = ColByHeader(Strings.GridSession);
                DataGridColumn stateCol = ColByHeader(Strings.GridState);
                if (indexCol != null)
                {
                    SetWidth(indexCol, narrow ? 40 : (compact ? 52 : 68), narrow ? 40 : (compact ? 52 : 68));
                }
                if (checkCol != null)
                {
                    SetWidth(checkCol, narrow ? 40 : (compact ? 44 : 48), narrow ? 40 : (compact ? 44 : 48));
                }
                if (nameCol != null)
                {
                    SetWidth(nameCol, narrow ? 150 : (compact ? 190 : 236), narrow ? 150 : (compact ? 190 : 236));
                }
                if (courtCol != null)
                {
                    SetWidth(courtCol, narrow ? 110 : (compact ? 150 : 202), narrow ? 110 : (compact ? 150 : 202));
                }
                if (sessionCol != null)
                {
                    SetWidth(sessionCol, narrow ? 128 : (compact ? 150 : 186), narrow ? 128 : (compact ? 150 : 186));
                }
                if (stateCol != null)
                {
                    SetWidth(stateCol, narrow ? 96 : (compact ? 150 : 176), narrow ? 96 : (compact ? 150 : 176));
                }
            }
        }

        private DataGridColumn ColByHeader(string header)
        {
            foreach (DataGridColumn c in Grid.Columns)
            {
                if ((c.Header as string) == header)
                {
                    return c;
                }
            }
            return null;
        }

        private DataGridColumn CheckCol()
        {
            foreach (DataGridColumn c in Grid.Columns)
            {
                if (c.Header is CheckBox)
                {
                    return c;
                }
            }
            return null;
        }

        private static void SetWidth(DataGridColumn col, double width, double min)
        {
            col.Width = new DataGridLength(width);
            col.MinWidth = min;
            double mx = col.MaxWidth;
            if (double.IsNaN(mx) || double.IsInfinity(mx))
            {
                col.MaxWidth = width * 1.6;
            }
        }

private void WrapActionContent()
        {
            var addContent = AddButton.Content as UIElement;
            if (addContent != null
                && System.Windows.Media.VisualTreeHelper.GetParent(addContent) == null)
            {
                AddButton.Content = AddStack;
                AddStack.Children.Add(addContent);
            }
        }

        private void ApplyButtonLayout(Button b, string iconKey, string label)
        {
            if (b == null)
            {
                return;
            }
            var panel = new StackPanel();
            panel.Orientation = _compact ? Orientation.Vertical : Orientation.Horizontal;
            panel.HorizontalAlignment = HorizontalAlignment.Center;
            panel.VerticalAlignment = VerticalAlignment.Center;
            var path = new System.Windows.Shapes.Path
            {
                Data = (Geometry)TryFindResource(iconKey),
                Stroke = (Brush)TryFindResource(b.Tag as string == "void" ? "DangerBrush" : "InkBrush"),
                StrokeThickness = 2.0,
                Fill = Brushes.Transparent,
                Width = 20,
                Height = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (b == DecideButton)
            {
                path.Stroke = (Brush)TryFindResource("WhiteBrush");
            }
            panel.Children.Add(path);
            var text = new TextBlock
            {
                Text = label,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(_compact ? 0 : 8, _compact ? 2 : 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = _compact ? TextWrapping.Wrap : TextWrapping.NoWrap,
                TextTrimming = _compact ? TextTrimming.None : TextTrimming.CharacterEllipsis
            };
            text.SetResourceReference(TextBlock.FontFamilyProperty, "AppFont");
            text.SetResourceReference(TextBlock.ForegroundProperty, b == DecideButton ? "WhiteBrush" : (b == VoidButton ? "DangerBrush" : "InkBrush"));
            panel.Children.Add(text);
            b.Content = panel;
            if (_compact)
            {
b.Width = _narrow ? 128 : 116;
                b.Height = 72;
                b.MinWidth = 0;
                b.Padding = new Thickness(4, 2, 4, 2);
            }
            else
            {
b.ClearValue(FrameworkElement.WidthProperty);
                b.Height = 52;
                b.MinWidth = 0;
                b.Padding = new Thickness(16, 4, 16, 4);
            }
        }

        public void SelectAndShow(int id)
        {
            if (id <= 0)
            {
                return;
            }
            RegisterViewModel vm = Vm;
            if (vm == null)
            {
                return;
            }
            foreach (RegisterRow row in vm.Rows)
            {
                if (row.Id == id)
                {
                    vm.Selected = row;
                    Grid.ScrollIntoView(row);
                    Grid.Focus();
                    return;
                }
            }
        }

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
            Grid.Columns.Add(IndexColumn());
            Grid.Columns.Add(CheckColumn());
            var nameCol = new DataGridTemplateColumn();
            nameCol.Header = Strings.GridCase;
            nameCol.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
            nameCol.MinWidth = 160;
            nameCol.CellTemplate = TwoLine("NameText", "MutedText", "Name", "CaseText", true, true);
            nameCol.SortMemberPath = "Name";
            Grid.Columns.Add(nameCol);
            Grid.Columns.Add(MakeColumn(Strings.GridCourt, 202, "CourtText", "CourtText", true, false));
            var sessionCol = new DataGridTemplateColumn();
            sessionCol.Header = Strings.GridSession;
            sessionCol.Width = 190;
            sessionCol.MinWidth = 190;
            sessionCol.CellTemplate = TwoLine("GridStrong", "MutedText", "SessionLine1", "SessionLine2", false, false);
            sessionCol.SortMemberPath = "SessionLine1";
            Grid.Columns.Add(sessionCol);
            Grid.Columns.Add(StateColumn());
            ApplyGridChrome();
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
                if (e.PropertyName == "SearchText")
                {
                    ApplySearchBox(vm);
                }
                if (e.PropertyName == "CheckedCount")
                {
                    ApplyCheckedCount(vm.CheckedCount);
                }
            };
ApplyWide(vm.Wide);
            ApplyBands(vm);
            ApplySearchBox(vm);
            SetCompact(_compact);
            Grid.Loaded += delegate { HookSelectAll(); };
            Loaded += delegate { SearchInput.Focus(); HookSelectAll(); };
        }

        private void ApplyGridChrome()
        {
            if (_chromeApplied)
            {
                return;
            }
            _chromeApplied = true;
            var header = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
            header.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.HeightProperty, 44.0));
            header.Setters.Add(new Setter(Control.FontFamilyProperty, UiRes.Font(this)));
            header.Setters.Add(new Setter(Control.FontSizeProperty, 18.0));
            header.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
            header.Setters.Add(new Setter(Control.ForegroundProperty, UiRes.Brush(this, "InkBrush", "#1B2430")));
            header.Setters.Add(new Setter(Control.BackgroundProperty, UiRes.Brush(this, "GridHeaderBgBrush", "#E8EDF3")));
header.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.SeparatorBrushProperty, Brushes.Transparent));
            header.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Right));
            header.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            header.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0)));
            header.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.ContentStringFormatProperty, null));
            Grid.ColumnHeaderStyle = header;

            var row = new Style(typeof(DataGridRow));
            row.Setters.Add(new Setter(DataGridRow.MinHeightProperty, 60.0));
            row.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 1)));
            row.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            row.Setters.Add(new Setter(Control.BorderBrushProperty, UiRes.Brush(this, "GridLineBrush", "#E1E6ED")));
            row.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
            row.Setters.Add(new Setter(Control.BorderBrushProperty, UiRes.Brush(this, "GridLineBrush", "#E1E6ED")));
            AddRowTriggers(row);
            Grid.RowStyle = row;

            var cell = new Style(typeof(DataGridCell));
            cell.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0)));
            cell.Setters.Add(new Setter(DataGridCell.FocusVisualStyleProperty, null));
            cell.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            cell.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0)));
            cell.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 4, 12, 4)));
            cell.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Right));
            cell.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            Grid.CellStyle = cell;
        }

        private void AddRowTriggers(Style row)
        {
            var hover = new Trigger();
            hover.Property = UIElement.IsMouseOverProperty;
            hover.Value = true;
            hover.Setters.Add(new Setter(Control.BackgroundProperty, UiRes.Brush(this, "HoverBrush", "#EEF4FB")));
            row.Triggers.Add(hover);
            var selected = new Trigger();
            selected.Property = DataGridRow.IsSelectedProperty;
            selected.Value = true;
            selected.Setters.Add(new Setter(Control.BackgroundProperty, UiRes.Brush(this, "SelectedBgBrush", "#D6E8FA")));
            selected.Setters.Add(new Setter(Control.BorderBrushProperty, UiRes.Brush(this, "PrimaryBrush", "#084A8F")));
            selected.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(2)));
            row.Triggers.Add(selected);
        }

        private void HookSelectAll()
        {
            if (_selectAllHooked)
            {
                return;
            }
            _selectAllHooked = true;
            foreach (DataGridColumn c in Grid.Columns)
            {
                var box = c.Header as CheckBox;
                if (box != null && "select-all" as string == box.Tag as string)
                {
                    box.Checked += SelectAllChecked;
                    box.Unchecked += SelectAllUnchecked;
                }
            }
        }

        private void SelectAllChecked(object sender, RoutedEventArgs e)
        {
            RegisterViewModel vm = Vm;
            if (vm == null)
            {
                return;
            }
            vm.SelectAllVisible(true);
        }

        private void SelectAllUnchecked(object sender, RoutedEventArgs e)
        {
            RegisterViewModel vm = Vm;
            if (vm == null)
            {
                return;
            }
            vm.SelectAllVisible(false);
        }

        private DataGridTemplateColumn CheckColumn()
        {
            var col = new DataGridTemplateColumn();
            col.Header = new CheckBox
            {
                MinWidth = 48,
                MinHeight = 48,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Tag = "select-all"
            };
            col.Width = 56;
            col.MinWidth = 56;
            col.MaxWidth = 56;
            var box = new FrameworkElementFactory(typeof(CheckBox));
            box.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
                new Binding("Checked") { Mode = BindingMode.TwoWay });
            box.SetValue(FrameworkElement.MarginProperty, new Thickness(0));
            box.SetValue(FrameworkElement.MinWidthProperty, 40.0);
            box.SetValue(FrameworkElement.MinHeightProperty, 40.0);
            box.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            box.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            box.SetValue(System.Windows.Controls.Primitives.ToggleButton.IsThreeStateProperty, false);
            col.CellTemplate = new DataTemplate(typeof(CheckBox)) { VisualTree = box };
            return col;
        }

        private DataGridTemplateColumn IndexColumn()
        {
            var col = new DataGridTemplateColumn();
            col.Header = Strings.GridIndex;
            col.Width = 74;
            col.MinWidth = 74;
            col.MaxWidth = 74;
            col.SortMemberPath = "Number";
            var grid = new FrameworkElementFactory(typeof(Grid));
            var c0 = new FrameworkElementFactory(typeof(ColumnDefinition));
            c0.SetValue(ColumnDefinition.WidthProperty, new GridLength(6));
            var c1 = new FrameworkElementFactory(typeof(ColumnDefinition));
            c1.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
            grid.AppendChild(c0);
            grid.AppendChild(c1);
            var stripe = new FrameworkElementFactory(typeof(Border));
            stripe.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 6, 0));
            stripe.SetBinding(Border.BorderBrushProperty, new Binding("StripeBrush"));
            stripe.SetValue(System.Windows.Controls.Grid.ColumnProperty, 0);
            grid.AppendChild(stripe);
            var num = new FrameworkElementFactory(typeof(TextBlock));
            num.SetBinding(TextBlock.TextProperty, new Binding("Number"));
            num.SetValue(TextBlock.FontSizeProperty, 22.0);
            num.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            num.SetValue(TextBlock.FontFamilyProperty, UiRes.Font(this));
            num.SetValue(TextBlock.ForegroundProperty, UiRes.Brush(this, "MutedBrush", "#46505E"));
            num.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
            num.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            num.SetValue(System.Windows.Controls.Grid.ColumnProperty, 1);
            grid.AppendChild(num);
            col.CellTemplate = new DataTemplate(typeof(Grid)) { VisualTree = grid };
            return col;
        }

        private DataGridTextColumn MakeColumn(string header, double width, string path, string sortKey,
            bool allowTrim, bool wrap)
        {
            var col = new DataGridTextColumn();
            col.Header = header;
            col.Binding = new Binding(path);
            col.Width = width;
            col.MinWidth = width;
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.FontSizeProperty, 20.0));
            style.Setters.Add(new Setter(TextBlock.FontFamilyProperty, UiRes.Font(this)));
            style.Setters.Add(new Setter(TextBlock.ForegroundProperty, UiRes.Brush(this, "InkBrush", "#1B2430")));
            if (wrap)
            {
                style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
            }
            else
            {
                style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty,
                    allowTrim ? TextTrimming.CharacterEllipsis : TextTrimming.None));
                if (allowTrim)
                {
                    style.Setters.Add(new Setter(FrameworkElement.TagProperty, "EllipsisAllowed"));
                }
            }
            style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
            col.ElementStyle = style;
            col.SortMemberPath = sortKey;
            return col;
        }

        private DataTemplate TwoLine(string topStyle, string bottomStyle, string top, string bottom,
            bool topWrap, bool bottomAllowTrim)
        {
            var grid = new FrameworkElementFactory(typeof(Grid));
            var rows = new FrameworkElementFactory(typeof(RowDefinition));
            rows.SetValue(RowDefinition.HeightProperty, new GridLength(1, GridUnitType.Star));
            var rows2 = new FrameworkElementFactory(typeof(RowDefinition));
            rows2.SetValue(RowDefinition.HeightProperty, new GridLength(1, GridUnitType.Star));
            grid.AppendChild(rows);
            grid.AppendChild(rows2);
            var t1 = new FrameworkElementFactory(typeof(TextBlock));
            t1.SetBinding(TextBlock.TextProperty, new Binding(top));
            t1.SetValue(TextBlock.StyleProperty, TryStyle(topStyle));
            t1.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            t1.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 2));
            t1.SetValue(TextBlock.TextWrappingProperty, topWrap ? TextWrapping.Wrap : TextWrapping.NoWrap);
            t1.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.None);
            t1.SetValue(System.Windows.Controls.Grid.RowProperty, 0);
            grid.AppendChild(t1);
            var t2 = new FrameworkElementFactory(typeof(TextBlock));
            t2.SetBinding(TextBlock.TextProperty, new Binding(bottom));
            t2.SetValue(TextBlock.StyleProperty, TryStyle(bottomStyle));
            t2.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            t2.SetValue(FrameworkElement.MarginProperty, new Thickness(0));
            t2.SetValue(TextBlock.TextWrappingProperty, TextWrapping.NoWrap);
            t2.SetValue(TextBlock.TextTrimmingProperty,
                bottomAllowTrim ? TextTrimming.CharacterEllipsis : TextTrimming.None);
            if (bottomAllowTrim)
            {
                t2.SetValue(FrameworkElement.TagProperty, "EllipsisAllowed");
            }
            t2.SetValue(System.Windows.Controls.Grid.RowProperty, 1);
            grid.AppendChild(t2);
            return new DataTemplate(typeof(Grid)) { VisualTree = grid };
        }

        private Style TryStyle(string key)
        {
            object o = TryFindResource(key);
            return o as Style;
        }

        private DataGridTemplateColumn StateColumn()
        {
            var col = new DataGridTemplateColumn();
            col.Header = Strings.GridState;
            col.Width = 176;
            col.MinWidth = 176;
            col.MaxWidth = 176;
            var factory = new FrameworkElementFactory(typeof(StatusChip));
            factory.SetBinding(StatusChip.StateKeyProperty, new Binding("StateKey"));
            factory.SetBinding(StatusChip.WordProperty, new Binding("StateWord"));
            factory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            factory.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 8, 0));
            col.CellTemplate = new DataTemplate(typeof(StatusChip)) { VisualTree = factory };
            return col;
        }

        private void ApplyWide(bool wide)
        {
            bool hasCharges = false;
            foreach (DataGridColumn c in Grid.Columns)
            {
                if ((c.Header as string) == Strings.GridCharges)
                {
                    hasCharges = true;
                }
            }
            if (wide && !hasCharges)
            {
                int at = Grid.Columns.Count;
                for (int i = 0; i < Grid.Columns.Count; i++)
                {
                    if ((Grid.Columns[i].Header as string) == Strings.GridSession)
                    {
                        at = i + 1;
                        break;
                    }
                }
                Grid.Columns.Insert(at, MakeColumn(Strings.GridCharges, 220, "ChargesText", "ChargesText", false, true));
                Grid.Columns.Add(MakeColumn(Strings.GridCustody, 170, "CustodyText", "CustodyText", false, true));
                DataGridColumn sessionCol = ColByHeader(Strings.GridSession);
                if (sessionCol != null)
                {
                    sessionCol.MinWidth = 170;
                }
            }
            if (!wide && hasCharges)
            {
                for (int i = Grid.Columns.Count - 1; i >= 0; i--)
                {
                    string h = Grid.Columns[i].Header as string;
                    if (h == Strings.GridCharges || h == Strings.GridCustody)
                    {
                        Grid.Columns.RemoveAt(i);
                    }
                }
                DataGridColumn sessionCol = ColByHeader(Strings.GridSession);
                if (sessionCol != null)
                {
                    sessionCol.MinWidth = _compact ? 150 : 190;
                }
            }
        }

        private void ApplyBands(RegisterViewModel vm)
        {
            if (vm.EmptyResult)
            {
                EmptyText.Text = vm.EmptyInFilter
                    ? string.Format(Strings.SearchNoneInFilter, FilterLabel(), vm.AllCount)
                    : Strings.SearchNone;
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
            if (vm == null) return "";
            foreach (FilterChipItem chip in vm.Chips)
            {
                if (chip.Checked) return chip.Label;
            }
            return "";
        }

        private void ApplySearchBox(RegisterViewModel vm)
        {
            bool has = vm != null && !string.IsNullOrEmpty(vm.SearchText);
            ClearButton.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
            SearchHint.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ClearClick(object sender, RoutedEventArgs e)
        {
            RegisterViewModel vm = Vm;
            if (vm != null)
            {
                vm.SearchText = "";
                SearchInput.Focus();
            }
        }

        private void ActionClick(object sender, RoutedEventArgs e)
        {
            var b = sender as Button;
            if (b == null || b.Tag == null)
            {
                return;
            }
            string what = (string)b.Tag;
            RegisterViewModel vm = Vm;
            int id = vm != null && vm.Selected != null ? vm.Selected.Id : 0;
            switch (what)
            {
                case "add":
                    if (AddRequested != null) AddRequested(id);
                    break;
                case "edit":
                    if (NeedRow(vm, id)) { if (EditRequested != null) EditRequested(id); }
                    break;
                case "decide":
                    if (NeedRow(vm, id)) { if (DecideRequested != null) DecideRequested(id); }
                    break;
                case "card":
                    if (NeedRow(vm, id)) { if (CardRequested != null) CardRequested(id); }
                    break;
                case "void":
                    if (NeedRow(vm, id)) { if (VoidRequested != null) VoidRequested(id); }
                    break;
            }
        }

        private static bool NeedRow(RegisterViewModel vm, int id)
        {
            if (id != 0)
            {
                return true;
            }
            if (vm != null)
            {
                vm.ShowHint(Strings.HintSelectRow);
            }
            return false;
        }
    }
}
using System.Windows;
using System.Windows.Controls;
using CourtDesk.Resources;

namespace CourtDesk.UI.Views
{
    public partial class TodaySidebar : UserControl
    {
        public TodaySidebar()
        {
            InitializeComponent();
            DataContextChanged += delegate { Apply(); };
            Loaded += delegate { Apply(); };
        }

        private ViewModels.TodaySidebarViewModel Vm
        {
            get { return DataContext as ViewModels.TodaySidebarViewModel; }
        }

        private void Apply()
        {
            ViewModels.TodaySidebarViewModel vm = Vm;
            if (vm == null)
            {
                return;
            }
            ApplyScopeDate(vm);
            SyncPrintScopeBox(vm.PrintScope);
            vm.PropertyChanged += delegate(object s, System.ComponentModel.PropertyChangedEventArgs e)
            {
                if (e.PropertyName == "PrintScope")
                {
                    SyncPrintScopeBox(vm.PrintScope);
                    return;
                }
                if (e.PropertyName == "DateLong" || e.PropertyName == "Empty" || e.PropertyName == "Tab"
                    || e.PropertyName == "MoreDecided" || e.PropertyName == "MoreDecidedText"
                    || e.PropertyName == "HasMoreDecided" || e.PropertyName == "DecidedToggleText"
                    || e.PropertyName == "DecidedExpanded")
                {
                    ApplyBands(vm);
                }
            };
            ApplyBands(vm);
        }

        private void ScopePrevDayClick(object sender, System.Windows.RoutedEventArgs e)
        {
            ViewModels.TodaySidebarViewModel vm = Vm;
            if (vm != null)
            {
                vm.ShiftScopeDate(-1);
                ApplyScopeDate(vm);
            }
        }

        private void ScopeNextDayClick(object sender, System.Windows.RoutedEventArgs e)
        {
            ViewModels.TodaySidebarViewModel vm = Vm;
            if (vm != null)
            {
                vm.ShiftScopeDate(1);
                ApplyScopeDate(vm);
            }
        }

        private void ApplyScopeDate(ViewModels.TodaySidebarViewModel vm)
        {
            ScopeDateText.Text = CourtDesk.Domain.ArabicDateFormat.FormatShort(vm.ScopeDate);
        }

        private void PrintScopeChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ViewModels.TodaySidebarViewModel vm = Vm;
            if (vm == null)
            {
                return;
            }
            var item = PrintScopeBox.SelectedItem as System.Windows.Controls.ComboBoxItem;
            if (item != null && item.Tag != null)
            {
                vm.PrintScope = System.Convert.ToString(item.Tag);
            }
        }

        private void SyncPrintScopeBox(string scope)
        {
            foreach (object o in PrintScopeBox.Items)
            {
                var item = o as System.Windows.Controls.ComboBoxItem;
                if (item != null && System.Convert.ToString(item.Tag) == scope)
                {
                    if (!ReferenceEquals(PrintScopeBox.SelectedItem, item))
                    {
                        PrintScopeBox.SelectedItem = item;
                    }
                    return;
                }
            }
        }

        private void NameClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            TextBlock tb = sender as TextBlock;
            ViewModels.SessionCard card = tb == null ? null : tb.DataContext as ViewModels.SessionCard;
            ViewModels.TodaySidebarViewModel vm = Vm;
            if (card == null || vm == null || vm.CardCommand == null)
            {
                return;
            }
            if (vm.CardCommand.CanExecute(card))
            {
                vm.CardCommand.Execute(card);
            }
        }

        private void ApplyBands(ViewModels.TodaySidebarViewModel vm)
        {
            DateLine.Text = vm.DateLineText;
            EmptyText.Visibility = vm.Empty ? Visibility.Visible : Visibility.Collapsed;
            DecidedBlock.Visibility = vm.DecidedCards.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            OverdueList.Visibility = vm.OverdueCards.Count == 0 ? Visibility.Collapsed
                : (vm.Tab == "overdue" ? Visibility.Visible : Visibility.Collapsed);
            PendingList.Visibility = vm.PendingCards.Count == 0 ? Visibility.Collapsed
                : (vm.Tab == "today" ? Visibility.Visible : Visibility.Collapsed);
            TomorrowList.Visibility = vm.TomorrowCards.Count == 0 ? Visibility.Collapsed
                : (vm.Tab == "tomorrow" ? Visibility.Visible : Visibility.Collapsed);
            TodayTab.IsChecked = vm.Tab == "today";
            TomorrowTab.IsChecked = vm.Tab == "tomorrow";
            TomorrowTab.Visibility = vm.TomorrowCards.Count > 0
                ? Visibility.Visible : Visibility.Collapsed;
            OverdueTab.IsChecked = vm.Tab == "overdue";
            OverdueTab.Visibility = vm.OverdueCards.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            MoreDecidedText.Text = vm.MoreDecidedText;
            MoreDecidedText.Visibility = vm.MoreDecided > 0 ? Visibility.Visible : Visibility.Collapsed;
            DecidedToggle.Content = vm.DecidedToggleText;
            DecidedToggle.Visibility = vm.HasMoreDecided ? Visibility.Visible : Visibility.Collapsed;
            DecidedMoreRow.Visibility = (vm.MoreDecided > 0 || vm.HasMoreDecided)
                ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
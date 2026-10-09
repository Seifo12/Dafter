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
            vm.PropertyChanged += delegate(object s, System.ComponentModel.PropertyChangedEventArgs e)
            {
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
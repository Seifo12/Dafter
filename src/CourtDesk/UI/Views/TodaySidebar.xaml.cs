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
                if (e.PropertyName == "DateLong" || e.PropertyName == "Empty" || e.PropertyName == "Tab")
                {
                    ApplyBands(vm);
                }
            };
            ApplyBands(vm);
        }

        private void ApplyBands(ViewModels.TodaySidebarViewModel vm)
        {
            DateLine.Text = vm.DateLineText;
            EmptyText.Visibility = vm.Empty ? Visibility.Visible : Visibility.Collapsed;
            DecidedBlock.Visibility = vm.DecidedCards.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            OverdueList.Visibility = vm.OverdueCards.Count == 0 ? Visibility.Collapsed
                : (vm.Tab == "overdue" ? Visibility.Visible : Visibility.Collapsed);
            PendingList.Visibility = vm.PendingCards.Count == 0 ? Visibility.Collapsed
                : (vm.Tab == "overdue" ? Visibility.Collapsed : Visibility.Visible);
            TodayTab.IsChecked = vm.Tab == "today";
            OverdueTab.IsChecked = vm.Tab == "overdue";
            OverdueTab.Visibility = vm.OverdueCards.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
using System.Windows;
using System.Windows.Controls;

namespace CourtDesk.UI.Views
{
    public partial class TodaySidebar : UserControl
    {
        public TodaySidebar()
        {
            InitializeComponent();
            DataContextChanged += delegate(object s, DependencyPropertyChangedEventArgs e) { Apply(); };
            Loaded += delegate(object s, RoutedEventArgs e) { Apply(); };
        }

        private void Apply()
        {
            var vm = DataContext as ViewModels.TodaySidebarViewModel;
            if (vm == null)
            {
                return;
            }
            vm.PropertyChanged += delegate(object s, System.ComponentModel.PropertyChangedEventArgs e)
            {
                if (e.PropertyName == "Empty" || e.PropertyName == "HasOverdue")
                {
                    ApplyBands(vm);
                }
            };
            ApplyBands(vm);
            Width = SystemParameters.PrimaryScreenWidth >= 1280 ? 380 : 340;
        }

        private void ApplyBands(ViewModels.TodaySidebarViewModel vm)
        {
            OverdueBlock.Visibility = vm.HasOverdue ? Visibility.Visible : Visibility.Collapsed;
            EmptyText.Visibility = vm.Empty ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}

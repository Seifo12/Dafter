using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CourtDesk.UI.ViewModels;

namespace CourtDesk.UI.Views
{
    public partial class HistoryView : UserControl
    {
        public HistoryView()
        {
            InitializeComponent();
        }

        private void GridDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var vm = DataContext as HistoryViewModel;
            if (vm == null)
            {
                return;
            }
            if (Grid.SelectedItem is HistoryRow)
            {
                vm.OpenCommand.Execute(Grid.SelectedItem);
            }
        }
    }
}
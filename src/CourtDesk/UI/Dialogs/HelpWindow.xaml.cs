using System.Windows;

namespace CourtDesk.UI.Dialogs
{
    public partial class HelpWindow : Window
    {
        public HelpWindow()
        {
            InitializeComponent();
        }

        private void CloseClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
using System.Windows;
using CourtDesk.UI.Controls;

namespace CourtDesk.UI.Dialogs
{
    public partial class AskCalendarWindow : Window
    {
        public AskCalendarWindow(string title, BigCalendar calendar)
        {
            InitializeComponent();
            TitleText.Text = title;
            Host.Content = calendar;
            OkButton.IsEnabled = calendar.Selected.HasValue;
            calendar.DateSelected += delegate { OkButton.IsEnabled = true; };
        }

        private void OkClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
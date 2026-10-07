using System.Windows;

namespace CourtDesk.UI.Dialogs
{
    public partial class MessageDialog : Window
    {
        public MessageDialog(string title, string body)
        {
            InitializeComponent();
            TitleText.Text = title;
            BodyText.Text = body;
            OkButton.Focus();
        }

        private void OkClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        public static void Show(Window owner, string title, string body)
        {
            var d = new MessageDialog(title, body);
            d.ShowDialog();
        }
    }
}
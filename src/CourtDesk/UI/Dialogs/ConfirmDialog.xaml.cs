using System.Windows;

namespace CourtDesk.UI.Dialogs
{
    public partial class ConfirmDialog : Window
    {
        public ConfirmDialog(string title, string body, string dangerLabel, string safeLabel)
        {
            InitializeComponent();
            TitleText.Text = title;
            BodyText.Text = body;
            DangerButton.Content = dangerLabel;
            PrimaryButton.Content = safeLabel;
            PrimaryButton.Focus();
        }

        private void DangerClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void PrimaryClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        public static bool Ask(Window owner, string title, string body, string dangerLabel, string safeLabel)
        {
            var d = new ConfirmDialog(title, body, dangerLabel, safeLabel);
            if (owner == null)
            {
                d.ShowDialog();
            }
            else
            {
                d.Owner = owner;
                d.ShowDialog();
            }
            return d.DialogResult == true;
        }
    }
}
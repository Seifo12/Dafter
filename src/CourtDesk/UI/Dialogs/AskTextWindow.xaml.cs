using System.Windows;

namespace CourtDesk.UI.Dialogs
{
    public partial class AskTextWindow : Window
    {
        public AskTextWindow(string title, string label)
        {
            InitializeComponent();
            TitleText.Text = title;
            LabelText.Text = label;
            Loaded += delegate { ValueBox.Focus(); };
        }

        public string Value
        {
            get { return ValueBox.Text == null ? null : ValueBox.Text.Trim(); }
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
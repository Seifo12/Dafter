using System.Globalization;
using System.Windows;
using CourtDesk.Application;
using CourtDesk.Resources;

namespace CourtDesk.UI.Dialogs
{
    public partial class ClockWarningDialog : Window
    {
        public ClockWarningDialog()
        {
            InitializeComponent();
        }

        public static void ShowFor(Window owner, ClockVerdict verdict)
        {
            var d = new ClockWarningDialog();
            d.BodyText.Text = string.Format(CultureInfo.InvariantCulture, Strings.ClockWarn,
                verdict.CurrentDate.ToIso(), verdict.MaxSeenUtc.ToString(
                    "yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture));
            d.Owner = owner;
            d.ShowDialog();
        }

        private void OkClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
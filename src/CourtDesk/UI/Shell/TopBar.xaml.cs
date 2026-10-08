using System.Windows;

namespace CourtDesk.UI.Shell
{
    public partial class TopBar : System.Windows.Controls.UserControl
    {
        public TopBar()
        {
            InitializeComponent();
        }

        public void ApplyWidthMode(bool wide)
        {
            NavRowWide.Visibility = wide ? Visibility.Visible : Visibility.Collapsed;
            NavRowCompact.Visibility = wide ? Visibility.Collapsed : Visibility.Visible;
            StationLineText.Visibility = wide ? Visibility.Visible : Visibility.Collapsed;
            FontLabelText.Visibility = wide ? Visibility.Visible : Visibility.Collapsed;
            HelpButton.MinWidth = wide ? 104 : 48;
            HelpButton.Content = wide ? (object)CourtDesk.Resources.Strings.NavHelp : "?";
            ExitButton.MinWidth = wide ? 104 : 48;
            ExitButton.Content = wide ? (object)CourtDesk.Resources.Strings.NavExit : "×";
        }
    }
}
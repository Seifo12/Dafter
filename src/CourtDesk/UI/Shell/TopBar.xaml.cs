using System.Windows.Controls;

namespace CourtDesk.UI.Shell
{
    public partial class TopBar : UserControl
    {
        public TopBar()
        {
            InitializeComponent();
        }

        public void ShowClockWarning(bool show)
        {
            _clockWarning = show;
        }

        private bool _clockWarning;
    }
}

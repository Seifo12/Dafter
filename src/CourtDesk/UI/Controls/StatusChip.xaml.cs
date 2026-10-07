using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CourtDesk.UI.Controls
{
    public partial class StatusChip : UserControl
    {
        public static readonly DependencyProperty StateKeyProperty =
            DependencyProperty.Register("StateKey", typeof(string), typeof(StatusChip),
                new PropertyMetadata("NO_DATE", OnStateKey));

        public static readonly DependencyProperty WordProperty =
            DependencyProperty.Register("Word", typeof(string), typeof(StatusChip),
                new PropertyMetadata("", OnWord));

        public string StateKey
        {
            get { return (string)GetValue(StateKeyProperty); }
            set { SetValue(StateKeyProperty, value); }
        }

        public string Word
        {
            get { return (string)GetValue(WordProperty); }
            set { SetValue(WordProperty, value); }
        }

        public StatusChip()
        {
            InitializeComponent();
            Apply();
        }

        private static void OnStateKey(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((StatusChip)d).Apply();
        }

        private static void OnWord(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((StatusChip)d).Apply();
        }

        private void Apply()
        {
            if (BackBorder == null)
            {
                return;
            }
            string key = StateKey ?? "NO_DATE";
            Brush bg;
            Brush accent;
            string icon;
            switch (key)
            {
                case "TODAY":
                    bg = CourtDesk.UI.UiRes.Brush(this, "TodayBgBrush", "#FFF1B8");
                    accent = CourtDesk.UI.UiRes.Brush(this, "TodayAccentBrush", "#8A5A00");
                    icon = "IconClock";
                    break;
                case "OVERDUE":
                    bg = CourtDesk.UI.UiRes.Brush(this, "OverdueBgBrush", "#FFD9D4");
                    accent = CourtDesk.UI.UiRes.Brush(this, "OverdueAccentBrush", "#B3261E");
                    icon = "IconAlert";
                    break;
                case "UPCOMING":
                    bg = CourtDesk.UI.UiRes.Brush(this, "UpcomingBgBrush", "#D6E8FA");
                    accent = CourtDesk.UI.UiRes.Brush(this, "UpcomingAccentBrush", "#0B5CAD");
                    icon = "IconArrow";
                    break;
                case "RELEASED":
                    bg = CourtDesk.UI.UiRes.Brush(this, "ReleasedBgBrush", "#D5EFDA");
                    accent = CourtDesk.UI.UiRes.Brush(this, "ReleasedAccentBrush", "#1B6E2D");
                    icon = "IconCheck";
                    break;
                case "JUDGED":
                    bg = CourtDesk.UI.UiRes.Brush(this, "JudgedBgBrush", "#E4DDF4");
                    accent = CourtDesk.UI.UiRes.Brush(this, "JudgedAccentBrush", "#5B3FA3");
                    icon = "IconScales";
                    break;
                default:
                    bg = CourtDesk.UI.UiRes.Brush(this, "NoDateBgBrush", "#EDEDED");
                    accent = CourtDesk.UI.UiRes.Brush(this, "NoDateAccentBrush", "#4A4A4A");
                    icon = "IconDashedCircle";
                    break;
            }
            BackBorder.Background = bg;
            BackBorder.BorderBrush = accent;
            WordText.Text = Word;
            WordText.Foreground = CourtDesk.UI.UiRes.Brush(this, "InkBrush", "#151515");
            GlyphPath.Data = CourtDesk.UI.UiRes.Geometry(this, icon);
            GlyphPath.Stroke = accent;
            if (key == "OVERDUE")
            {
                GlyphPath.Fill = accent;
            }
            else
            {
                GlyphPath.Fill = Brushes.Transparent;
            }
        }
    }
}

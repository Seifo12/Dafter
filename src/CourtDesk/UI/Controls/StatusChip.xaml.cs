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
            string bg;
            string fg;
            string icon;
            switch (key)
            {
                case "TODAY":
                    bg = "TodayBgBrush"; fg = "TodayFgBrush"; icon = "IconClock";
                    break;
                case "OVERDUE":
                    bg = "OverdueBgBrush"; fg = "OverdueFgBrush"; icon = "IconWarning";
                    break;
                case "UPCOMING":
                    bg = "UpcomingBgBrush"; fg = "UpcomingFgBrush"; icon = "IconClock";
                    break;
                case "RELEASED":
                    bg = "ReleasedBgBrush"; fg = "ReleasedFgBrush"; icon = "IconCheck";
                    break;
                case "JUDGED":
                    bg = "JudgedBgBrush"; fg = "JudgedFgBrush"; icon = "IconScales";
                    break;
                default:
                    bg = "NoDateBgBrush"; fg = "NoDateFgBrush"; icon = "IconCalendar";
                    break;
            }
            string fallbackBg = key == "TODAY" ? "#FFF1B8"
                : key == "OVERDUE" ? "#FFD9D4"
                : key == "UPCOMING" ? "#D6E8FA"
                : key == "RELEASED" ? "#D5EFDA"
                : key == "JUDGED" ? "#E4DDF4" : "#EDEDED";
            string fallbackFg = key == "TODAY" ? "#8A5A00"
                : key == "OVERDUE" ? "#A32D2D"
                : key == "UPCOMING" ? "#0B5CAD"
                : key == "RELEASED" ? "#1B6E2D"
                : key == "JUDGED" ? "#5B3FA3" : "#46505E";
            BackBorder.Background = CourtDesk.UI.UiRes.Brush(this, bg, fallbackBg);
            BackBorder.BorderBrush = CourtDesk.UI.UiRes.Brush(this, fg, fallbackFg);
            WordText.Text = Word;
            WordText.Foreground = CourtDesk.UI.UiRes.Brush(this, "InkBrush", "#1B2430");
            GlyphPath.Data = CourtDesk.UI.UiRes.Geometry(this, icon);
            GlyphPath.Stroke = CourtDesk.UI.UiRes.Brush(this, fg, fallbackFg);
            GlyphPath.Fill = Brushes.Transparent;
        }
    }
}
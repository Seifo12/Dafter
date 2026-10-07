using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CourtDesk.UI.Converters
{
    public sealed class StateToAccentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string key = value as string;
            if (key == "TODAY")
            {
                return App.Current.FindResource("TodayAccentBrush");
            }
            if (key == "OVERDUE")
            {
                return App.Current.FindResource("OverdueAccentBrush");
            }
            if (key == "UPCOMING")
            {
                return App.Current.FindResource("UpcomingAccentBrush");
            }
            if (key == "RELEASED")
            {
                return App.Current.FindResource("ReleasedAccentBrush");
            }
            if (key == "JUDGED")
            {
                return App.Current.FindResource("JudgedAccentBrush");
            }
            return App.Current.FindResource("NoDateAccentBrush");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}

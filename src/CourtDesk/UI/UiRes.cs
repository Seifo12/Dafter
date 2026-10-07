using System.Windows;
using System.Windows.Media;

namespace CourtDesk.UI
{
    public static class UiRes
    {
        public static object Find(FrameworkElement scope, string key)
        {
            object o = null;
            if (scope != null)
            {
                o = scope.TryFindResource(key);
            }
            if (o == null && System.Windows.Application.Current != null)
            {
                o = System.Windows.Application.Current.TryFindResource(key);
            }
            return o;
        }

        public static Brush Brush(FrameworkElement scope, string key, string fallback)
        {
            object o = Find(scope, key);
            Brush b = o as Brush;
            if (b != null)
            {
                return b;
            }
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallback));
        }

        public static Geometry Geometry(FrameworkElement scope, string key)
        {
            object o = Find(scope, key);
            return o as Geometry;
        }

        public static FontFamily Font(FrameworkElement scope)
        {
            object o = Find(scope, "AppFont");
            FontFamily f = o as FontFamily;
            return f ?? new FontFamily("Tahoma");
        }
    }
}
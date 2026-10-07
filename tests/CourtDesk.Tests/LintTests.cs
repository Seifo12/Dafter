using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace CourtDesk.Tests
{
    [Trait("Category", "UI")]
    public sealed class LintTests
    {
        private static string SrcRoot()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, "CourtDesk.sln")))
                {
                    return Path.Combine(dir, "src", "CourtDesk");
                }
                dir = Path.GetDirectoryName(dir);
            }
            throw new InvalidOperationException("repo root not found");
        }

        private static IEnumerable<string> XamlFiles()
        {
            foreach (string f in Directory.GetFiles(SrcRoot(), "*.xaml", SearchOption.AllDirectories))
            {
                if (f.IndexOf("\\obj\\", StringComparison.OrdinalIgnoreCase) >= 0
                    || f.IndexOf("\\bin\\", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }
                yield return f;
            }
        }

        [Fact]
        public void NoFontSizeBelow18()
        {
            var bad = new List<string>();
            foreach (string f in XamlFiles())
            {
                foreach (Match m in Regex.Matches(File.ReadAllText(f), "FontSize\\s*=\\s*\"(\\d+)\""))
                {
                    if (int.Parse(m.Groups[1].Value) < 18)
                    {
                        bad.Add(Path.GetFileName(f) + ": " + m.Value);
                    }
                }
            }
            Assert.True(bad.Count == 0, "small text: " + string.Join("; ", bad));
        }

        [Fact]
        public void EveryRootIsRtl()
        {
            var bad = new List<string>();
            foreach (string f in XamlFiles())
            {
                string text = File.ReadAllText(f);
                Match root = Regex.Match(text, "<(Window|UserControl)[\\s>]");
                if (!root.Success)
                {
                    continue;
                }
                int end = text.IndexOf('>', root.Index);
                string head = text.Substring(root.Index, end - root.Index);
                if (head.IndexOf("FlowDirection", StringComparison.Ordinal) < 0
                    || head.IndexOf("RightToLeft", StringComparison.Ordinal) < 0)
                {
                    bad.Add(f);
                }
            }
            Assert.True(bad.Count == 0, "non-RTL roots: " + string.Join("; ", bad));
        }

        [Fact]
        public void ButtonAndInputHeights()
        {
            string controls = File.ReadAllText(Path.Combine(SrcRoot(), "UI", "Styles", "Controls.xaml"));
            AssertMinHeight(controls, "BigButton", 56);
            AssertMinHeight(controls, "PrimaryButton", 64);
            AssertMinHeight(controls, "PrintButton", 72);
            AssertMinHeight(controls, "SmallButton", 48);
            AssertMinHeight(controls, "FilterChip", 48);
            AssertMinHeight(controls, "InputBox", 56);
        }

        private static void AssertMinHeight(string xaml, string key, int min)
        {
            Match style = Regex.Match(xaml, "x:Key=\"" + key + "\"[\\s\\S]*?MinHeight\" Value=\"(\\d+)\"");
            Assert.True(style.Success, "no MinHeight for " + key);
            Assert.True(int.Parse(style.Groups[1].Value) >= min, key + " too short");
        }

        [Fact]
        public void ContrastPairs()
        {
            var colors = new Dictionary<string, string>();
            string text = File.ReadAllText(Path.Combine(SrcRoot(), "UI", "Styles", "Colors.xaml"));
            foreach (Match m in Regex.Matches(text, "<Color x:Key=\"([^\"]+)\">(#......)</Color>"))
            {
                colors[m.Groups[1].Value] = m.Groups[2].Value;
            }
            AssertRatio(colors, "Ink", "Surface", 7.0);
            AssertRatio(colors, "InkMuted", "Surface", 7.0);
            AssertRatio(colors, "Surface", "Primary", 4.5);
            AssertRatio(colors, "Surface", "Danger", 4.5);
            AssertRatio(colors, "Border", "Surface", 4.5);
            AssertRatio(colors, "TodayAccent", "Surface", 3.0);
            AssertRatio(colors, "OverdueAccent", "Surface", 3.0);
            AssertRatio(colors, "UpcomingAccent", "Surface", 3.0);
            AssertRatio(colors, "ReleasedAccent", "Surface", 3.0);
            AssertRatio(colors, "JudgedAccent", "Surface", 3.0);
            AssertRatio(colors, "NoDateAccent", "Surface", 3.0);
        }

        private static void AssertRatio(Dictionary<string, string> colors, string fg, string bg, double min)
        {
            double r = Ratio(colors[fg], colors[bg]);
            Assert.True(r >= min, fg + " on " + bg + " = " + r.ToString("0.0") + " < " + min);
        }

        private static double Ratio(string a, string b)
        {
            double l1 = Luminance(a);
            double l2 = Luminance(b);
            if (l1 < l2)
            {
                double t = l1;
                l1 = l2;
                l2 = t;
            }
            return (l1 + 0.05) / (l2 + 0.05);
        }

        private static double Luminance(string hex)
        {
            double r = Component(hex.Substring(1, 2));
            double g = Component(hex.Substring(3, 2));
            double bl = Component(hex.Substring(5, 2));
            return 0.2126 * r + 0.7152 * g + 0.0722 * bl;
        }

        private static double Component(string hh)
        {
            double c = int.Parse(hh, System.Globalization.NumberStyles.HexNumber) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
    }
}

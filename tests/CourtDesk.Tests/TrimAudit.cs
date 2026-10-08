using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;
using Xunit;

namespace CourtDesk.Tests
{
    [Trait("Category", "UI")]
    public sealed class TrimAudit
    {
        [Fact]
        public void NoClippedTextOutsideWhitelist()
        {
            StaRunner.Run(delegate
            {
                var failures = new List<string>();
                CheckScenario(1366, 768, true, false, failures);
                CheckScenario(1024, 768, true, false, failures);
                CheckScenario(1366, 768, false, false, failures);
                CheckScenario(1366, 768, true, true, failures);
                CheckScenario(1024, 768, true, true, failures);
                Assert.True(failures.Count == 0, "TrimAudit failures:" + Environment.NewLine
                    + string.Join(Environment.NewLine, failures.ToArray()));
            });
        }

        private static void CheckScenario(int w, int h, bool data, bool zoom125, List<string> failures)
        {
            string tag = w + "x" + h + (data ? " full" : " empty") + (zoom125 ? " @125" : "");
            using (var env = new UiEnv())
            {
                if (data)
                {
                    SeedNasty(env);
                }
                env.Main.Register.Wide = true;
                if (zoom125)
                {
                    env.Main.ZoomInCommand.Execute(null);
                }
                env.Refresh();
                var window = new CourtDesk.MainWindow(env.Main);
                window.WindowState = WindowState.Normal;
                window.Width = w;
                window.Height = h;
                window.Left = -32000;
                window.Top = -32000;
                window.ShowActivated = false;
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Loaded,
                        new Action(delegate { }));
                    window.UpdateLayout();
                    var texts = new List<TextBlock>();
                    CollectTexts(window, texts);
                    foreach (TextBlock t in texts)
                    {
                        CheckBlock(tag, t, failures);
                    }
                }
                finally
                {
                    window.Close();
                }
            }
        }

        private static void SeedNasty(UiEnv env)
        {
            var svc = new DefendantService(env.Db, env.Defendants, env.Events,
                env.Clock, env.Settings, new NullLogger());
            for (int i = 0; i < 200; i++)
            {
                env.AddDefendant("test " + i, (9000 + i).ToString(),
                    new LocalDate(2026, 10, 7).AddDays(i % 10 - 2));
            }
            svc.Create(new DefendantInput
            {
                FullName = "defendant with a very long name exceeding any fixed column",
                CaseNumber = "999991",
                CaseYear = "2026",
                Charges = "charge text that is deliberately very long so wrapping can be observed in the wide grid column without any trimming",
                SessionDate = new LocalDate(2026, 10, 7)
            }, Guid.NewGuid().ToString("N"), true);
            svc.Create(new DefendantInput
            {
                FullName = "overdue with a very long name exceeding any fixed column width",
                CaseNumber = "999992",
                CaseYear = "2026",
                Charges = "another deliberately long charge text for the wide column wrap check",
                SessionDate = new LocalDate(2026, 10, 1)
            }, Guid.NewGuid().ToString("N"), true);
        }

        private static void CollectTexts(DependencyObject root, List<TextBlock> outp)
        {
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                DependencyObject c = VisualTreeHelper.GetChild(root, i);
                TextBlock t = c as TextBlock;
                if (t != null)
                {
                    outp.Add(t);
                }
                CollectTexts(c, outp);
            }
        }

        private static void CheckBlock(string tag, TextBlock t, List<string> failures)
        {
            if (!t.IsVisible)
            {
                return;
            }
            string text = t.Text;
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            if (t.Tag as string == "EllipsisAllowed")
            {
                return;
            }
            if (t.TextWrapping != TextWrapping.NoWrap)
            {
                return;
            }
            if (t.ActualWidth < 1.0)
            {
                return;
            }
            var face = new Typeface(t.FontFamily, t.FontStyle, t.FontWeight, t.FontStretch);
            var ft = new FormattedText(text, CultureInfo.InvariantCulture,
                t.FlowDirection, face, t.FontSize, Brushes.Black);
            if (ft.Width > t.ActualWidth + 3.0)
            {
                failures.Add(tag + ": '" + Short(text) + "' needs "
                    + Math.Round(ft.Width) + "px in " + Math.Round(t.ActualWidth)
                    + "px at " + Chain(t));
            }
        }

        private static string Short(string s)
        {
            s = s.Replace("\r", " ").Replace("\n", " ");
            return s.Length > 28 ? s.Substring(0, 28) : s;
        }

        private static string Chain(DependencyObject o)
        {
            string c = "";
            int hops = 0;
            while (o != null && hops < 5)
            {
                FrameworkElement f = o as FrameworkElement;
                if (f != null)
                {
                    string nm = string.IsNullOrEmpty(f.Name) ? f.GetType().Name
                        : f.GetType().Name + "#" + f.Name;
                    c = c == "" ? nm : nm + ">" + c;
                }
                o = VisualTreeHelper.GetParent(o);
                hops++;
            }
            return c;
        }
    }
}

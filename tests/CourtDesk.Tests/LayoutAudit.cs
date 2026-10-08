using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;
using CourtDesk.Persistence;
using Xunit;

namespace CourtDesk.Tests
{
    internal sealed class AuditClock : IClock
    {
        public LocalDate Today { get { return new LocalDate(2026, 10, 7); } }
        public DateTime UtcNow { get { return new DateTime(2026, 10, 7, 8, 0, 0, DateTimeKind.Utc); } }
    }

    [Trait("Category", "LayoutAudit")]
    public sealed class LayoutAudit
    {
        private static string OutDir()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, "CourtDesk.sln")))
                {
                    string d = Path.Combine(dir, "artifacts", "audit");
                    Directory.CreateDirectory(d);
                    return d;
                }
                dir = Path.GetDirectoryName(dir);
            }
            throw new InvalidOperationException("repo root not found");
        }

        private sealed class Ctx
        {
            public CourtDesk.MainWindow Window;
            public string Name;
            public string Scenario;
            public int Width;
            public int Height;
            public int Scale;
            public bool HasData;
        }

        private static readonly int[][] Sizes = new int[][]
        {
            new int[] { 1366, 768 },
            new int[] { 1280, 720 },
            new int[] { 1024, 768 }
        };

        [Fact]
        public void Matrix_AllTwelveScenarios_PassLayoutRules()
        {
            StaRunner.Run(delegate
            {
                List<string> failures = new List<string>();
                foreach (int[] size in Sizes)
                {
                    foreach (int scale in new int[] { 100, 125 })
                    {
                        foreach (bool data in new bool[] { false, true })
                        {
                            Ctx ctx = Build(size[0], size[1], scale, data);
                            try
                            {
                                Check(ctx, failures);
                            }
                            finally
                            {
                                Close(ctx);
                            }
                        }
                    }
                }
                Assert.True(failures.Count == 0, "LayoutAudit failures:" + Environment.NewLine
                    + string.Join(Environment.NewLine, failures.ToArray()));
            });
        }

        private static void Check(Ctx c, List<string> failures)
        {
            Window w = c.Window;

            if (w.ActualWidth + 0.5 < c.Width || w.ActualHeight + 0.5 < c.Height)
            {
                failures.Add(c.Scenario + ": window shrank below min size "
                    + w.ActualWidth + "x" + w.ActualHeight);
            }

            var scrolls = new List<ScrollViewer>();
            var buttons = new List<FrameworkElement>();
            var texts = new List<TextBlock>();
            var grids = new List<DataGrid>();
            Collect(w, scrolls, buttons, texts, grids);

            foreach (ScrollViewer sv in scrolls)
            {
                if (Name(sv) == "DecidedStrip")
                {
                    continue;
                }
                if (sv.ScrollableWidth > 1.0)
                {
                    failures.Add(c.Scenario + ": horizontal overflow of "
                        + Math.Round(sv.ScrollableWidth, 1) + "px on '" + Name(sv) + "'");
                }
            }

            foreach (FrameworkElement b in buttons)
            {
                string bn = Name(b);
                if (bn.StartsWith("PART_", StringComparison.Ordinal)
                    || bn == "RepeatButton" || bn == "ScrollBar")
                {
                    continue;
                }
                if (!IsVisible(b))
                {
                    continue;
                }
                double minW = Min(b, FrameworkElement.MinWidthProperty);
                double minH = Min(b, FrameworkElement.MinHeightProperty);
                if (b.ActualWidth + 0.75 < minW || b.ActualHeight + 0.75 < minH)
                {
                    failures.Add(c.Scenario + ": clipped control '" + Name(b) + "' actual "
                        + Math.Round(b.ActualWidth) + "x" + Math.Round(b.ActualHeight)
                        + " < min " + Math.Round(minW) + "x" + Math.Round(minH));
                }
                if (b.ActualHeight + 0.75 < 40.0 && Name(b) != "WideToggle")
                {
                    failures.Add(c.Scenario + ": target height " + Math.Round(b.ActualHeight)
                        + " < 40 on '" + Name(b) + "'");
                }
            }

            foreach (TextBlock t in texts)
            {
                if (!IsVisible(t))
                {
                    continue;
                }
                if (t.TextTrimming == TextTrimming.None
                    && t.TextWrapping == TextWrapping.NoWrap
                    && t.Text != ""
                    && t.Text != null
                    && t.ActualWidth + t.Margin.Left + t.Margin.Right + 3.0 < t.DesiredSize.Width)
                {
                    string chain = t.Name + "/" + t.FontSize.ToString("0")
                        + " act=" + Math.Round(t.ActualWidth) + " des=" + Math.Round(t.DesiredSize.Width)
                        + " pad=" + Math.Round(t.Margin.Left) + "," + Math.Round(t.Margin.Right);
                    DependencyObject up = VisualTreeHelper.GetParent(t);
                    int hops = 0;
                    while (up != null && hops < 6)
                    {
                        var ufe = up as FrameworkElement;
                        if (ufe != null)
                        {
                            chain += " > " + Name(ufe) + "(" + ufe.GetType().Name + ")"
                                + "[" + Math.Round(ufe.ActualWidth) + "/" + Math.Round(ufe.DesiredSize.Width) + "]";
                        }
                        up = VisualTreeHelper.GetParent(up);
                        hops++;
                    }
                    failures.Add(c.Scenario + ": text clipped '" + t.Text + "' :: " + chain);
                }
            }

            if (Environment.GetEnvironmentVariable("COURTDESK_AUDIT_DEBUG") == "1")
            {
                FrameworkElement bar = Find(w, "ActionBar");
                if (bar != null)
                {
                    string dump = c.Scenario + " actionbar=" + Math.Round(bar.ActualWidth)
                        + " desired=" + Math.Round(bar.DesiredSize.Width);
                    var sp = bar as Panel;
                    if (sp != null)
                    {
                        foreach (UIElement child in sp.Children)
                        {
                            var fe = child as FrameworkElement;
                            dump += " | " + Name(fe) + " " + Math.Round(fe.ActualWidth)
                                + "/" + Math.Round(fe.DesiredSize.Width);
                        }
                    }
                    failures.Add(dump);
                }
            }

            foreach (DataGrid g in grids)
            {
                double total = 0;
                foreach (DataGridColumn col in g.Columns)
                {
                    total += col.ActualWidth;
                }
                double avail = g.ActualWidth - 20;
                if (total > avail + 2.0)
                {
                    failures.Add(c.Scenario + ": grid columns need " + Math.Round(total)
                        + "px but only " + Math.Round(avail) + "px available");
                }
                for (int i = 0; i < g.Items.Count && i < 12; i++)
                {
                    var row = g.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                    if (row == null || !IsVisible(row))
                    {
                        continue;
                    }
                    foreach (DependencyObject d in Flatten(row))
                    {
                        var cellEl = d as DataGridCell;
                        if (cellEl == null || !IsVisible(cellEl))
                        {
                            continue;
                        }
                        foreach (DependencyObject inner in Flatten(cellEl))
                        {
                            var t2 = inner as TextBlock;
                            if (t2 == null || string.IsNullOrEmpty(t2.Text))
                            {
                                continue;
                            }
                            if (t2.ActualWidth > cellEl.ActualWidth + 1.0)
                            {
                                failures.Add(c.Scenario + ": cell text '" + Trim(t2.Text) + "' "
                                    + Math.Round(t2.ActualWidth) + "px inside "
                                    + Math.Round(cellEl.ActualWidth) + "px cell");
                            }
                        }
                    }
                }
            }

            FrameworkElement status = Find(w, "StatusRow");
            if (status != null && Math.Abs(status.ActualHeight - 36.0) > 1.5)
            {
                failures.Add(c.Scenario + ": status bar height " + Math.Round(status.ActualHeight) + " != 36");
            }

            FrameworkElement card = Find(w, "CommandCard");
            if (card != null && Math.Abs(card.ActualHeight - 124.0) > 1.5)
            {
                failures.Add(c.Scenario + ": command card height " + Math.Round(card.ActualHeight) + " != 124");
            }

            if (c.HasData)
            {
                bool strict = c.Width >= 1366 && c.Scale == 100;
                foreach (DataGrid g in grids)
                {
                    int realized = 0;
                    for (int i = 0; i < g.Items.Count && i < 40; i++)
                    {
                        if (g.ItemContainerGenerator.ContainerFromIndex(i) != null)
                        {
                            realized++;
                        }
                    }
                    if (realized < (strict ? 6 : 3))
                    {
                        failures.Add(c.Scenario + ": grid shows only " + realized + " rows");
                    }
                }
                int cards = CountSidebarCards(w);
                if (c.Width >= 1366 && cards < 4)
                {
                    failures.Add(c.Scenario + ": sidebar shows only " + cards + " cards");
                }
                if (cards == 0)
                {
                    failures.Add(c.Scenario + ": sidebar shows no cards");
                }
            }

            Save(c);
        }

        private static int CountSidebarCards(Window w)
        {
            int total = 0;
            var list = new List<DependencyObject>();
            CollectAll(w, list);
            foreach (DependencyObject d in list)
            {
                var ic = d as ItemsControl;
                if (ic == null || ic.ItemsSource == null || !IsVisible(ic))
                {
                    continue;
                }
                if (ic.Name == "PendingList" || ic.Name == "OverdueList")
                {
                    int shown = 0;
                    foreach (object item in ic.Items)
                    {
                        if (shown >= 6)
                        {
                            break;
                        }
                        shown++;
                    }
                    total += shown;
                }
            }
            return total;
        }

        private static void Save(Ctx c)
        {
            try
            {
                var bmp = new RenderTargetBitmap(c.Width, c.Height, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(c.Window);
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bmp));
                using (var fs = new FileStream(Path.Combine(OutDir(), c.Name + ".png"), FileMode.Create))
                {
                    enc.Save(fs);
                }
            }
            catch (Exception)
            {
            }
        }

        private static void Close(Ctx c)
        {
            try
            {
                if (c.Window != null)
                {
                    c.Window.Close();
                }
            }
            catch (Exception)
            {
            }
        }

        private static double Min(FrameworkElement e, DependencyProperty p)
        {
            object v = e.GetValue(p);
            return v is double ? (double)v : 0.0;
        }

        private static string Name(FrameworkElement e)
        {
            return string.IsNullOrEmpty(e.Name) ? e.GetType().Name : e.Name;
        }

        private static bool IsVisible(FrameworkElement e)
        {
            return e.IsVisible && e.ActualWidth > 0 && e.ActualHeight > 0
                && e.Visibility == Visibility.Visible;
        }

        private static FrameworkElement Find(DependencyObject root, string name)
        {
            var all = new List<DependencyObject>();
            CollectAll(root, all);
            foreach (DependencyObject d in all)
            {
                var fe = d as FrameworkElement;
                if (fe != null && fe.Name == name)
                {
                    return fe;
                }
            }
            return null;
        }

        private static void Collect(DependencyObject root, List<ScrollViewer> scrolls,
            List<FrameworkElement> buttons, List<TextBlock> texts, List<DataGrid> grids)
        {
            var stack = new Stack<DependencyObject>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                DependencyObject cur = stack.Pop();
                var sv = cur as ScrollViewer;
                if (sv != null)
                {
                    scrolls.Add(sv);
                }
                if (cur is ButtonBase || cur is TextBox || cur is ComboBox)
                {
                    buttons.Add(cur as FrameworkElement);
                }
                var tb = cur as TextBlock;
                if (tb != null)
                {
                    texts.Add(tb);
                }
                var dg = cur as DataGrid;
                if (dg != null)
                {
                    grids.Add(dg);
                }
                int n = VisualTreeHelper.GetChildrenCount(cur);
                for (int i = 0; i < n; i++)
                {
                    stack.Push(VisualTreeHelper.GetChild(cur, i));
                }
            }
        }

        private static string Trim(string s)
        {
            if (s == null)
            {
                return "";
            }
            s = s.Replace("\r", " ").Replace("\n", " ");
            return s.Length > 24 ? s.Substring(0, 24) : s;
        }

        private static List<DependencyObject> Flatten(DependencyObject root)
        {
            var all = new List<DependencyObject>();
            CollectAll(root, all);
            return all;
        }

        private static void CollectAll(DependencyObject root, List<DependencyObject> all)
        {
            var stack = new Stack<DependencyObject>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                DependencyObject cur = stack.Pop();
                all.Add(cur);
                int n = VisualTreeHelper.GetChildrenCount(cur);
                for (int i = 0; i < n; i++)
                {
                    stack.Push(VisualTreeHelper.GetChild(cur, i));
                }
            }
        }

        private static Ctx Build(int w, int h, int scale, bool data)
        {
            string home = TestDb.NewHome();
            var clock = new AuditClock();
            RecoveryResult rec = TestDb.Open(home, clock);
            DbGateway db = rec.Gateway;
            var defendants = new SqliteDefendantStore();
            var events = new SqliteEventStore();
            var settings = new SettingsService(db, new SqliteSettingsStore());
            db.Write(s =>
            {
                SeedData.SeedLookups((DbContext)s,
                    CourtDesk.Resources.Strings.SeedCourts,
                    CourtDesk.Resources.Strings.SeedCustody,
                    CourtDesk.Resources.Strings.SeedReasons);
                return 0;
            });
            var svc = new DefendantService(db, defendants, events, clock, settings, new NullLogger());
            if (data)
            {
                Seed(200, svc);
            }
            var index = new SearchIndex(db, defendants, clock, new string[]
            {
                CourtDesk.Resources.Strings.StatusDetained,
                CourtDesk.Resources.Strings.StatusReleased,
                CourtDesk.Resources.Strings.StatusJudged
            });
            index.Rebuild();
            var today = new TodayService(db, defendants, clock, settings);
            var register = new CourtDesk.UI.ViewModels.RegisterViewModel(index, today,
                delegate { return settings.Get(SettingKeys.LastFilter); },
                delegate(string v) { settings.Set(SettingKeys.LastFilter, v); });
            var sidebar = new CourtDesk.UI.ViewModels.TodaySidebarViewModel(today);
            settings.Set(SettingKeys.FontScale, scale.ToString());
            var main = new CourtDesk.UI.ViewModels.MainViewModel(settings, clock, register, sidebar);
            main.RefreshAll();

            var window = new CourtDesk.MainWindow(main);
            window.WindowState = WindowState.Normal;
            window.Width = w;
            window.Height = h;
            window.Left = -32000;
            window.Top = -32000;
            window.ShowActivated = false;
            window.Show();
            window.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Loaded, new Action(delegate { }));
            window.UpdateLayout();

            var ctx = new Ctx
            {
                Window = window,
                Width = w,
                Height = h,
                Scale = scale,
                HasData = data,
                Name = string.Format("audit-{0}x{1}-s{2}-{3}", w, h, scale, data ? "200" : "empty"),
                Scenario = string.Format("{0}x{1}@{2}% {3}", w, h, scale, data ? "200 rows" : "empty")
            };
            try
            {
                db.Close();
                TestDb.Wipe(home);
            }
            catch (Exception)
            {
            }
            return ctx;
        }

        private static void Seed(int total, DefendantService svc)
        {
            string[] first = { "محمد عبد الله السيد", "السيد حسن علي علي", "كريم فؤاد إبراهيم",
                "طارق محمود حسن", "عبد الرحمن شاهين", "مصطفى عبد الحميد شريف",
                "أحمد جمال الدين زكي", "إسلام رجب توفيق", "هشام أنور صادق",
                "ياسر حمدي عبد الجواد" };
            LocalDate today = new LocalDate(2026, 10, 7);
            for (int i = 0; i < total; i++)
            {
                LocalDate? date;
                if (i < 20)
                {
                    date = today;
                }
                else if (i < 60)
                {
                    date = new LocalDate(2026, 10, 1);
                }
                else if (i < 140)
                {
                    date = new LocalDate(2026, 10, 20);
                }
                else
                {
                    date = null;
                }
                svc.Create(new DefendantInput
                {
                    FullName = first[i % first.Length] + " " + (100 + i),
                    CaseNumber = (1000 + i).ToString(),
                    CaseYear = "2026",
                    SessionDate = date
                }, Guid.NewGuid().ToString("N"), true);
            }
        }
    }
}
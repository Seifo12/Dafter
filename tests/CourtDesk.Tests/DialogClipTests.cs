using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CourtDesk.Domain;
using CourtDesk.UI.Dialogs;
using CourtDesk.UI.ViewModels;
using Xunit;

namespace CourtDesk.Tests
{
    [Trait("Category", "UI")]
    public sealed class DialogClipTests
    {
        [Fact]
        public void DialogContentInsideBoundsAndButtonsHittable()
        {
            StaRunner.Run(delegate
            {
                var failures = new List<string>();
                using (var env = new DialogEnv())
                {
                    int id = env.Add("clip check", "6101", new LocalDate(2026, 10, 7));
                    env.Refresh();
                    var wiz = OpenWizard(env, id);
                    try
                    {
                        Show(wiz);
                        Check("wizard-step1", wiz, failures);
                        wiz.ViewModel.Choose(DecisionCodes.RenewDetention);
                        Pump();
                        Check("wizard-step2", wiz, failures);
                        wiz.ViewModel.PickOffset(7);
                        wiz.ViewModel.NextCommand.Execute(null);
                        Pump();
                        Check("wizard-step3", wiz, failures);
                    }
                    finally
                    {
                        wiz.Close();
                    }
                    CheckShow("edit", OpenEdit(env), failures);
                    CheckShow("card", new DefendantCardDialog(
                        env.DefendantService, env.Undo, env.Lookups, id), failures);
                    CheckShow("confirm", new ConfirmDialog("t", "b", "d", "s"), failures);
                    CheckShow("message", new MessageDialog("t", "b"), failures);
                    CheckShow("clock", new ClockWarningDialog(), failures);
                    CheckShow("help", new HelpWindow(), failures);
                    CheckShow("firstrun", new FirstRunDialog(env.Settings), failures);
                    CheckShow("asktext", new AskTextWindow("t", "l"), failures);
                }
                Assert.True(failures.Count == 0, "DialogClip failures:" + Environment.NewLine
                    + string.Join(Environment.NewLine, failures.ToArray()));
            });
        }

        private static DecisionWizardDialog OpenWizard(DialogEnv env, int id)
        {
            var vm = new DecisionWizardViewModel(env.DecisionService, env.Undo,
                env.DefendantService, env.Lookups, env.Settings, env.Clock,
                new List<int> { id }, 0);
            return new DecisionWizardDialog(vm, null, env.Clock);
        }

        private static DefendantEditDialog OpenEdit(DialogEnv env)
        {
            var vm = new DefendantEditViewModel(env.DefendantService, env.Lookups, env.Clock, null);
            return new DefendantEditDialog(vm, env.Lookups, env.Clock, null);
        }

        private static void CheckShow(string tag, Window w, List<string> failures)
        {
            try
            {
                Show(w);
                Check(tag, w, failures);
            }
            finally
            {
                w.Close();
            }
        }

        private static void Show(Window w)
        {
            w.WindowState = WindowState.Normal;
            w.Left = -32000;
            w.Top = -32000;
            w.ShowActivated = false;
            w.Show();
            Pump();
            w.UpdateLayout();
            Pump();
        }

        private static void Pump()
        {
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Loaded,
                new Action(delegate { }));
        }

        private static void Check(string tag, Window w, List<string> failures)
        {
            if (w.Width > 1024 + 0.5 || w.Height > 768 + 0.5)
            {
                failures.Add(tag + ": dialog " + Math.Round(w.Width) + "x"
                    + Math.Round(w.Height) + " exceeds 1024x768");
            }
            FrameworkElement root = w.Content as FrameworkElement;
            if (root == null)
            {
                failures.Add(tag + ": dialog has no content root");
                return;
            }
            var bounds = new Rect(0, 0, root.ActualWidth, root.ActualHeight);
            var all = new List<DependencyObject>();
            Flatten(root, all);
            foreach (DependencyObject o in all)
            {
                FrameworkElement f = o as FrameworkElement;
                if (f == null || !f.IsVisible)
                {
                    continue;
                }
                if (InsideScrollContent(f))
                {
                    continue;
                }
                Button b = o as Button;
                if (b != null)
                {
                    if (b.ActualWidth < 1.0 || b.ActualHeight < 1.0)
                    {
                        continue;
                    }
                    Point center = b.TranslatePoint(
                        new Point(b.ActualWidth / 2.0, b.ActualHeight / 2.0), root);
                    if (!bounds.Contains(center))
                    {
                        failures.Add(tag + ": button center outside dialog at " + Chain(b));
                        continue;
                    }
                    HitTestResult hit = VisualTreeHelper.HitTest(root, center);
                    if (hit == null || !IsSelfOrChild(b, hit.VisualHit))
                    {
                        failures.Add(tag + ": button center not hittable at " + Chain(b));
                    }
                    continue;
                }
                if (InsideScrollContent(f))
                {
                    continue;
                }
                if (f.ActualWidth < 1.0 || f.ActualHeight < 1.0)
                {
                    continue;
                }
                Point topLeft = f.TranslatePoint(new Point(0, 0), root);
                var r = new Rect(topLeft, new Size(f.ActualWidth, f.ActualHeight));
                if (r.Left < -1.0 || r.Top < -1.0
                    || r.Right > bounds.Width + 1.0 || r.Bottom > bounds.Height + 1.0)
                {
                    failures.Add(tag + ": element outside dialog "
                        + Math.Round(r.Left) + "," + Math.Round(r.Top) + " "
                        + Math.Round(r.Width) + "x" + Math.Round(r.Height)
                        + " at " + Chain(f));
                }
            }
        }

        private static bool InsideScrollContent(DependencyObject o)
        {
            DependencyObject at = VisualTreeHelper.GetParent(o);
            while (at != null)
            {
                if (at is ScrollViewer || at is ScrollContentPresenter)
                {
                    return true;
                }
                at = VisualTreeHelper.GetParent(at);
            }
            return false;
        }

        private static bool IsSelfOrChild(DependencyObject root, DependencyObject hit)
        {
            DependencyObject at = hit;
            while (at != null)
            {
                if (object.ReferenceEquals(at, root))
                {
                    return true;
                }
                at = VisualTreeHelper.GetParent(at);
            }
            return false;
        }

        private static void Flatten(DependencyObject root, List<DependencyObject> outp)
        {
            var stack = new Stack<DependencyObject>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                DependencyObject cur = stack.Pop();
                outp.Add(cur);
                int n = VisualTreeHelper.GetChildrenCount(cur);
                for (int i = 0; i < n; i++)
                {
                    stack.Push(VisualTreeHelper.GetChild(cur, i));
                }
            }
        }

        private static string Chain(DependencyObject o)
        {
            string c = "";
            int hops = 0;
            while (o != null && hops < 4)
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

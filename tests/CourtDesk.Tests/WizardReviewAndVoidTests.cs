using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CourtDesk.Domain;
using CourtDesk.UI.Dialogs;
using Xunit;

namespace CourtDesk.Tests
{
    [Trait("Category", "UI")]
    public sealed class WizardReviewAndVoidTests
    {
        [Fact]
        public void ReviewStepShowsItems()
        {
            StaRunner.Run(delegate
            {
                using (var env = new DialogEnv())
                {
                    int id = env.Add("مراجعة", "6501", new LocalDate(2026, 10, 7));
                    var vm = new CourtDesk.UI.ViewModels.DecisionWizardViewModel(
                        env.DecisionService, env.Undo, env.DefendantService, env.Lookups,
                        env.Settings, env.Clock, new List<int> { id }, 0);
                    vm.Choose(DecisionCodes.RenewDetention);
                    vm.PickOffset(7);
                    vm.NextCommand.Execute(null);
                    Assert.True(vm.AtStep3, "not on step 3");
                    Assert.True(vm.Review.Count >= 2,
                        "review items: " + vm.Review.Count);
                    var dlg = new DecisionWizardDialog(vm, new PopupHost(), env.Clock);
                    dlg.Left = -32000;
                    dlg.Top = -32000;
                    dlg.ShowActivated = false;
                    try
                    {
                        dlg.Show();
                        Pump();
                        dlg.UpdateLayout();
                        Pump();
                        dlg.UpdateLayout();
                        var boxes = new List<TextBlock>();
                        Collect(dlg, boxes);
                        int shown = 0;
                        foreach (TextBlock t in boxes)
                        {
                            if (t.Text == vm.ReviewSentence && t.IsVisible)
                            {
                                shown++;
                            }
                        }
                        Assert.True(shown >= 1, "review sentence not visible");
                        var items = dlg.FindName("ReviewItems") as ItemsControl;
                        Assert.True(items != null, "no ReviewItems");
                        Assert.True(items.Items.Count == vm.Review.Count,
                            "shown " + items.Items.Count + " of " + vm.Review.Count);
                        int rendered = 0;
                        var texts = new List<TextBlock>();
                        Collect(items, texts);
                        foreach (TextBlock t in texts)
                        {
                            if (!string.IsNullOrEmpty(t.Text) && t.IsVisible
                                && t.ActualWidth > 1)
                            {
                                rendered++;
                            }
                        }
                        Assert.True(rendered >= vm.Review.Count,
                            "rendered " + rendered + " of " + vm.Review.Count);
                    }
                    finally
                    {
                        dlg.Close();
                    }
                }
            });
        }

        private static void Pump()
        {
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Loaded,
                new Action(delegate { }));
        }

        private static void Collect(DependencyObject root, List<TextBlock> outp)
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
                Collect(c, outp);
            }
        }
    }
}
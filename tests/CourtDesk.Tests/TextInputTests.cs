using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Xunit;

namespace CourtDesk.Tests
{
    [Trait("Category", "UI")]
    public sealed class TextInputTests
    {
        [Fact]
        public void NameFieldAcceptsTyping()
        {
            StaRunner.Run(delegate
            {
                using (var env = new DialogEnv())
                {
                    var vm = new CourtDesk.UI.ViewModels.DefendantEditViewModel(
                        env.DefendantService, env.Lookups, env.Clock, null);
                    var dlg = new CourtDesk.UI.Dialogs.DefendantEditDialog(
                        vm, env.Lookups, env.Clock, new CourtDesk.UI.Dialogs.PopupHost());
                    dlg.Left = -32000;
                    dlg.Top = -32000;
                    dlg.ShowActivated = false;
                    try
                    {
                        dlg.Show();
                        Pump();
                        dlg.UpdateLayout();
                        Pump();
                        var box = dlg.FindName("NameBox") as TextBox;
                        Assert.True(box != null, "no NameBox");
                        Assert.True(box.IsEnabled, "name box disabled");
                        Assert.True(box.Focusable, "name box not focusable");
                        box.Focus();
                        box.Text = "محمد عبد الله";
                        Pump();
                        Assert.Equal("محمد عبد الله", box.Text);
                        Assert.Equal("محمد عبد الله", vm.Name);
                    }
                    finally
                    {
                        dlg.Close();
                    }
                }
            });
        }

        [Fact]
        public void EditDialogInputsAreEditable()
        {
            StaRunner.Run(delegate
            {
                using (var env = new DialogEnv())
                {
                    var vm = new CourtDesk.UI.ViewModels.DefendantEditViewModel(
                        env.DefendantService, env.Lookups, env.Clock, null);
                    var dlg = new CourtDesk.UI.Dialogs.DefendantEditDialog(
                        vm, env.Lookups, env.Clock, new CourtDesk.UI.Dialogs.PopupHost());
                    dlg.Left = -32000;
                    dlg.Top = -32000;
                    dlg.ShowActivated = false;
                    try
                    {
                        dlg.Show();
                        Pump();
                        dlg.UpdateLayout();
                        foreach (string name in new string[]
                        {
                            "NameBox", "NationalIdBox", "CaseNumberBox", "CaseYearBox"
                        })
                        {
                            var b = dlg.FindName(name) as TextBox;
                            Assert.True(b != null, "missing " + name);
                            Assert.False(b.IsReadOnly, name + " is read-only");
                            Assert.True(b.IsEnabled, name + " disabled");
                        }
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
                new System.Action(delegate { }));
        }
    }
}
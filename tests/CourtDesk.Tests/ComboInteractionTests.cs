using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Xunit;

namespace CourtDesk.Tests
{
    [Trait("Category", "UI")]
    public sealed class ComboInteractionTests
    {
        [Fact]
        public void DropdownOpensAndSelectsItem()
        {
            StaRunner.Run(delegate
            {
                var combo = new ComboBox { Style = (Style)Find("BigCombo") };
                combo.ItemsSource = new string[] { "one", "two", "three" };
                combo.MinHeight = 56;
                combo.Width = 300;
                combo.Height = 56;
                var host = new Window
                {
                    Width = 500,
                    Height = 400,
                    Left = -32000,
                    Top = -32000,
                    ShowActivated = false,
                    FlowDirection = FlowDirection.RightToLeft,
                    Content = combo
                };
                try
                {
                    host.Show();
                    Pump();
                    host.UpdateLayout();
                    Assert.True(combo.IsEnabled, "combo disabled");
                    var toggle = FindToggle(combo);
                    Assert.True(toggle != null, "no toggle button in combo template");
                    combo.IsDropDownOpen = true;
                    Pump();
                    host.UpdateLayout();
                    Assert.True(combo.IsDropDownOpen, "dropdown refused to open");
                    Assert.Equal(3, combo.Items.Count);
                    combo.SelectedIndex = 1;
                    combo.IsDropDownOpen = false;
                    Pump();
                    Assert.Equal("two", combo.Text);
                    Assert.True(combo.ItemsPanel != null, "no items panel realised");
                    var containers = combo.ItemContainerGenerator;
                    Assert.NotNull(containers);
                    DependencyObject c0 = containers.ContainerFromIndex(0);
                    Assert.True(c0 != null, "item container 0 not generated");
                }
                finally
                {
                    host.Close();
                }
            });
        }

        private static object Find(string key)
        {
            return System.Windows.Application.Current.TryFindResource(key);
        }

        private static void Pump()
        {
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Loaded,
                new Action(delegate { }));
        }

        private static ToggleButton FindToggle(DependencyObject root)
        {
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                DependencyObject c = VisualTreeHelper.GetChild(root, i);
                ToggleButton t = c as ToggleButton;
                if (t != null)
                {
                    return t;
                }
                ToggleButton deep = FindToggle(c);
                if (deep != null)
                {
                    return deep;
                }
            }
            return null;
        }

        private static void CollectItems(DependencyObject root,
            System.Collections.Generic.List<ComboBoxItem> outp)
        {
            var items = new System.Collections.Generic.List<ComboBoxItem>();
            var stack = new System.Collections.Generic.Stack<DependencyObject>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                DependencyObject cur = stack.Pop();
                ComboBoxItem ci = cur as ComboBoxItem;
                if (ci != null)
                {
                    items.Add(ci);
                }
                int n = VisualTreeHelper.GetChildrenCount(cur);
                for (int i = 0; i < n; i++)
                {
                    stack.Push(VisualTreeHelper.GetChild(cur, i));
                }
            }
            if (items.Count > 0)
            {
                outp.AddRange(items);
                return;
            }
            var cb = root as ComboBox;
            if (cb == null)
            {
                return;
            }
            foreach (object o in cb.Items)
            {
                outp.Add(new ComboBoxItem { Content = o });
            }
        }
    }
}
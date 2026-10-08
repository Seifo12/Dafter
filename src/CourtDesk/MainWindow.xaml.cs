using System.Windows;
using System.Windows.Controls;
using CourtDesk.UI.Views;
using CourtDesk.UI.ViewModels;

namespace CourtDesk
{
    public partial class MainWindow : Window
    {
        private MainViewModel _vm;
        private ScheduleView _schedule;
        private HistoryView _history;
        private FrameworkElement _current;
        private double _zoom = 1.0;

        public MainWindow(MainViewModel vm) : this(vm, null)
        {
        }

        public MainWindow(MainViewModel vm, Bootstrap.Composition composition)
        {
            _vm = vm;
            InitializeComponent();
            DataContext = vm;
            ApplyZoom(vm);
            if (composition != null)
            {
                RegisterView.AddRequested = delegate
                {
                    CourtDesk.UI.Dialogs.DefendantEditDialog dlg = composition.Coordinator.OpenEdit(0);
                    AfterSave(dlg.ViewModel.SavedId);
                };
                RegisterView.EditRequested = delegate(int id)
                {
                    CourtDesk.UI.Dialogs.DefendantEditDialog dlg = composition.Coordinator.OpenEdit(id);
                    AfterSave(dlg.ViewModel.SavedId);
                };
                RegisterView.DecideRequested = delegate(int id) { composition.Coordinator.OpenWizard(id); };
                RegisterView.CardRequested = delegate(int id) { composition.Coordinator.OpenCard(id); };
                RegisterView.VoidRequested = delegate(int id) { composition.Coordinator.VoidRecord(id); };
                vm.Sidebar.RecordRequested += delegate(SessionCard c)
                {
                    composition.Coordinator.OpenWizard(c.Id);
                };
                vm.Sidebar.UndoRequested += delegate(SessionCard c)
                {
                    composition.Coordinator.OpenCard(c.Id);
                };
                vm.Sidebar.CardRequested += delegate(SessionCard c)
                {
                    composition.Coordinator.OpenCard(c.Id);
                };
                vm.Sidebar.PrintRequested += delegate(bool preview) { composition.Coordinator.PrintToday(preview); };
            }
            vm.PropertyChanged += delegate(object s, System.ComponentModel.PropertyChangedEventArgs e)
            {
                if (e.PropertyName == "Zoom")
                {
                    ApplyZoom(vm);
                }
                if (e.PropertyName == "CurrentSection")
                {
                    ApplySection(vm);
                }
            };
            SizeChanged += delegate { ApplyWidthMode(); };
            Loaded += delegate { ApplyWidthMode(); };
            ApplyWidthMode();
            ApplySection(vm);
        }

        private void AfterSave(int savedId)
        {
            if (savedId <= 0)
            {
                return;
            }
            RegisterView.SelectAndShow(savedId);
            Toasts.Show(CourtDesk.Resources.Strings.ToastSaved);
        }

        private void WideToggleClick(object sender, RoutedEventArgs e)
        {
            MainViewModel vm = DataContext as MainViewModel;
            if (vm != null && vm.Register != null)
            {
                vm.Register.Wide = !vm.Register.Wide;
            }
        }

        private void ApplyWidthMode()
        {
            bool wide = ActualWidth >= 1366.0 * _zoom;
            bool narrow = ActualWidth < 1150.0 * _zoom;
            Bar.ApplyWidthMode(wide);
            SidebarColumn.Width = new GridLength(wide ? 380 : (narrow ? 272 : 320));
            RegisterView.SetCompact(!wide, narrow);
        }

private void ApplyZoom(MainViewModel vm)
        {
            _zoom = vm.Zoom;
            RootScale.ScaleX = vm.Zoom;
            RootScale.ScaleY = vm.Zoom;
            ApplyWidthMode();
        }

        private void ApplySection(MainViewModel vm)
        {
            FrameworkElement next;
            if (vm.IsRegister)
            {
                next = RegisterView;
            }
            else if (vm.CurrentSection == "schedule")
            {
                if (_schedule == null)
                {
                    _schedule = new ScheduleView();
                    _schedule.DataContext = vm.Schedule;
                }
                next = _schedule;
            }
            else if (vm.CurrentSection == "history")
            {
                if (_history == null)
                {
                    _history = new HistoryView();
                    _history.DataContext = vm.History;
                }
                next = _history;
            }
            else
            {
                return;
            }
            if (ReferenceEquals(next, _current))
            {
                return;
            }
            if (_current != null)
            {
                _current.Visibility = Visibility.Collapsed;
            }
            if (next.Parent == null)
            {
                ContentHost.Children.Add(next);
            }
            next.Visibility = Visibility.Visible;
            _current = next;
            RegisterView.SetCompact(!wideNow(), ActualWidth < 1150.0 * _zoom);
        }

        private bool wideNow()
        {
            return ActualWidth >= 1366.0 * _zoom;
        }
    }
}
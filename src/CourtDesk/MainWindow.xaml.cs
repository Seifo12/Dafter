using System.Windows;
using CourtDesk.UI.ViewModels;
using CourtDesk.UI.Views;

namespace CourtDesk
{
    public partial class MainWindow : Window
    {
        private readonly ScheduleView _schedule;
        private readonly HistoryView _history;

        public MainWindow(MainViewModel vm) : this(vm, null)
        {
        }

        public MainWindow(MainViewModel vm, Bootstrap.Composition composition)
        {
            InitializeComponent();
            DataContext = vm;
            ApplyZoom(vm);
            if (composition != null)
            {
                _schedule = new ScheduleView { DataContext = new ScheduleViewModel(composition.ScheduleService, composition.Clock) };
                _history = new HistoryView { DataContext = composition.Coordinator.C.History };
                RegisterView.AddRequested = delegate { composition.Coordinator.OpenEdit(0); vm.RefreshAll(); };
                RegisterView.EditRequested = delegate(int id) { composition.Coordinator.OpenEdit(id); vm.RefreshAll(); };
                RegisterView.DecideRequested = delegate(int id) { composition.Coordinator.OpenWizard(id); vm.RefreshAll(); };
                RegisterView.CardRequested = delegate(int id) { composition.Coordinator.OpenCard(id); vm.RefreshAll(); };
                RegisterView.VoidRequested = delegate(int id)
                {
                    composition.Coordinator.VoidRecord(id);
                    composition.SearchIndex.Rebuild();
                    vm.RefreshAll();
                };
                vm.Sidebar.RecordRequested += delegate(SessionCard c) { composition.Coordinator.OpenWizard(c.Id); vm.RefreshAll(); };
                vm.Sidebar.UndoRequested += delegate(SessionCard c)
                {
                    composition.Coordinator.OpenCard(c.Id);
                    vm.RefreshAll();
                };
            }
            vm.PropertyChanged += delegate(object s, System.ComponentModel.PropertyChangedEventArgs e)
            {
                if (e.PropertyName == "CurrentSection")
                {
                    ApplySection(vm);
                }
                if (e.PropertyName == "Zoom")
                {
                    ApplyZoom(vm);
                }
            };
            ApplySection(vm);
        }

        private void ApplyZoom(MainViewModel vm)
        {
            RootScale.ScaleX = vm.Zoom;
            RootScale.ScaleY = vm.Zoom;
        }

        private void ApplySection(MainViewModel vm)
        {
            RegisterView.Visibility = vm.IsRegister ? Visibility.Visible : Visibility.Collapsed;
            if (_schedule != null)
            {
                _schedule.Visibility = vm.CurrentSection == "schedule" ? Visibility.Visible : Visibility.Collapsed;
                _history.Visibility = vm.CurrentSection == "history" ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}

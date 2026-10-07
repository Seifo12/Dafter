using System.Windows;
using CourtDesk.UI.ViewModels;

namespace CourtDesk
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            ApplyZoom(vm);
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
        }
    }
}

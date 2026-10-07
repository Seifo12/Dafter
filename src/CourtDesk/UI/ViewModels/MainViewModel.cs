using System;
using System.Windows.Input;
using CourtDesk.Application;
using CourtDesk.Domain;

namespace CourtDesk.UI.ViewModels
{
    public sealed class MainViewModel : ObservableObject
    {
        private readonly SettingsService _settings;
        private readonly IClock _clock;
        private double _zoom = 1.0;
        private object _currentView;
        private string _currentSection = "register";
        private string _statusRecords = "";
        private string _statusExit = "";
        private string _statusUsb = "";

        public MainViewModel(SettingsService settings, IClock clock,
            RegisterViewModel register, TodaySidebarViewModel sidebar)
        {
            _settings = settings;
            _clock = clock;
            Register = register;
            Sidebar = sidebar;
            int scale = settings.GetInt(SettingKeys.FontScale, 100);
            _zoom = scale == 150 ? 1.5 : (scale == 125 ? 1.25 : 1.0);
            NavCommand = new RelayCommand(delegate(object o)
            {
                string key = o as string;
                if (!string.IsNullOrEmpty(key))
                {
                    CurrentSection = key;
                }
            });
            ZoomInCommand = new RelayCommand(delegate(object o) { StepZoom(1); });
            ZoomOutCommand = new RelayCommand(delegate(object o) { StepZoom(-1); });
            CloseCommand = new RelayCommand(delegate(object o)
            {
                Action h = CloseRequested;
                if (h != null)
                {
                    h();
                }
            });
            HelpCommand = new RelayCommand(delegate(object o) { CurrentSection = "help"; });
            _currentView = register;
        }

        public RegisterViewModel Register { get; private set; }
        public TodaySidebarViewModel Sidebar { get; private set; }
        public ICommand NavCommand { get; private set; }
        public ICommand ZoomInCommand { get; private set; }
        public ICommand ZoomOutCommand { get; private set; }
        public ICommand CloseCommand { get; private set; }
        public ICommand HelpCommand { get; private set; }

        public event Action CloseRequested;

        public string AppTitle
        {
            get { return CourtDesk.Resources.Strings.AppTitle; }
        }

        public string StationLine
        {
            get { return _settings.Get(SettingKeys.StationName); }
        }

        public string TodayLong
        {
            get
            {
                return ArabicDateFormat.FormatLong(_clock.Today,
                    CourtDesk.Resources.Strings.Weekdays, CourtDesk.Resources.Strings.Months);
            }
        }

        public double Zoom
        {
            get { return _zoom; }
        }

        public object CurrentView
        {
            get { return _currentView; }
        }

        public string CurrentSection
        {
            get { return _currentSection; }
            set
            {
                if (Set(ref _currentSection, value, "CurrentSection"))
                {
                    Raise("IsRegister");
                }
            }
        }

        public bool IsRegister
        {
            get { return _currentSection == "register"; }
        }

        public string StatusRecords
        {
            get { return _statusRecords; }
        }

        public string StatusExit
        {
            get { return _statusExit; }
        }

        public string StatusUsb
        {
            get { return _statusUsb; }
        }

        public void SetStatus(string records, string exit, string usb)
        {
            _statusRecords = records;
            _statusExit = exit;
            _statusUsb = usb;
            Raise("StatusRecords");
            Raise("StatusExit");
            Raise("StatusUsb");
            Raise("StationLine");
            Raise("TodayLong");
        }

        public void RefreshAll()
        {
            Register.Refresh();
            Sidebar.Refresh();
            Raise("TodayLong");
        }

        private void StepZoom(int dir)
        {
            double[] steps = new double[] { 1.0, 1.25, 1.5 };
            int at = 0;
            for (int i = 0; i < steps.Length; i++)
            {
                if (Math.Abs(steps[i] - _zoom) < 0.01)
                {
                    at = i;
                }
            }
            at = Math.Max(0, Math.Min(steps.Length - 1, at + dir));
            _zoom = steps[at];
            _settings.Set(SettingKeys.FontScale, ((int)(_zoom * 100)).ToString());
            Raise("Zoom");
        }
    }
}

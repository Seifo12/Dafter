using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;
using CourtDesk.Resources;
using CourtDesk.UI.Controls;
using CourtDesk.UI.ViewModels;

namespace CourtDesk.UI.Dialogs
{
    public partial class DefendantEditDialog : Window
    {
        private readonly DefendantEditViewModel _vm;
        private readonly LookupService _lookups;
        private readonly IClock _clock;
        private readonly PopupHost _popups;
        private MedicalService _medical;

        public void AttachMedical(MedicalService medical)
        {
            _medical = medical;
            if (_vm != null)
            {
                _vm.AttachMedical(medical);
            }
        }

        public DefendantEditDialog(DefendantEditViewModel vm, LookupService lookups, IClock clock, PopupHost popups)
        {
            InitializeComponent();
            _vm = vm;
            _lookups = lookups;
            _clock = clock;
            _popups = popups;
            DataContext = vm;
            vm.PropertyChanged += delegate(object s, System.ComponentModel.PropertyChangedEventArgs e)
            {
                if (e.PropertyName == "HasProblems")
                {
                    SummaryBand.Visibility = vm.HasProblems ? Visibility.Visible : Visibility.Collapsed;
                }
                if (e.PropertyName == "SessionEditable")
                {
                    PickDateButton.IsEnabled = vm.SessionEditable;
                }
            };
            vm.Saved += delegate
            {
                DialogResult = true;
                Close();
            };
            vm.DuplicateFound += delegate(DuplicateHit hit)
            {
                bool add = ConfirmDialog.Ask(this, Strings.FormAddTitle, Strings.DupWarn,
                    Strings.DupAdd, Strings.DupOpen);
                if (add)
                {
                    vm.SaveAnywayCommand.Execute(null);
                }
            };
            LoadLookups();
            PickDateButton.IsEnabled = vm.SessionEditable;
            Title = vm.Title;
            Loaded += delegate
            {
                if (vm.IsNew)
                {
                    NameBox.Focus();
                }
            };
        }

        public DefendantEditViewModel ViewModel
        {
            get { return _vm; }
        }

        private void LoadLookups()
        {
            Fill(CourtBox, Lookups());
            Fill(CustodyBox, CustodyItems());
            ReloadCircuits();
            CourtBox.SelectedValue = _vm.CourtId;
            CustodyBox.SelectedValue = _vm.CustodyId;
            var types = new List<LookupItem>();
            foreach (string t in Strings.CaseTypes)
            {
                types.Add(new LookupItem { Name = t });
            }
            CaseTypeBox.ItemsSource = types;
            CaseTypeBox.Text = _vm.CaseType;
            DetainedText.Text = _vm.DetainedSince.HasValue
                ? ArabicDateFormat.FormatShort(_vm.DetainedSince.Value) : "";
        }

        private List<LookupItem> Lookups()
        {
            var outp = new List<LookupItem>();
            foreach (CourtRow c in _lookups.Courts(true))
            {
                outp.Add(new LookupItem { Id = c.Id, Name = c.Name });
            }
            return outp;
        }

        private List<LookupItem> CustodyItems()
        {
            var outp = new List<LookupItem>();
            foreach (CustodyRow c in _lookups.CustodyLocations(true))
            {
                outp.Add(new LookupItem { Id = c.Id, Name = c.Name });
            }
            return outp;
        }

        private void ReloadCircuits()
        {
            int court = PickedId(CourtBox);
            var outp = new List<LookupItem>();
            if (court > 0)
            {
                foreach (CircuitRow c in _lookups.Circuits(court, true))
                {
                    outp.Add(new LookupItem { Id = c.Id, Name = c.Name });
                }
            }
            CircuitBox.ItemsSource = outp;
            CircuitBox.SelectedValue = _vm.CircuitId;
        }

        private static int PickedId(ComboBox box)
        {
            var item = box.SelectedItem as LookupItem;
            return item == null ? 0 : item.Id;
        }

        private static void Fill(ComboBox box, List<LookupItem> items)
        {
            box.DisplayMemberPath = "Display";
            box.SelectedValuePath = "Id";
            box.ItemsSource = items;
        }

        private void CourtChanged(object sender, SelectionChangedEventArgs e)
        {
            _vm.CourtId = PickedId(CourtBox) == 0 ? (int?)null : PickedId(CourtBox);
            ReloadCircuits();
        }

        private void CustodyChanged(object sender, SelectionChangedEventArgs e)
        {
            _vm.CustodyId = PickedId(CustodyBox) == 0 ? (int?)null : PickedId(CustodyBox);
        }

        private void CircuitChanged(object sender, SelectionChangedEventArgs e)
        {
            _vm.CircuitId = PickedId(CircuitBox) == 0 ? (int?)null : PickedId(CircuitBox);
        }

        private void AddCourtClick(object sender, RoutedEventArgs e)
        {
            string name = _popups.AskText(this, Strings.AddNew, Strings.FieldCourt);
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            int id = _lookups.AddCourt(name);
            Fill(CourtBox, Lookups());
            CourtBox.SelectedValue = id;
            _vm.CourtId = id;
            ReloadCircuits();
        }

        private void AddCircuitClick(object sender, RoutedEventArgs e)
        {
            int court = PickedId(CourtBox);
            if (court == 0)
            {
                MessageDialog.Show(this, Strings.AddNewCircuit, Strings.ValCircuitCourt);
                return;
            }
            string name = _popups.AskText(this, Strings.AddNewCircuit, Strings.FieldCircuit);
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            int id = _lookups.AddCircuit(court, name);
            ReloadCircuits();
            CircuitBox.SelectedValue = id;
            _vm.CircuitId = id;
        }

        private void AddCustodyClick(object sender, RoutedEventArgs e)
        {
            string name = _popups.AskText(this, Strings.AddNew, Strings.FieldCustody);
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            int id = _lookups.AddCustody(name);
            Fill(CustodyBox, CustodyItems());
            CustodyBox.SelectedValue = id;
            _vm.CustodyId = id;
        }

        private void PickDateClick(object sender, RoutedEventArgs e)
        {
            var cal = new BigCalendar();
            cal.Today = _clock.Today;
            cal.Minimum = _clock.Today.AddDays(-365);
            cal.Select(_vm.SessionDate);
            bool ok = _popups.AskCalendar(this, Strings.FieldSessionDate, cal);
            if (ok && cal.Selected.HasValue)
            {
                _vm.SessionDate = cal.Selected.Value;
                _vm.NoSessionDate = false;
                NoSessionButton.Content = Strings.FieldNoDate;
            }
        }

        private void PickDetainedClick(object sender, RoutedEventArgs e)
        {
            if (_popups == null)
            {
                return;
            }
            var cal = new BigCalendar();
            cal.Today = _clock.Today;
            cal.Minimum = _clock.Today.AddDays(-3650);
            cal.Select(_vm.DetainedSince);
            bool ok = _popups.AskCalendar(this, Strings.FieldDetainedSince, cal);
            if (ok && cal.Selected.HasValue)
            {
                _vm.DetainedSince = cal.Selected.Value;
            }
        }

        private void NoDetainedClick(object sender, RoutedEventArgs e)
        {
            _vm.DetainedSince = null;
        }

        private void CancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        protected override void OnContentRendered(System.EventArgs e)
        {
            base.OnContentRendered(e);
            CustodyBox.SelectionChanged += CustodyChanged;
            CircuitBox.SelectionChanged += CircuitChanged;
        }

        private void MedicalImagesClick(object sender, RoutedEventArgs e)
        {
            if (_medical == null || _vm == null)
            {
                return;
            }
            if (_vm.Id <= 0)
            {
                MessageDialog.Show(this, CourtDesk.Resources.Strings.MedicalTitle,
                    CourtDesk.Resources.Strings.MedicalSaveFirst);
                return;
            }
            var dlg = new MedicalImagesDialog(_medical, _vm.Id);
            dlg.ShowDialog();
            _vm.ReloadMedicalPublic();
        }
    }
}
using System;
using System.Collections.Generic;
using System.Windows;
using CourtDesk.Domain;
using CourtDesk.Resources;
using CourtDesk.UI.Controls;
using CourtDesk.UI.ViewModels;

namespace CourtDesk.UI.Dialogs
{
    public partial class DecisionWizardDialog : Window
    {
        private readonly DecisionWizardViewModel _vm;
        private readonly PopupHost _popups;
        private readonly IClock _clock;

        public DecisionWizardDialog(DecisionWizardViewModel vm, PopupHost popups, IClock clock)
        {
            InitializeComponent();
            _vm = vm;
            _popups = popups;
            _clock = clock;
            DataContext = vm;
            vm.PropertyChanged += delegate(object s, System.ComponentModel.PropertyChangedEventArgs e)
            {
                switch (e.PropertyName)
                {
                    case "Step":
                    case "Done":
                    case "AtStep1":
                    case "AtStep2":
                    case "AtStep3":
                        Apply();
                        break;
                    case "Error":
                    case "HasError":
                        ApplyError();
                        break;
                    case "ShowPastWarning":
                    case "ShowWeekendWarning":
                    case "ShowFarWarning":
                        ApplyWarnings();
                        break;
                    case "ChosenLine":
                        ChosenLine.Text = vm.ChosenLine;
                        break;
                    case "Review":
                    case "ReviewSentence":
                        ApplyReview();
                        break;
                    case "IdentityName":
                    case "IdentityCase":
                    case "IdentityCourt":
                    case "IdentitySession":
                        ApplyIdentity();
                        break;
                    case "NeedsNextDate":
                    case "NeedsReason":
                    case "NeedsBail":
                    case "NeedsNote":
                    case "Reasons":
                        ApplyDetailPanels();
                        break;
                    case "QueueProgress":
                    case "InQueue":
                        Apply();
                        break;
                }
            };
            vm.Cancelled += delegate { DialogResult = false; Close(); };
            vm.Closed += delegate { DialogResult = true; Close(); };
            vm.UndoRequested += UndoRequested;
            vm.NextInQueue += delegate { _popups.Tell(this, Strings.WizTitle, Strings.WizDone); Close(); };
            vm.QueueStopped += delegate { Close(); };
            vm.Saved += delegate(Sentence s)
            {
                DoneLine.Text = s.Text;
                Apply();
            };
            Loaded += delegate
            {
                string pre = vm.PreCheckError;
                if (pre != null)
                {
                    ShowError(pre);
                }
                Apply();
                ApplyIdentity();
                ApplyDetailPanels();
                ApplyWarnings();
                ApplyReview();
            };
        }

        public DecisionWizardViewModel ViewModel
        {
            get { return _vm; }
        }

        public event Action UndoRequested;

        private void ApplyIdentity()
        {
            Identity.Text = _vm.IdentityName + "  ·  " + _vm.IdentityCase + "  ·  " + _vm.IdentityCourt
                + "  ·  " + _vm.IdentitySession;
            Title = _vm.IdentityName + " — " + Strings.WizTitle;
        }

        private void Apply()
        {
            StepText.Text = _vm.Done ? "" : _vm.StepText;
            QueueText.Text = _vm.InQueue ? _vm.QueueProgress : "";
            Step1.Visibility = _vm.Done ? Visibility.Collapsed : (_vm.AtStep1 ? Visibility.Visible : Visibility.Collapsed);
            Step2.Visibility = _vm.Done ? Visibility.Collapsed : (_vm.AtStep2 ? Visibility.Visible : Visibility.Collapsed);
            Step3.Visibility = _vm.Done ? Visibility.Collapsed : (_vm.AtStep3 ? Visibility.Visible : Visibility.Collapsed);
            DonePanel.Visibility = _vm.Done ? Visibility.Visible : Visibility.Collapsed;
            PrimaryNext.Visibility = _vm.Done || _vm.AtStep3 || _vm.AtStep1
                ? Visibility.Collapsed : Visibility.Visible;
            PrimarySave.Visibility = !_vm.Done && _vm.AtStep3 ? Visibility.Visible : Visibility.Collapsed;
            BackButton.Visibility = !_vm.Done && !_vm.AtStep1 ? Visibility.Visible : Visibility.Collapsed;
            CancelButton.Visibility = _vm.Done ? Visibility.Collapsed : Visibility.Visible;
            UndoButton.Visibility = _vm.Done ? Visibility.Visible : Visibility.Collapsed;
            StopQueueButton.Visibility = _vm.Done && _vm.InQueue ? Visibility.Visible : Visibility.Collapsed;
            NextPersonButton.Visibility = _vm.Done && _vm.InQueue ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ApplyDetailPanels()
        {
            OffsetsBox.Visibility = _vm.NeedsNextDate ? Visibility.Visible : Visibility.Collapsed;
            PickDateButton.Visibility = _vm.NeedsNextDate ? Visibility.Visible : Visibility.Collapsed;
            ReasonPanel.Visibility = _vm.NeedsReason ? Visibility.Visible : Visibility.Collapsed;
            BailPanel.Visibility = _vm.NeedsBail ? Visibility.Visible : Visibility.Collapsed;
            NotePanel.Visibility = _vm.NeedsNote ? Visibility.Visible : Visibility.Visible;
            NoteLabel.Text = _vm.Code == DecisionCodes.Judgment ? Strings.WizJudgmentNote : Strings.WizNote;
            ChosenLine.Text = _vm.ChosenLine;
            ApplyWarnings();
        }

        private void ApplyWarnings()
        {
            WarnPast.Visibility = _vm.ShowPastWarning ? Visibility.Visible : Visibility.Collapsed;
            WarnWeekend.Visibility = _vm.ShowWeekendWarning ? Visibility.Visible : Visibility.Collapsed;
            WarnFar.Visibility = _vm.ShowFarWarning ? Visibility.Visible : Visibility.Collapsed;
            if (_vm.NextDate.HasValue)
            {
                WeekendText.Text = string.Format(Strings.WarnWeekend,
                    CourtDesk.Domain.ArabicDateFormat.FormatLong(_vm.NextDate.Value, Strings.Weekdays, Strings.Months));
                FarText.Text = string.Format(Strings.WarnFar,
                    LocalDate.DaysBetween(_clock.Today, _vm.NextDate.Value));
            }
        }

        private void ApplyReview()
        {
            ReviewSentence.Text = _vm.ReviewSentence;
            ReviewItems.ItemsSource = _vm.Review;
        }

        private void ApplyError()
        {
            ErrorBand.Visibility = _vm.HasError ? Visibility.Visible : Visibility.Collapsed;
            ErrorText.Text = _vm.Error;
        }

        private void ShowError(string text)
        {
            ErrorBand.Visibility = Visibility.Visible;
            ErrorText.Text = text;
        }

        private void NextClick(object sender, RoutedEventArgs e)
        {
            _vm.NextCommand.Execute(null);
        }

        private void BackClick(object sender, RoutedEventArgs e)
        {
            _vm.BackCommand.Execute(null);
        }

        private void SaveClick(object sender, RoutedEventArgs e)
        {
            _vm.Save();
        }

        private void CancelClick(object sender, RoutedEventArgs e)
        {
            _vm.CancelCommand.Execute(null);
        }

        private void DoneCloseClick(object sender, RoutedEventArgs e)
        {
            _vm.CloseCommand.Execute(null);
        }

        private void NextPersonClick(object sender, RoutedEventArgs e)
        {
            _vm.NextQueueCommand.Execute(null);
        }

        private void StopQueueClick(object sender, RoutedEventArgs e)
        {
            _vm.StopQueueCommand.Execute(null);
        }

        private void UndoClick(object sender, RoutedEventArgs e)
        {
            _vm.UndoCommand.Execute(null);
        }

        private void OffsetClick(object sender, RoutedEventArgs e)
        {
            var b = sender as System.Windows.Controls.Button;
            if (b == null || b.Tag == null)
            {
                return;
            }
            _vm.PickOffset((int)b.Tag);
        }

        private void ReasonClick(object sender, RoutedEventArgs e)
        {
            var b = sender as System.Windows.Controls.Button;
            if (b == null || b.Tag == null)
            {
                return;
            }
            _vm.Reason = (string)b.Tag;
        }

        private void PickDateClick(object sender, RoutedEventArgs e)
        {
            var cal = new BigCalendar();
            cal.Today = _clock.Today;
            if (_vm.NextDate.HasValue)
            {
                cal.ShowMonth(_vm.NextDate.Value);
                cal.Select(_vm.NextDate.Value);
            }
            else
            {
                cal.ShowMonth(_clock.Today);
                cal.Select(_clock.Today.AddDays(7));
            }
            if (_popups.AskCalendar(this, Strings.WizPickDate, cal))
            {
                _vm.PickDate(cal.Selected.Value);
            }
        }
    }
}
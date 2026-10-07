using System;
using System.Collections.Generic;
using System.Windows;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;
using CourtDesk.Resources;
using CourtDesk.UI.Dialogs;
using CourtDesk.UI.ViewModels;

namespace CourtDesk.UI
{
    public sealed class UiCoordinator
    {
        private readonly PopupHost _popups = new PopupHost();

        public UiCoordinator(CompositionLike composition)
        {
            C = composition;
        }

        public sealed class CompositionLike
        {
            public DefendantService Defendants;
            public DecisionService Decisions;
            public UndoService Undo;
            public LookupService Lookups;
            public SettingsService Settings;
            public ScheduleService Schedule;
            public TodayService Today;
            public IClock Clock;
            public HistoryViewModel History;
        public CourtDesk.Printing.PrintService Printing;
        public RosterService Rosters;
        }

        public CompositionLike C { get; private set; }

        public bool LastPrintWasPreview { get; private set; }
        public System.DateTime LastPrintRequestedUtc { get; private set; }

        public void PrintToday(bool preview)
        {
            LastPrintWasPreview = preview;
            LastPrintRequestedUtc = C.Clock.UtcNow;
            PrintRequested(preview, C.Clock.Today);
            DoPrint(preview, C.Clock.Today);
        }

        public void PrintDate(bool preview, LocalDate date)
        {
            LastPrintWasPreview = preview;
            LastPrintRequestedUtc = C.Clock.UtcNow;
            PrintRequested(preview, date);
            DoPrint(preview, date);
        }

        public event Action<bool, LocalDate> PrintRequested;

        private void DoPrint(bool preview, LocalDate date)
        {
            if (C.Printing == null)
            {
                return;
            }
            RosterModel model = C.Rosters.Build(date);
            if (model.Count == 0)
            {
                CourtDesk.UI.Dialogs.MessageDialog.Show(null,
                    CourtDesk.Resources.Strings.PrintEmpty,
                    CourtDesk.Resources.Strings.PrintEmpty);
                return;
            }
            bool direct = !preview && C.Settings.Get(SettingKeys.PrintDirect) != "0";
            PrintOutcome outcome = C.Printing.PrintRoster(model, direct);
            switch (outcome)
            {
                case PrintOutcome.Printed:
                    CourtDesk.UI.Dialogs.MessageDialog.Show(null,
                        CourtDesk.Resources.Strings.PrintSent,
                        CourtDesk.Resources.Strings.PrintSent);
                    break;
                case PrintOutcome.PreviewRequested:
                case PrintOutcome.NoPrinter:
                case PrintOutcome.Failed:
                    if (outcome != PrintOutcome.PreviewRequested && C.Printing.LastError != null)
                    {
                        CourtDesk.UI.Dialogs.MessageDialog.Show(null,
                            CourtDesk.Resources.Strings.PrintFail,
                            C.Printing.LastError);
                    }
                    new CourtDesk.UI.Dialogs.PrintPreviewWindow(
                        C.Printing.BuildDocument(model)).ShowDialog();
                    break;
            }
        }
        public DefendantEditDialog OpenEdit(int id)
        {
            Defendant existing = id == 0 ? null : C.Defendants.Get(id);
            var vm = new DefendantEditViewModel(C.Defendants, C.Lookups, C.Clock, existing);
            var dlg = new DefendantEditDialog(vm, C.Lookups, C.Clock, _popups);
            dlg.ShowDialog();
            return dlg;
        }

        public DecisionWizardDialog OpenWizard(int id)
        {
            return OpenWizard(new List<int> { id }, 0);
        }

        public DecisionWizardDialog OpenWizard(IList<int> queue, int at)
        {
            var vm = new DecisionWizardViewModel(C.Decisions, C.Undo, C.Defendants,
                C.Lookups, C.Settings, C.Clock, new List<int>(queue), at);
            var dlg = new DecisionWizardDialog(vm, _popups, C.Clock);
            dlg.UndoRequested += delegate { ConfirmUndo(dlg); };
            dlg.ShowDialog();
            return dlg;
        }

        public void ConfirmUndo(DecisionWizardDialog dlg)
        {
            string decision = dlg.ViewModel.CodeLabel;
            string who = dlg.ViewModel.IdentityName;
            bool ok = _popups.Confirm(dlg, Strings.WizUndo,
                string.Format(Strings.UndoConfirm, decision, who), Strings.UndoYes, Strings.UndoNo);
            if (!ok)
            {
                return;
            }
            try
            {
                C.Undo.Undo(LastEventOf(dlg.ViewModel.DefendantId), Guid.NewGuid().ToString("N"));
                _popups.Tell(dlg, Strings.WizUndo, Strings.SideUndo);
            }
            catch (UndoNotAllowedException)
            {
                _popups.Tell(dlg, Strings.WizUndo, Strings.ErrStale);
            }
        }

        private long LastEventOf(int defendantId)
        {
            long id = 0;
            foreach (SessionEvent ev in C.Defendants.History(defendantId))
            {
                if (C.Undo.CanUndo(ev.Id))
                {
                    id = ev.Id;
                }
            }
            return id;
        }

        public DefendantCardDialog OpenCard(int id)
        {
            var dlg = new DefendantCardDialog(C.Defendants, C.Undo, C.Lookups, id);
            dlg.EditRequested += delegate { OpenEdit(id); dlg.Reload(); };
            dlg.DecideRequested += delegate { OpenWizard(id); dlg.Reload(); };
            dlg.UndoRequested += delegate
            {
                bool ok = _popups.Confirm(dlg, Strings.WizUndo, Strings.WizUndo,
                    Strings.UndoYes, Strings.UndoNo);
                if (ok)
                {
                    long ev = LastEventOf(id);
                    if (ev != 0)
                    {
                        try
                        {
                            C.Undo.Undo(ev, Guid.NewGuid().ToString("N"));
                        }
                        catch (UndoNotAllowedException)
                        {
                        }
                    }
                }
                dlg.Reload();
            };
            dlg.ShowDialog();
            return dlg;
        }

        public bool VoidRecord(int id)
        {
            Defendant d = C.Defendants.Get(id);
            if (d == null)
            {
                return false;
            }
            string body = string.Format(Strings.VoidSentence, d.FullName,
                string.Format(Strings.CaseFormat, d.CaseNumber, d.CaseYear));
            bool ok = _popups.Confirm(System.Windows.Application.Current == null ? null : System.Windows.Application.Current.MainWindow,
                Strings.VoidTitle, body, Strings.VoidYes, Strings.VoidNo);
            if (!ok)
            {
                return false;
            }
            try
            {
                C.Defendants.Void(id, d.RowVersion, Guid.NewGuid().ToString("N"));
                return true;
            }
            catch (StaleRecordException)
            {
                _popups.Tell(System.Windows.Application.Current == null ? null : System.Windows.Application.Current.MainWindow,
                    Strings.VoidTitle, Strings.ErrStale);
                return false;
            }
        }
    }
}
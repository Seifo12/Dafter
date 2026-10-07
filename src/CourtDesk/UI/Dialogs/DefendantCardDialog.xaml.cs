using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;
using CourtDesk.Resources;

namespace CourtDesk.UI.Dialogs
{
    public sealed class TimelineEntry
    {
        public string Title { get; set; }
        public string Detail { get; set; }
    }

    public partial class DefendantCardDialog : Window
    {
        private readonly DefendantService _defendants;
        private readonly UndoService _undo;
        private readonly LookupService _lookups;
        private readonly int _id;
        private long _undoEventId;

        public DefendantCardDialog(DefendantService defendants, UndoService undo,
            LookupService lookups, int id)
        {
            InitializeComponent();
            _defendants = defendants;
            _undo = undo;
            _lookups = lookups;
            _id = id;
            Title = Strings.ActionCard;
            Reload();
        }

        public event Action EditRequested;
        public event Action DecideRequested;
        public event Action UndoRequested;

        public void Reload()
        {
            Defendant d = _defendants.Get(_id);
            if (d == null)
            {
                return;
            }
            NameText.Text = d.FullName;
            CaseText.Text = string.Format(Strings.CaseFormat, d.CaseNumber, d.CaseYear);
            string state = d.Status == DefendantStatuses.Released ? Strings.StatusReleased
                : d.Status == DefendantStatuses.Judged ? Strings.StatusJudged
                : Strings.StatusDetained;
            CourtText.Text = Strings.GridCourt + ": " + LookupName(d.CourtId) + " / " + LookupName(d.CircuitId);
            StateText.Text = Strings.GridState + ": " + state
                + (d.SessionDate.HasValue
                    ? " — " + Strings.GridSession + ": " + ArabicDateFormat.FormatShort(d.SessionDate.Value)
                    : " — " + Strings.FieldNoDate);
            var sb = new StringBuilder();
            Line(sb, Strings.FieldName, d.FullName);
            Line(sb, Strings.FieldNationalId, d.NationalId);
            Line(sb, Strings.GridCase, string.Format(Strings.CaseFormat, d.CaseNumber, d.CaseYear));
            Line(sb, Strings.FieldCaseType, d.CaseType);
            Line(sb, Strings.FieldCharges, d.Charges);
            Line(sb, Strings.FieldCourt, LookupName(d.CourtId));
            Line(sb, Strings.FieldCircuit, CircuitNameOf(d.CircuitId));
            Line(sb, Strings.FieldCustody, LookupName(d.CustodyLocationId));
            Line(sb, Strings.GridSession, d.SessionDate.HasValue
                ? ArabicDateFormat.FormatShort(d.SessionDate.Value) : Strings.FieldNoDate);
            Line(sb, Strings.FieldDetainedSince, d.DetainedSince.HasValue
                ? ArabicDateFormat.FormatShort(d.DetainedSince.Value) : "");
            Line(sb, Strings.FieldNotes, d.Notes);
            FieldsBox.Text = sb.ToString();
            var entries = new List<TimelineEntry>();
            List<SessionEvent> history = _defendants.History(_id);
            for (int i = history.Count - 1; i >= 0; i--)
            {
                SessionEvent ev = history[i];
                entries.Add(new TimelineEntry
                {
                    Title = EventTitle(ev),
                    Detail = EventDetail(ev)
                });
            }
            Timeline.ItemsSource = entries;
            _undoEventId = 0;
            foreach (SessionEvent ev in history)
            {
                if (_undo.CanUndo(ev.Id))
                {
                    _undoEventId = ev.Id;
                    break;
                }
            }
            UndoButton.IsEnabled = _undoEventId != 0;
        }

        private string LookupName(int? id)
        {
            if (!id.HasValue)
            {
                return "";
            }
            foreach (CourtRow c in _lookups.Courts(false))
            {
                if (c.Id == id.Value)
                {
                    return c.Name;
                }
            }
            foreach (CustodyRow c in _lookups.CustodyLocations(false))
            {
                if (c.Id == id.Value)
                {
                    return c.Name;
                }
            }
            return "";
        }

        private string CircuitNameOf(int? id)
        {
            if (!id.HasValue)
            {
                return "";
            }
            foreach (CourtRow c in _lookups.Courts(false))
            {
                foreach (CircuitRow r in _lookups.Circuits(c.Id, false))
                {
                    if (r.Id == id.Value)
                    {
                        return r.Name;
                    }
                }
            }
            return "";
        }

        private static void Line(StringBuilder sb, string label, string value)
        {
            sb.Append(label);
            sb.Append(": ");
            sb.Append(value ?? "");
            sb.AppendLine();
        }

        private static string EventTitle(SessionEvent ev)
        {
            if (ev.UndoneByEventId.HasValue)
            {
                return Strings.SideUndo + " — " + EventName(ev.EventType);
            }
            return EventName(ev.EventType);
        }

        private static string EventName(string type)
        {
            switch (type)
            {
                case EventTypes.Created: return Strings.FormAddTitle;
                case EventTypes.Edited: return Strings.ActionEdit;
                case EventTypes.Decision: return Strings.WizTitle;
                case EventTypes.Undo: return Strings.SideUndo;
                case EventTypes.Deleted: return Strings.VoidTitle;
                case EventTypes.Restored: return Strings.TrashRestore;
                default: return Strings.BkTitle;
            }
        }

        private static string EventDetail(SessionEvent ev)
        {
            var sb = new StringBuilder();
            sb.Append(ev.OccurredAtUtc);
            sb.Append(" — ");
            sb.Append(ev.OperatorName);
            if (!string.IsNullOrEmpty(ev.DecisionCode))
            {
                sb.Append(" — ");
                sb.Append(UI.ViewModels.DecisionWizardViewModel.LabelOf(ev.DecisionCode));
            }
            if (!string.IsNullOrEmpty(ev.NextSessionDate))
            {
                sb.Append(" ← ");
                sb.Append(ev.NextSessionDate);
            }
            if (!string.IsNullOrEmpty(ev.Note))
            {
                sb.Append(" — ");
                sb.Append(ev.Note);
            }
            return sb.ToString();
        }

        private void EditClick(object sender, RoutedEventArgs e)
        {
            Action h = EditRequested;
            if (h != null)
            {
                h();
                Reload();
            }
        }

        private void DecideClick(object sender, RoutedEventArgs e)
        {
            Action h = DecideRequested;
            if (h != null)
            {
                h();
                Reload();
            }
        }

        private void UndoClick(object sender, RoutedEventArgs e)
        {
            if (_undoEventId == 0)
            {
                return;
            }
            Action h = UndoRequested;
            if (h != null)
            {
                h();
                Reload();
            }
        }

        private void CloseClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
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
        private MedicalService _photos;

        public void AttachPhotos(MedicalService photos)
        {
            _photos = photos;
            ReloadPhoto();
        }

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

        private void ReloadPhoto()
        {
            string abs = null;
            try
            {
                Defendant d = _defendants.Get(_id);
                if (d != null && _photos != null && !string.IsNullOrEmpty(d.PhotoPath))
                {
                    string p = _photos.PhotoAbsolute(d.PhotoPath);
                    if (!string.IsNullOrEmpty(p) && System.IO.File.Exists(p))
                    {
                        abs = p;
                    }
                }
            }
            catch (System.Exception)
            {
                abs = null;
            }
            PhotoFrame.Visibility = abs == null ? Visibility.Collapsed : Visibility.Visible;
            if (abs == null)
            {
                CardPhoto.Source = null;
                return;
            }
            try
            {
                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.UriSource = new System.Uri(abs);
                bmp.EndInit();
                bmp.Freeze();
                CardPhoto.Source = bmp;
            }
            catch (System.Exception)
            {
                CardPhoto.Source = null;
                PhotoFrame.Visibility = Visibility.Collapsed;
            }
        }

        private string MedicalSummary(int id)
        {
            if (_photos == null)
            {
                return "";
            }
            int imgs = 0;
            string note = "";
            try
            {
                imgs = _photos.Images(id).Count;
                System.Collections.Generic.List<CourtDesk.Application.Ports.MedicalNote> notes =
                    _photos.Notes(id);
                if (notes.Count > 0)
                {
                    note = notes[0].Text ?? "";
                }
            }
            catch (System.Exception)
            {
                return "";
            }
            if (imgs == 0 && string.IsNullOrWhiteSpace(note))
            {
                return "";
            }
            string shortNote = note ?? "";
            if (shortNote.Length > 60)
            {
                shortNote = shortNote.Substring(0, 60) + "...";
            }
            if (imgs > 0 && !string.IsNullOrWhiteSpace(shortNote))
            {
                return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    Strings.MedicalImageCount, imgs) + " - " + shortNote;
            }
            if (imgs > 0)
            {
                return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    Strings.MedicalImageCount, imgs);
            }
            return shortNote;
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
            CourtText.Text = Strings.GridCourt + ": " + CourtNameOf(d.CourtId) + " / " + CircuitNameOf(d.CircuitId);
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
            Line(sb, Strings.FieldCourt, CourtNameOf(d.CourtId));
            Line(sb, Strings.FieldCircuit, CircuitNameOf(d.CircuitId));
            Line(sb, Strings.FieldCustody, CustodyNameOf(d.CustodyLocationId));
            Line(sb, Strings.GridSession, d.SessionDate.HasValue
                ? ArabicDateFormat.FormatShort(d.SessionDate.Value) : Strings.FieldNoDate);
            Line(sb, Strings.FieldDetainedSince, d.DetainedSince.HasValue
                ? ArabicDateFormat.FormatShort(d.DetainedSince.Value) : "");
            Line(sb, Strings.FieldNotes, d.Notes);
            string med = MedicalSummary(d.Id);
            if (!string.IsNullOrEmpty(med))
            {
                Line(sb, Strings.MedicalTitle, med);
            }
            FieldsBox.Text = sb.ToString();
            ReloadPhoto();
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

        private string CourtNameOf(int? id)
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
            return "";
        }

        private string CustodyNameOf(int? id)
        {
            if (!id.HasValue)
            {
                return "";
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
            sb.Append(FormatStamp(ev));
            sb.Append(" - ");
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

        private static string FormatStamp(SessionEvent ev)
        {
            string when = ev.OccurredAtUtc ?? "";
            try
            {
                if (when.Length >= 16)
                {
                    LocalDate d = LocalDate.Parse(when.Substring(0, 10));
                    return ArabicDateFormat.FormatShort(d) + " " + when.Substring(11, 5);
                }
            }
            catch (Exception)
            {
            }
            try
            {
                if (!string.IsNullOrEmpty(ev.OccurredLocalDate))
                {
                    return ArabicDateFormat.FormatShort(LocalDate.Parse(ev.OccurredLocalDate));
                }
            }
            catch (Exception)
            {
            }
            return when;
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
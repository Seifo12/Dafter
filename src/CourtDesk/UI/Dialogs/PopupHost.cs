using System.Windows;
using CourtDesk.Resources;
using CourtDesk.UI.Controls;

namespace CourtDesk.UI.Dialogs
{
    public sealed class PopupHost
    {
        public string AskText(Window owner, string title, string label)
        {
            var w = new AskTextWindow(title, label);
            if (owner != null && owner.IsVisible)
            {
                w.Owner = owner;
            }
            return w.ShowDialog() == true ? w.Value : null;
        }

        public bool AskCalendar(Window owner, string title, BigCalendar calendar)
        {
            var w = new AskCalendarWindow(title, calendar);
            if (owner != null && owner.IsVisible)
            {
                w.Owner = owner;
            }
            return w.ShowDialog() == true;
        }

        public bool Confirm(Window owner, string title, string body, string danger, string safe)
        {
            return ConfirmDialog.Ask(owner, title, body, danger, safe);
        }

        public void Tell(Window owner, string title, string body)
        {
            MessageDialog.Show(owner, title, body);
        }
    }
}
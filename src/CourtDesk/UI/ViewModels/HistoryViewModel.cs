using System;
using System.Collections.Generic;
using System.Windows.Input;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;
using CourtDesk.Resources;

namespace CourtDesk.UI.ViewModels
{
    public sealed class HistoryRow
    {
        public int DefendantId { get; set; }
        public string When { get; set; }
        public string Defendant { get; set; }
        public string Event { get; set; }
        public string NextDate { get; set; }
        public string Operator { get; set; }
        public string Undone { get; set; }
    }

    public sealed class HistoryViewModel : ObservableObject
    {
        private readonly IDatabase _db;
        private readonly IEventStore _events;
        private readonly IDefendantStore _defendants;
        private string _query = "";
        private readonly List<HistoryRow> _rows = new List<HistoryRow>();

        public HistoryViewModel(IDatabase db, IEventStore events, IDefendantStore defendants)
        {
            _db = db;
            _events = events;
            _defendants = defendants;
            RefreshCommand = new RelayCommand(delegate { Refresh(); });
            OpenCommand = new RelayCommand(delegate(object o)
            {
                var r = o as HistoryRow;
                if (r != null)
                {
                    Action<int> h = OpenRequested;
                    if (h != null)
                    {
                        h(r.DefendantId);
                    }
                }
            });
            Refresh();
        }

        public ICommand RefreshCommand { get; private set; }
        public ICommand OpenCommand { get; private set; }
        public event Action<int> OpenRequested;

        public string Title { get { return Strings.NavHistory; } }
        public List<HistoryRow> Rows { get { return _rows; } }

        public string Query
        {
            get { return _query; }
            set
            {
                if (Set(ref _query, value ?? "", "Query"))
                {
                    Refresh();
                }
            }
        }

        public string EmptyText
        {
            get { return _rows.Count == 0 ? Strings.SearchNone : ""; }
        }

        public void Refresh()
        {
            _rows.Clear();
            string q = Domain.ArabicText.Normalize(_query);
            List<SessionEvent> all = _db.Read(s => _events.ListAllNewestFirst(s));
            foreach (SessionEvent ev in all)
            {
                Defendant d = _db.Read(s => _defendants.GetById(s, ev.DefendantId));
                string name = d == null ? "" : d.FullName;
                if (q.Length > 0)
                {
                    string hay = Domain.ArabicText.Normalize(name + " " + Domain.ArabicText.Normalize(ev.DecisionCode ?? ""));
                    if (hay.IndexOf(q, StringComparison.Ordinal) < 0)
                    {
                        continue;
                    }
                }
                _rows.Add(new HistoryRow
                {
                    DefendantId = ev.DefendantId,
                    When = ev.OccurredAtUtc,
                    Defendant = name,
                    Event = EventName(ev),
                    NextDate = ev.NextSessionDate ?? "",
                    Operator = ev.OperatorName,
                    Undone = ev.UndoneByEventId.HasValue ? Strings.SideUndo : ""
                });
            }
            Raise("Rows");
            Raise("EmptyText");
        }

        private static string EventName(SessionEvent ev)
        {
            if (ev.EventType == EventTypes.Decision)
            {
                return DecisionWizardViewModel.LabelOf(ev.DecisionCode);
            }
            switch (ev.EventType)
            {
                case EventTypes.Created: return Strings.FormAddTitle;
                case EventTypes.Edited: return Strings.ActionEdit;
                case EventTypes.Undo: return Strings.SideUndo;
                case EventTypes.Deleted: return Strings.VoidTitle;
                case EventTypes.Restored: return Strings.TrashRestore;
                default: return ev.EventType;
            }
        }
    }
}
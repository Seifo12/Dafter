using System;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;

namespace CourtDesk.Application
{
    public sealed class UndoResult
    {
        public long UndoEventId;
        public DefendantSnapshot Restored;
    }

    public sealed class UndoService
    {
        private readonly IDatabase _db;
        private readonly IDefendantStore _defendants;
        private readonly IEventStore _events;
        private readonly IClock _clock;
        private readonly SettingsService _settings;

        public UndoService(IDatabase db, IDefendantStore defendants, IEventStore events,
            IClock clock, SettingsService settings)
        {
            _db = db;
            _defendants = defendants;
            _events = events;
            _clock = clock;
            _settings = settings;
        }

        public bool CanUndo(long eventId)
        {
            return _db.Read(s =>
            {
                SessionEvent ev = _events.Get(s, eventId);
                if (ev == null || ev.UndoneByEventId.HasValue)
                {
                    return false;
                }
                if (ev.EventType != EventTypes.Decision && ev.EventType != EventTypes.Edited)
                {
                    return false;
                }
                return _events.LatestId(s, ev.DefendantId) == ev.Id;
            });
        }

        public UndoResult Undo(long eventId, string requestUuid)
        {
            LocalDate today = _clock.Today;
            string operatorName = _settings.OperatorOrUnknown();
            UndoResult prior = _db.Read(s =>
            {
                if (!_events.ExistsByRequestUuid(s, requestUuid))
                {
                    return null;
                }
                SessionEvent existing = _events.GetByRequestUuid(s, requestUuid);
                return new UndoResult
                {
                    UndoEventId = existing.Id,
                    Restored = SnapshotJson.Read(existing.AfterJson)
                };
            });
            if (prior != null)
            {
                return prior;
            }
            return _db.Write(s =>
            {
                if (_events.ExistsByRequestUuid(s, requestUuid))
                {
                    SessionEvent existing = _events.GetByRequestUuid(s, requestUuid);
                    return new UndoResult
                    {
                        UndoEventId = existing.Id,
                        Restored = SnapshotJson.Read(existing.AfterJson)
                    };
                }
                SessionEvent ev = _events.Get(s, eventId);
                if (ev == null || ev.UndoneByEventId.HasValue)
                {
                    throw new UndoNotAllowedException("not undoable");
                }
                if (ev.EventType != EventTypes.Decision && ev.EventType != EventTypes.Edited)
                {
                    throw new UndoNotAllowedException("type not undoable");
                }
                if (_events.LatestId(s, ev.DefendantId) != ev.Id)
                {
                    throw new UndoNotAllowedException("not latest");
                }
                Defendant row = _defendants.GetById(s, ev.DefendantId);
                if (row == null || SnapshotJson.Write(DefendantSnapshot.FromDefendant(row)) != ev.AfterJson)
                {
                    throw new UndoNotAllowedException("drifted");
                }
                DefendantSnapshot restored = SnapshotJson.Read(ev.BeforeJson);
                restored.Id = ev.DefendantId;
                long undoId = _events.Insert(s, new SessionEvent
                {
                    RequestUuid = requestUuid,
                    DefendantId = ev.DefendantId,
                    EventType = EventTypes.Undo,
                    UndoesEventId = ev.Id,
                    CourtNameAtEvent = ev.CourtNameAtEvent,
                    CircuitNameAtEvent = ev.CircuitNameAtEvent,
                    BeforeJson = ev.AfterJson,
                    AfterJson = SnapshotJson.Write(restored),
                    OperatorName = operatorName,
                    OccurredAtUtc = s.UtcNowIso,
                    OccurredLocalDate = today.ToIso()
                });
                restored.CopyTo(row);
                if (!_defendants.UpdateFull(s, row))
                {
                    throw new StaleRecordException();
                }
                _events.MarkUndone(s, ev.Id, undoId);
                Defendant check = _defendants.GetById(s, ev.DefendantId);
                if (SnapshotJson.Write(DefendantSnapshot.FromDefendant(check)) != SnapshotJson.Write(restored))
                {
                    throw new DecisionInvariantException("undo read-back");
                }
                return new UndoResult { UndoEventId = undoId, Restored = restored };
            });
        }
    }

    public sealed class UndoNotAllowedException : Exception
    {
        public UndoNotAllowedException(string reason) : base("undo: " + reason) { }
    }
}

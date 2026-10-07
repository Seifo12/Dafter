using System;
using System.Collections.Generic;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;

namespace CourtDesk.Application
{
    public sealed class DecideResult
    {
        public bool AlreadyApplied;
        public long EventId;
        public DefendantSnapshot After;
        public string CourtNameAtEvent;
        public string CircuitNameAtEvent;
        public LocalDate? NextDate;
    }

    public interface IDecisionFaults
    {
        void Hit(string point);
    }

    public sealed class NoFaults : IDecisionFaults
    {
        public void Hit(string point) { }
    }

    public sealed class DecisionService
    {
        private readonly IDatabase _db;
        private readonly IDefendantStore _defendants;
        private readonly IEventStore _events;
        private readonly IClock _clock;
        private readonly SettingsService _settings;
        private readonly IDecisionFaults _faults;

        public DecisionService(IDatabase db, IDefendantStore defendants, IEventStore events,
            IClock clock, SettingsService settings)
            : this(db, defendants, events, clock, settings, new NoFaults())
        {
        }

        public DecisionService(IDatabase db, IDefendantStore defendants, IEventStore events,
            IClock clock, SettingsService settings, IDecisionFaults faults)
        {
            _db = db;
            _defendants = defendants;
            _events = events;
            _clock = clock;
            _settings = settings;
            _faults = faults ?? new NoFaults();
        }

        public DecideResult Decide(DecisionRequest req)
        {
            LocalDate today = _clock.Today;
            string operatorName = _settings.OperatorOrUnknown();
            DecideResult prior = _db.Read(s =>
            {
                if (!_events.ExistsByRequestUuid(s, req.RequestUuid))
                {
                    return null;
                }
                SessionEvent existing = _events.GetByRequestUuid(s, req.RequestUuid);
                return ToAlreadyApplied(existing);
            });
            if (prior != null)
            {
                return prior;
            }
            return _db.Write(s =>
            {
                _faults.Hit("open-tx");
                if (_events.ExistsByRequestUuid(s, req.RequestUuid))
                {
                    SessionEvent existing = _events.GetByRequestUuid(s, req.RequestUuid);
                    return ToAlreadyApplied(existing);
                }
                _faults.Hit("read-row");
                Defendant row = _defendants.GetById(s, req.DefendantId);
                ValidationResult vr = DecisionRules.Validate(row, req, today);
                if (vr.HasErrors)
                {
                    throw new DecisionValidationException(vr);
                }
                _faults.Hit("after-validate");
                string courtName = _defendants.CourtName(s, req.CourtId.HasValue ? req.CourtId : row.CourtId);
                string circuitName = _defendants.CircuitName(s, req.CircuitId.HasValue ? req.CircuitId : row.CircuitId);
                string held = row.Status == DefendantStatuses.Detained && row.SessionDate.HasValue
                    ? row.SessionDate.Value.ToIso() : today.ToIso();
                DefendantSnapshot after = DecisionRules.Apply(DefendantSnapshot.FromDefendant(row), req, today);
                after.Id = row.Id;
                _faults.Hit("after-apply");
                var ev = new SessionEvent
                {
                    RequestUuid = req.RequestUuid,
                    DefendantId = req.DefendantId,
                    EventType = EventTypes.Decision,
                    DecisionCode = req.Code,
                    SessionDateHeld = held,
                    NextSessionDate = req.NextDate.HasValue ? req.NextDate.Value.ToIso() : null,
                    ReasonText = Blank(req.ReasonText),
                    BailAmount = req.BailAmount,
                    Note = Blank(req.Note),
                    CourtNameAtEvent = courtName,
                    CircuitNameAtEvent = circuitName,
                    BeforeJson = SnapshotJson.Write(DefendantSnapshot.FromDefendant(row)),
                    AfterJson = SnapshotJson.Write(after),
                    OperatorName = operatorName,
                    OccurredAtUtc = s.UtcNowIso,
                    OccurredLocalDate = today.ToIso()
                };
                _faults.Hit("before-insert-event");
                long eventId = _events.Insert(s, ev);
                _faults.Hit("after-insert-event");
                after.CopyTo(row);
                if (!_defendants.UpdateFull(s, row))
                {
                    throw new StaleRecordException();
                }
                _faults.Hit("after-update-row");
                Defendant check = _defendants.GetById(s, req.DefendantId);
                if (SnapshotJson.Write(DefendantSnapshot.FromDefendant(check)) != SnapshotJson.Write(after))
                {
                    throw new DecisionInvariantException("row mismatch");
                }
                if (_events.LatestId(s, req.DefendantId) != eventId)
                {
                    throw new DecisionInvariantException("latest mismatch");
                }
                _faults.Hit("after-verify");
                return new DecideResult
                {
                    EventId = eventId,
                    After = after,
                    CourtNameAtEvent = courtName,
                    CircuitNameAtEvent = circuitName,
                    NextDate = req.NextDate
                };
            });
        }

        private static DecideResult ToAlreadyApplied(SessionEvent existing)
        {
            return new DecideResult
            {
                AlreadyApplied = true,
                EventId = existing.Id,
                After = SnapshotJson.Read(existing.AfterJson),
                CourtNameAtEvent = existing.CourtNameAtEvent,
                CircuitNameAtEvent = existing.CircuitNameAtEvent,
                NextDate = existing.NextSessionDate == null ? (LocalDate?)null : LocalDate.Parse(existing.NextSessionDate)
            };
        }

        private static string Blank(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return null;
            }
            string t = s.Trim();
            return t.Length == 0 ? null : t;
        }
    }

    public sealed class DecisionValidationException : Exception
    {
        public readonly ValidationResult Result;

        public DecisionValidationException(ValidationResult result)
            : base("decision: " + string.Join(",", result.Errors.ToArray()))
        {
            Result = result;
        }
    }

    public sealed class DecisionInvariantException : Exception
    {
        public DecisionInvariantException(string detail) : base("invariant: " + detail) { }
    }
}

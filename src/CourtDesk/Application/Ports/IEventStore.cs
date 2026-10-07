using System.Collections.Generic;
using CourtDesk.Domain;

namespace CourtDesk.Application.Ports
{
    public interface IEventStore
    {
        bool ExistsByRequestUuid(ISession s, string uuid);
        SessionEvent GetByRequestUuid(ISession s, string uuid);
        long Insert(ISession s, SessionEvent e);
        SessionEvent Get(ISession s, long id);
        long LatestId(ISession s, int defendantId);
        void MarkUndone(ISession s, long eventId, long undoId);
        List<SessionEvent> ListByDefendant(ISession s, int defendantId);
        List<SessionEvent> ListDecisionsOn(ISession s, string heldDateIso);
        int CountAll(ISession s);
    }
}

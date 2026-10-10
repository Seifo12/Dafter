using System.Collections.Generic;
using CourtDesk.Domain;

namespace CourtDesk.Application.Ports
{
    public sealed class DuplicateHit
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string CaseNumber { get; set; }
        public int CaseYear { get; set; }
    }

    public interface IDefendantStore
    {
        Defendant GetById(ISession s, int id);
        int Insert(ISession s, Defendant d);
        bool UpdateFull(ISession s, Defendant d);
        bool SetDeleted(ISession s, int id, int expectedVersion, bool deleted);
        List<Defendant> ListDetainedOn(ISession s, string dateIso);
        List<Defendant> ListOverdue(ISession s, string dateIso);
        int CountOn(ISession s, string dateIso);
        int CountNoDate(ISession s);
        int CountActive(ISession s);
        List<Defendant> ListActive(ISession s);
        List<DefendantRow> ListPendingOn(ISession s, string dateIso);
        List<DefendantRow> ListPendingAll(ISession s);
        List<DefendantRow> ListOverdueRows(ISession s, string dateIso);
        List<DecidedRow> ListDecidedOn(ISession s, string heldDateIso);
        List<DecidedRow> ListDecidedAll(ISession s);
        List<DefendantRow> ListActiveRows(ISession s);
        List<DefendantRow> ListRowsByIds(ISession s, int[] ids);
        List<Defendant> ListDeleted(ISession s);
        string CourtName(ISession s, int? id);
        string CircuitName(ISession s, int? id);
        string CustodyName(ISession s, int? id);
        List<DuplicateHit> FindDuplicates(ISession s, string nameNorm, string caseNumber, int caseYear, int excludeId);
    }
}

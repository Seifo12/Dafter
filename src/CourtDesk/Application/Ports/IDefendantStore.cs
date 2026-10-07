using System.Collections.Generic;
using CourtDesk.Domain;

namespace CourtDesk.Application.Ports
{
    public sealed class DuplicateHit
    {
        public int Id;
        public string FullName;
        public string CaseNumber;
        public int CaseYear;
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
        List<DuplicateHit> FindDuplicates(ISession s, string nameNorm, string caseNumber, int caseYear, int excludeId);
    }
}

using System.Collections.Generic;

namespace CourtDesk.Application.Ports
{
    public sealed class CourtRow
    {
        public int Id;
        public string Name;
        public int SortOrder;
        public bool IsActive;
    }

    public sealed class CircuitRow
    {
        public int Id;
        public int CourtId;
        public string Name;
        public bool IsActive;
    }

    public sealed class CustodyRow
    {
        public int Id;
        public string Name;
        public int SortOrder;
        public bool IsActive;
    }

    public sealed class ReasonRow
    {
        public int Id;
        public string Label;
        public int SortOrder;
        public bool IsActive;
    }

    public interface ILookupStore
    {
        List<CourtRow> Courts(ISession s, bool activeOnly);
        CourtRow GetCourt(ISession s, int id);
        int InsertCourt(ISession s, string name);
        void RenameCourt(ISession s, int id, string name);
        void SetCourtActive(ISession s, int id, bool active);
        List<CircuitRow> Circuits(ISession s, int courtId, bool activeOnly);
        CircuitRow GetCircuit(ISession s, int id);
        int InsertCircuit(ISession s, int courtId, string name);
        void RenameCircuit(ISession s, int id, string name);
        void SetCircuitActive(ISession s, int id, bool active);
        List<CustodyRow> CustodyLocations(ISession s, bool activeOnly);
        int InsertCustody(ISession s, string name);
        void RenameCustody(ISession s, int id, string name);
        void SetCustodyActive(ISession s, int id, bool active);
        List<ReasonRow> Reasons(ISession s, bool activeOnly);
        int InsertReason(ISession s, string label);
        void RenameReason(ISession s, int id, string label);
        void SetReasonActive(ISession s, int id, bool active);
        void RecordChargeUse(ISession s, string text);
        List<string> SuggestCharges(ISession s, string prefix, int limit);
    }
}

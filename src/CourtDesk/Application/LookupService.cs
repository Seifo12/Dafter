using System.Collections.Generic;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;

namespace CourtDesk.Application
{
    public sealed class LookupService
    {
        private readonly IDatabase _db;
        private readonly ILookupStore _store;

        public LookupService(IDatabase db, ILookupStore store)
        {
            _db = db;
            _store = store;
        }

        public List<CourtRow> Courts(bool activeOnly)
        {
            return _db.Read(s => _store.Courts(s, activeOnly));
        }

        public List<CircuitRow> Circuits(int courtId, bool activeOnly)
        {
            return _db.Read(s => _store.Circuits(s, courtId, activeOnly));
        }

        public List<CustodyRow> CustodyLocations(bool activeOnly)
        {
            return _db.Read(s => _store.CustodyLocations(s, activeOnly));
        }

        public List<ReasonRow> Reasons(bool activeOnly)
        {
            return _db.Read(s => _store.Reasons(s, activeOnly));
        }

        public int AddCourt(string name)
        {
            string clean = Name(name);
            string norm = ArabicText.Normalize(clean);
            return _db.Write(s =>
            {
                foreach (CourtRow c in _store.Courts(s, false))
                {
                    if (ArabicText.Normalize(c.Name) == norm)
                    {
                        return c.Id;
                    }
                }
                return _store.InsertCourt(s, clean);
            });
        }

        public int AddCircuit(int courtId, string name)
        {
            string clean = Name(name);
            string norm = ArabicText.Normalize(clean);
            return _db.Write(s =>
            {
                foreach (CircuitRow c in _store.Circuits(s, courtId, false))
                {
                    if (ArabicText.Normalize(c.Name) == norm)
                    {
                        return c.Id;
                    }
                }
                return _store.InsertCircuit(s, courtId, clean);
            });
        }

        public int AddCustody(string name)
        {
            string clean = Name(name);
            string norm = ArabicText.Normalize(clean);
            return _db.Write(s =>
            {
                foreach (CustodyRow c in _store.CustodyLocations(s, false))
                {
                    if (ArabicText.Normalize(c.Name) == norm)
                    {
                        return c.Id;
                    }
                }
                return _store.InsertCustody(s, clean);
            });
        }

        public int AddReason(string label)
        {
            string clean = Name(label);
            string norm = ArabicText.Normalize(clean);
            return _db.Write(s =>
            {
                foreach (ReasonRow r in _store.Reasons(s, false))
                {
                    if (ArabicText.Normalize(r.Label) == norm)
                    {
                        return r.Id;
                    }
                }
                return _store.InsertReason(s, clean);
            });
        }

        public void RenameCourt(int id, string name)
        {
            _db.Write(s => { _store.RenameCourt(s, id, Name(name)); return 0; });
        }

        public void RenameCircuit(int id, string name)
        {
            _db.Write(s => { _store.RenameCircuit(s, id, Name(name)); return 0; });
        }

        public void RenameCustody(int id, string name)
        {
            _db.Write(s => { _store.RenameCustody(s, id, Name(name)); return 0; });
        }

        public void RenameReason(int id, string label)
        {
            _db.Write(s => { _store.RenameReason(s, id, Name(label)); return 0; });
        }

        public void SetCourtActive(int id, bool active)
        {
            _db.Write(s => { _store.SetCourtActive(s, id, active); return 0; });
        }

        public void SetCircuitActive(int id, bool active)
        {
            _db.Write(s => { _store.SetCircuitActive(s, id, active); return 0; });
        }

        public void SetCustodyActive(int id, bool active)
        {
            _db.Write(s => { _store.SetCustodyActive(s, id, active); return 0; });
        }

        public void SetReasonActive(int id, bool active)
        {
            _db.Write(s => { _store.SetReasonActive(s, id, active); return 0; });
        }

        public List<string> SuggestCharges(string prefix, int limit)
        {
            return _db.Read(s => _store.SuggestCharges(s, prefix, limit));
        }

        public void RecordChargeUse(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
            {
                return;
            }
            _db.Write(s => { _store.RecordChargeUse(s, text); return 0; });
        }

        private static string Name(string s)
        {
            if (s == null)
            {
                throw new System.ArgumentNullException("name");
            }
            string t = s.Trim();
            while (t.Contains("  "))
            {
                t = t.Replace("  ", " ");
            }
            if (t.Length < 1 || t.Length > 120)
            {
                throw new System.ArgumentException("name length");
            }
            return t;
        }
    }
}

namespace CourtDesk.Domain
{
    public sealed class DefendantSnapshot
    {
        public int Id;
        public string FullName;
        public string NationalId;
        public string CaseNumber;
        public int CaseYear;
        public string CaseType;
        public string Charges;
        public int? CourtId;
        public int? CircuitId;
        public int? CustodyLocationId;
        public LocalDate? SessionDate;
        public string Status;
        public LocalDate? DetainedSince;
        public string Notes;
        public bool IsDeleted;

        public static DefendantSnapshot FromDefendant(Defendant d)
        {
            return new DefendantSnapshot
            {
                Id = d.Id,
                FullName = d.FullName,
                NationalId = d.NationalId,
                CaseNumber = d.CaseNumber,
                CaseYear = d.CaseYear,
                CaseType = d.CaseType,
                Charges = d.Charges,
                CourtId = d.CourtId,
                CircuitId = d.CircuitId,
                CustodyLocationId = d.CustodyLocationId,
                SessionDate = d.SessionDate,
                Status = d.Status,
                DetainedSince = d.DetainedSince,
                Notes = d.Notes,
                IsDeleted = d.IsDeleted
            };
        }

        public void CopyTo(Defendant d)
        {
            d.FullName = FullName;
            d.NationalId = NationalId;
            d.CaseNumber = CaseNumber;
            d.CaseYear = CaseYear;
            d.CaseType = CaseType;
            d.Charges = Charges;
            d.CourtId = CourtId;
            d.CircuitId = CircuitId;
            d.CustodyLocationId = CustodyLocationId;
            d.SessionDate = SessionDate;
            d.Status = Status;
            d.DetainedSince = DetainedSince;
            d.Notes = Notes;
            d.IsDeleted = IsDeleted;
        }
    }
}

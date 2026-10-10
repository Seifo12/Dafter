namespace CourtDesk.Domain
{
    public sealed class Defendant
    {
        public int Id;
        public string FullName;
        public string NameNorm;
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
        public string PhotoPath;
        public bool IsDeleted;
        public int RowVersion;

        public Defendant Clone()
        {
            return (Defendant)MemberwiseClone();
        }
    }
}

namespace CourtDesk.Domain
{
    public static class DecisionRules
    {
        public const int FarDays = 120;
        public const int MaxDays = 1100;
        public const int MaxBail = 99999999;
        public const int MaxReasonLength = 200;
        public const int MaxNoteLength = 500;

        public static ValidationResult Validate(Defendant row, DecisionRequest req, LocalDate today)
        {
            var vr = new ValidationResult();
            if (req == null || !IsUuid(req.RequestUuid))
            {
                vr.Errors.Add(ValidationErrors.Request);
                return vr;
            }
            if (row == null || row.IsDeleted || req.ExpectedVersion != row.RowVersion)
            {
                vr.Errors.Add(ValidationErrors.Stale);
                return vr;
            }
            DefendantSnapshot snap = DefendantSnapshot.FromDefendant(row);
            if (!DecisionCodes.IsValid(req.Code))
            {
                vr.Errors.Add(ValidationErrors.Decision);
                return vr;
            }
            if (!AllowedFrom(snap.Status, req.Code))
            {
                vr.Errors.Add(ValidationErrors.Decision);
                return vr;
            }

            LocalDate held = today;
            if (snap.Status == DefendantStatuses.Detained)
            {
                if (!snap.SessionDate.HasValue)
                {
                    vr.Errors.Add(ValidationErrors.NoSession);
                    return vr;
                }
                if (snap.SessionDate.Value > today)
                {
                    vr.Errors.Add(ValidationErrors.FutureSession);
                    return vr;
                }
                held = snap.SessionDate.Value;
            }

            bool needsNext = DecisionCodes.RequiresNextDate(req.Code);
            if (needsNext && !req.NextDate.HasValue)
            {
                vr.Errors.Add(ValidationErrors.NextRequired);
            }
            if (!needsNext && req.NextDate.HasValue)
            {
                vr.Errors.Add(ValidationErrors.NextForbidden);
            }
            if (needsNext && req.NextDate.HasValue)
            {
                LocalDate next = req.NextDate.Value;
                if (req.Code == DecisionCodes.Reopen)
                {
                    if (next < today)
                    {
                        vr.Errors.Add(ValidationErrors.NextAfter);
                    }
                }
                else if (next <= held)
                {
                    vr.Errors.Add(ValidationErrors.NextAfter);
                }
                if (next < today)
                {
                    vr.Require(ConfirmKind.Past);
                }
                if (next.IsWeekend)
                {
                    vr.Require(ConfirmKind.Weekend);
                }
                int span = LocalDate.DaysBetween(held, next);
                if (span > MaxDays)
                {
                    vr.Errors.Add(ValidationErrors.TooFar);
                }
                else if (span > FarDays)
                {
                    vr.Require(ConfirmKind.Far);
                }
            }

            if (req.BailAmount.HasValue && req.Code != DecisionCodes.ReleaseBail)
            {
                vr.Errors.Add(ValidationErrors.Bail);
            }
            if (req.Code == DecisionCodes.ReleaseBail && req.BailAmount.HasValue
                && (req.BailAmount.Value < 0 || req.BailAmount.Value > MaxBail))
            {
                vr.Errors.Add(ValidationErrors.Bail);
            }
            if (!string.IsNullOrEmpty(req.ReasonText))
            {
                if (!DecisionCodes.AllowsReason(req.Code))
                {
                    vr.Errors.Add(ValidationErrors.Reason);
                }
                else if (req.ReasonText.Length > MaxReasonLength)
                {
                    vr.Errors.Add(ValidationErrors.Reason);
                }
            }
            if (!string.IsNullOrEmpty(req.Note) && req.Note.Length > MaxNoteLength)
            {
                vr.Errors.Add(ValidationErrors.Note);
            }
            if (req.CircuitId.HasValue && !req.CourtId.HasValue)
            {
                vr.Errors.Add(ValidationErrors.CircuitCourt);
            }

            if (vr.RequiredConfirmations.Contains(ConfirmKind.Past) && !req.ConfirmPast)
            {
                vr.Errors.Add(ValidationErrors.ConfirmPast);
            }
            if (vr.RequiredConfirmations.Contains(ConfirmKind.Weekend) && !req.ConfirmWeekend)
            {
                vr.Errors.Add(ValidationErrors.ConfirmWeekend);
            }
            if (vr.RequiredConfirmations.Contains(ConfirmKind.Far) && !req.ConfirmFar)
            {
                vr.Errors.Add(ValidationErrors.ConfirmFar);
            }
            return vr;
        }

        public static DefendantSnapshot Apply(DefendantSnapshot row, DecisionRequest req, LocalDate today)
        {
            var after = new DefendantSnapshot
            {
                Id = row.Id,
                FullName = row.FullName,
                NationalId = row.NationalId,
                CaseNumber = row.CaseNumber,
                CaseYear = row.CaseYear,
                CaseType = row.CaseType,
                Charges = row.Charges,
                CourtId = req.CourtId.HasValue ? req.CourtId : row.CourtId,
                CircuitId = req.CircuitId.HasValue ? req.CircuitId : row.CircuitId,
                CustodyLocationId = row.CustodyLocationId,
                Status = row.Status,
                SessionDate = row.SessionDate,
                DetainedSince = row.DetainedSince,
                Notes = row.Notes,
                IsDeleted = row.IsDeleted
            };
            switch (req.Code)
            {
                case DecisionCodes.RenewDetention:
                case DecisionCodes.Postpone:
                case DecisionCodes.Reopen:
                    after.Status = DefendantStatuses.Detained;
                    after.SessionDate = req.NextDate;
                    break;
                case DecisionCodes.Release:
                case DecisionCodes.ReleaseBail:
                    after.Status = DefendantStatuses.Released;
                    after.SessionDate = null;
                    break;
                case DecisionCodes.Judgment:
                    after.Status = DefendantStatuses.Judged;
                    after.SessionDate = null;
                    break;
            }
            return after;
        }

        private static bool AllowedFrom(string status, string code)
        {
            if (status == DefendantStatuses.Detained)
            {
                return code == DecisionCodes.RenewDetention || code == DecisionCodes.Postpone
                    || code == DecisionCodes.Release || code == DecisionCodes.ReleaseBail
                    || code == DecisionCodes.Judgment;
            }
            if (status == DefendantStatuses.Released || status == DefendantStatuses.Judged)
            {
                return code == DecisionCodes.Reopen;
            }
            return false;
        }

        private static bool IsUuid(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length != 32)
            {
                return false;
            }
            foreach (char c in s)
            {
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex)
                {
                    return false;
                }
            }
            return true;
        }
    }
}

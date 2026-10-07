using CourtDesk.Domain;
using Xunit;

namespace CourtDesk.Tests
{
    [Trait("Category", "Domain")]
    public sealed class DecisionRulesTests
    {
        private static readonly LocalDate Today = new LocalDate(2026, 10, 7);

        private static Defendant Detained(int version)
        {
            return new Defendant
            {
                Id = 1,
                FullName = "A B",
                NameNorm = "a b",
                NationalId = null,
                CaseNumber = "123",
                CaseYear = 2026,
                CaseType = "",
                Charges = "",
                CourtId = 1,
                CircuitId = null,
                CustodyLocationId = null,
                SessionDate = new LocalDate(2026, 10, 6),
                Status = DefendantStatuses.Detained,
                DetainedSince = new LocalDate(2026, 9, 1),
                Notes = "",
                IsDeleted = false,
                RowVersion = version
            };
        }

        private static DecisionRequest Req(string code, LocalDate? next)
        {
            return new DecisionRequest
            {
                RequestUuid = new string('a', 32),
                DefendantId = 1,
                ExpectedVersion = 3,
                Code = code,
                NextDate = next
            };
        }

        [Fact]
        public void ValidRenewPasses()
        {
            ValidationResult vr = DecisionRules.Validate(Detained(3), Req(DecisionCodes.RenewDetention, new LocalDate(2026, 10, 20)), Today);
            Assert.False(vr.HasErrors);
            Assert.Empty(vr.RequiredConfirmations);
        }

        [Fact]
        public void EachDecisionValidFromItsOwnStatus()
        {
            Assert.False(DecisionRules.Validate(Detained(3), Req(DecisionCodes.Postpone, new LocalDate(2026, 10, 20)), Today).HasErrors);
            Assert.False(DecisionRules.Validate(Detained(3), Req(DecisionCodes.Release, null), Today).HasErrors);
            Assert.False(DecisionRules.Validate(Detained(3), Req(DecisionCodes.Judgment, null), Today).HasErrors);
            Defendant rel = Detained(3);
            rel.Status = DefendantStatuses.Released;
            rel.SessionDate = null;
            Assert.False(DecisionRules.Validate(rel, Req(DecisionCodes.Reopen, new LocalDate(2026, 10, 20)), Today).HasErrors);
            Defendant jud = Detained(3);
            jud.Status = DefendantStatuses.Judged;
            jud.SessionDate = null;
            Assert.False(DecisionRules.Validate(jud, Req(DecisionCodes.Reopen, new LocalDate(2026, 10, 20)), Today).HasErrors);
        }

        [Fact]
        public void WrongStatusDecisionRejected()
        {
            Assert.Contains(ValidationErrors.Decision,
                DecisionRules.Validate(Detained(3), Req(DecisionCodes.Reopen, new LocalDate(2026, 10, 20)), Today).Errors);
            Defendant rel = Detained(3);
            rel.Status = DefendantStatuses.Released;
            rel.SessionDate = null;
            Assert.Contains(ValidationErrors.Decision,
                DecisionRules.Validate(rel, Req(DecisionCodes.RenewDetention, new LocalDate(2026, 10, 20)), Today).Errors);
            Assert.Contains(ValidationErrors.Decision,
                DecisionRules.Validate(Detained(3), Req("UNKNOWN", null), Today).Errors);
        }

        [Fact]
        public void StaleChecks()
        {
            Assert.Contains(ValidationErrors.Stale, DecisionRules.Validate(null, Req(DecisionCodes.Release, null), Today).Errors);
            Assert.Contains(ValidationErrors.Stale,
                DecisionRules.Validate(Detained(9), Req(DecisionCodes.Release, null), Today).Errors);
            Defendant gone = Detained(3);
            gone.IsDeleted = true;
            Assert.Contains(ValidationErrors.Stale, DecisionRules.Validate(gone, Req(DecisionCodes.Release, null), Today).Errors);
        }

        [Fact]
        public void BadRequestUuidRejected()
        {
            DecisionRequest r = Req(DecisionCodes.Release, null);
            r.RequestUuid = "xyz";
            Assert.Contains(ValidationErrors.Request, DecisionRules.Validate(Detained(3), r, Today).Errors);
            r.RequestUuid = null;
            Assert.Contains(ValidationErrors.Request, DecisionRules.Validate(Detained(3), r, Today).Errors);
        }

        [Fact]
        public void NoSessionAndFutureSessionBlocked()
        {
            Defendant d = Detained(3);
            d.SessionDate = null;
            Assert.Contains(ValidationErrors.NoSession,
                DecisionRules.Validate(d, Req(DecisionCodes.RenewDetention, new LocalDate(2026, 10, 20)), Today).Errors);
            d.SessionDate = new LocalDate(2026, 10, 8);
            Assert.Contains(ValidationErrors.FutureSession,
                DecisionRules.Validate(d, Req(DecisionCodes.RenewDetention, new LocalDate(2026, 10, 20)), Today).Errors);
        }

        [Fact]
        public void NextDateRequiredAndForbidden()
        {
            Assert.Contains(ValidationErrors.NextRequired,
                DecisionRules.Validate(Detained(3), Req(DecisionCodes.RenewDetention, null), Today).Errors);
            Assert.Contains(ValidationErrors.NextRequired,
                DecisionRules.Validate(Detained(3), Req(DecisionCodes.Postpone, null), Today).Errors);
            Assert.Contains(ValidationErrors.NextForbidden,
                DecisionRules.Validate(Detained(3), Req(DecisionCodes.Release, new LocalDate(2026, 10, 20)), Today).Errors);
            Assert.Contains(ValidationErrors.NextForbidden,
                DecisionRules.Validate(Detained(3), Req(DecisionCodes.Judgment, new LocalDate(2026, 10, 20)), Today).Errors);
        }

        [Fact]
        public void NextMustBeAfterHeld()
        {
            Assert.Contains(ValidationErrors.NextAfter,
                DecisionRules.Validate(Detained(3), Req(DecisionCodes.RenewDetention, new LocalDate(2026, 10, 6)), Today).Errors);
            Assert.Contains(ValidationErrors.NextAfter,
                DecisionRules.Validate(Detained(3), Req(DecisionCodes.Postpone, new LocalDate(2026, 10, 1)), Today).Errors);
        }

        [Fact]
        public void ReopenNextMustBeTodayOrLater()
        {
            Defendant rel = Detained(3);
            rel.Status = DefendantStatuses.Released;
            rel.SessionDate = null;
            Assert.Contains(ValidationErrors.NextAfter,
                DecisionRules.Validate(rel, Req(DecisionCodes.Reopen, new LocalDate(2026, 10, 6)), Today).Errors);
            Assert.False(DecisionRules.Validate(rel, Req(DecisionCodes.Reopen, new LocalDate(2026, 10, 7)), Today).HasErrors);
        }

        [Fact]
        public void PastDateNeedsConfirmation()
        {
            Defendant d = Detained(3);
            d.SessionDate = new LocalDate(2026, 9, 1);
            DecisionRequest r = Req(DecisionCodes.RenewDetention, new LocalDate(2026, 10, 5));
            ValidationResult vr = DecisionRules.Validate(d, r, Today);
            Assert.Contains(ConfirmKind.Past, vr.RequiredConfirmations);
            Assert.Contains(ValidationErrors.ConfirmPast, vr.Errors);
            r.ConfirmPast = true;
            Assert.False(DecisionRules.Validate(d, r, Today).HasErrors);
        }

        [Fact]
        public void WeekendNeedsConfirmation()
        {
            DecisionRequest r = Req(DecisionCodes.RenewDetention, new LocalDate(2026, 10, 9));
            ValidationResult vr = DecisionRules.Validate(Detained(3), r, Today);
            Assert.Contains(ConfirmKind.Weekend, vr.RequiredConfirmations);
            Assert.Contains(ValidationErrors.ConfirmWeekend, vr.Errors);
            r.ConfirmWeekend = true;
            Assert.False(DecisionRules.Validate(Detained(3), r, Today).HasErrors);
        }

        [Fact]
        public void FarDateNeedsConfirmationTooFarIsHardError()
        {
            DecisionRequest r = Req(DecisionCodes.RenewDetention, new LocalDate(2027, 2, 4));
            ValidationResult vr = DecisionRules.Validate(Detained(3), r, Today);
            Assert.Contains(ConfirmKind.Far, vr.RequiredConfirmations);
            r.ConfirmFar = true;
            r.ConfirmWeekend = true;
            Assert.False(DecisionRules.Validate(Detained(3), r, Today).HasErrors);

            Defendant d = Detained(3);
            d.SessionDate = new LocalDate(2023, 6, 1);
            Assert.Contains(ValidationErrors.TooFar,
                DecisionRules.Validate(d, Req(DecisionCodes.RenewDetention, new LocalDate(2026, 10, 7)), Today).Errors);
        }

        [Fact]
        public void BailRules()
        {
            DecisionRequest ok = Req(DecisionCodes.ReleaseBail, null);
            ok.BailAmount = 5000;
            Assert.False(DecisionRules.Validate(Detained(3), ok, Today).HasErrors);

            DecisionRequest neg = Req(DecisionCodes.ReleaseBail, null);
            neg.BailAmount = -1;
            Assert.Contains(ValidationErrors.Bail, DecisionRules.Validate(Detained(3), neg, Today).Errors);

            DecisionRequest huge = Req(DecisionCodes.ReleaseBail, null);
            huge.BailAmount = 100000000;
            Assert.Contains(ValidationErrors.Bail, DecisionRules.Validate(Detained(3), huge, Today).Errors);

            DecisionRequest wrong = Req(DecisionCodes.Release, null);
            wrong.BailAmount = 100;
            Assert.Contains(ValidationErrors.Bail, DecisionRules.Validate(Detained(3), wrong, Today).Errors);
        }

        [Fact]
        public void ReasonAndNoteRules()
        {
            DecisionRequest longReason = Req(DecisionCodes.RenewDetention, new LocalDate(2026, 10, 20));
            longReason.ReasonText = new string('x', 201);
            Assert.Contains(ValidationErrors.Reason, DecisionRules.Validate(Detained(3), longReason, Today).Errors);

            DecisionRequest reasonOnRelease = Req(DecisionCodes.Release, null);
            reasonOnRelease.ReasonText = "x";
            Assert.Contains(ValidationErrors.Reason, DecisionRules.Validate(Detained(3), reasonOnRelease, Today).Errors);

            DecisionRequest longNote = Req(DecisionCodes.Release, null);
            longNote.Note = new string('x', 501);
            Assert.Contains(ValidationErrors.Note, DecisionRules.Validate(Detained(3), longNote, Today).Errors);
        }

        [Fact]
        public void CircuitWithoutCourtRejected()
        {
            DecisionRequest r = Req(DecisionCodes.RenewDetention, new LocalDate(2026, 10, 20));
            r.CircuitId = 5;
            Assert.Contains(ValidationErrors.CircuitCourt, DecisionRules.Validate(Detained(3), r, Today).Errors);
            r.CourtId = 2;
            Assert.False(DecisionRules.Validate(Detained(3), r, Today).HasErrors);
        }

        [Fact]
        public void ApplyMovesStateAndDate()
        {
            DefendantSnapshot before = DefendantSnapshot.FromDefendant(Detained(3));

            DefendantSnapshot renew = DecisionRules.Apply(before,
                Req(DecisionCodes.RenewDetention, new LocalDate(2026, 10, 20)), Today);
            Assert.Equal(DefendantStatuses.Detained, renew.Status);
            Assert.Equal(new LocalDate(2026, 10, 20), renew.SessionDate);
            Assert.Equal(before.FullName, renew.FullName);

            DefendantSnapshot rel = DecisionRules.Apply(before, Req(DecisionCodes.Release, null), Today);
            Assert.Equal(DefendantStatuses.Released, rel.Status);
            Assert.False(rel.SessionDate.HasValue);

            DefendantSnapshot jud = DecisionRules.Apply(before, Req(DecisionCodes.Judgment, null), Today);
            Assert.Equal(DefendantStatuses.Judged, jud.Status);
            Assert.False(jud.SessionDate.HasValue);

            DecisionRequest court = Req(DecisionCodes.Postpone, new LocalDate(2026, 10, 20));
            court.CourtId = 9;
            court.CircuitId = 8;
            DefendantSnapshot moved = DecisionRules.Apply(before, court, Today);
            Assert.Equal(9, moved.CourtId);
            Assert.Equal(8, moved.CircuitId);
        }
    }
}

using System.Collections.Generic;

namespace CourtDesk.Domain
{
    public enum ConfirmKind
    {
        Past,
        Weekend,
        Far
    }

    public static class ValidationErrors
    {
        public const string Request = "err.request";
        public const string Stale = "err.stale";
        public const string Decision = "err.decision";
        public const string NoSession = "err.no_session";
        public const string FutureSession = "err.future_session";
        public const string NextRequired = "err.next_required";
        public const string NextForbidden = "err.next_forbidden";
        public const string NextAfter = "err.next_after";
        public const string TooFar = "err.too_far";
        public const string ConfirmPast = "err.confirm_past";
        public const string ConfirmWeekend = "err.confirm_weekend";
        public const string ConfirmFar = "err.confirm_far";
        public const string Bail = "err.bail";
        public const string Reason = "err.reason";
        public const string Note = "err.note";
        public const string CircuitCourt = "err.circuit_court";
    }

    public sealed class ValidationResult
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<ConfirmKind> RequiredConfirmations = new List<ConfirmKind>();

        public bool HasErrors
        {
            get { return Errors.Count > 0; }
        }

        public void Require(ConfirmKind kind)
        {
            if (!RequiredConfirmations.Contains(kind))
            {
                RequiredConfirmations.Add(kind);
            }
        }
    }
}

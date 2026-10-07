using System;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;

namespace CourtDesk.Persistence
{
    public static class ErrorCodes
    {
        public const string DbWrite = "E-DB-001";
        public const string DbBusy = "E-DB-002";
        public const string DbCorrupt = "E-DB-003";
        public const string DbNewer = "E-DB-004";
        public const string DbNotOurs = "E-DB-005";
        public const string DbBackup = "E-BAK-001";
        public const string DbRestore = "E-BAK-002";
        public const string DbState = "E-STA-001";
    }

    public class DbException : Exception
    {
        public readonly string Code;

        public DbException(string code, string message) : base(message)
        {
            Code = code;
        }

        public DbException(string code, string message, Exception inner) : base(message, inner)
        {
            Code = code;
        }
    }

    public sealed class BusyException : DbException
    {
        public BusyException() : base(ErrorCodes.DbBusy, "busy") { }
    }

    public sealed class CorruptDatabaseException : DbException
    {
        public CorruptDatabaseException(string detail) : base(ErrorCodes.DbCorrupt, "corrupt: " + detail) { }
    }

    public sealed class UnsupportedVersionException : DbException
    {
        public UnsupportedVersionException(int found)
            : base(ErrorCodes.DbNewer, "user_version " + found + " newer than supported " + DbPaths.SupportedSchemaVersion) { }
    }

    public sealed class NotOurDatabaseException : DbException
    {
        public NotOurDatabaseException() : base(ErrorCodes.DbNotOurs, "application_id mismatch") { }
    }

    public sealed class NoBackupException : DbException
    {
        public NoBackupException() : base(ErrorCodes.DbRestore, "no verified backup") { }
    }

    public sealed class ConcurrencyException : Exception
    {
        public ConcurrencyException() : base("concurrency") { }
    }

    public sealed class ValidationException : Exception
    {
        public readonly ValidationResult Result;

        public ValidationException(ValidationResult result)
            : base("validation: " + string.Join(",", result.Errors.ToArray()))
        {
            Result = result;
        }
    }

    public sealed class InvariantException : Exception
    {
        public InvariantException(string detail) : base("invariant: " + detail) { }
    }
}

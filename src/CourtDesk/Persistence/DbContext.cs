using System.Data.SQLite;
using CourtDesk.Application.Ports;

namespace CourtDesk.Persistence
{
    public class DbContext : ISession
    {
        internal readonly SQLiteConnection Connection;
        public readonly string UtcNowIso;
        public readonly string LocalDateIso;
        public readonly bool InWrite;

        internal DbContext(SQLiteConnection connection, string utcNowIso, string localDateIso, bool inWrite)
        {
            Connection = connection;
            UtcNowIso = utcNowIso;
            LocalDateIso = localDateIso;
            InWrite = inWrite;
        }
    }

    public sealed class WriteTx : DbContext
    {
        internal WriteTx(SQLiteConnection connection, string utcNowIso, string localDateIso)
            : base(connection, utcNowIso, localDateIso, true)
        {
        }
    }
}

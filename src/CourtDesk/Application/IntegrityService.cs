using System.Collections.Generic;
using CourtDesk.Application.Ports;

namespace CourtDesk.Application
{
    public sealed class IntegrityService
    {
        private readonly IDatabase _db;
        private readonly IBackupEngine _engine;
        private readonly ILogger _log;

        public IntegrityService(IDatabase db, IBackupEngine engine, ILogger log)
        {
            _db = db;
            _engine = engine;
            _log = log;
        }

        public List<string> Check()
        {
            var source = _db as IChangeSeqSource;
            return source == null
                ? new List<string>()
                : source.CheckInvariants();
        }

        public void LogWarnings(List<string> warnings)
        {
            foreach (string w in warnings)
            {
                _log.Warn("E-STA-010", w);
            }
        }

        public BackupReport VerifyLatestSnapshot(string destPath)
        {
            return _engine.Snapshot(destPath);
        }
    }
}

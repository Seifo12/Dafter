namespace CourtDesk.Application.Ports
{
    public sealed class BackupReport
    {
        public string Path;
        public long SizeBytes;
        public string Sha256;
        public long DefendantCount;
        public long EventCount;
    }

    public interface IBackupEngine
    {
        BackupReport Snapshot(string destFinalPath);
    }
}

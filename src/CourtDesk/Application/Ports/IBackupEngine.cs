namespace CourtDesk.Application.Ports
{
    public sealed class BackupReport
    {
        public string Path { get; set; }
        public long SizeBytes { get; set; }
        public string Sha256 { get; set; }
        public long DefendantCount { get; set; }
        public long EventCount { get; set; }
    }

    public interface IBackupEngine
    {
        BackupReport Snapshot(string destFinalPath);
    }
}

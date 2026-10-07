namespace CourtDesk.Application.Ports
{
    public sealed class DriveItem
    {
        public string Name;
        public string VolumeLabel;
        public DriveKind Kind;
        public long TotalBytes;
        public long FreeBytes;
    }

    public enum DriveKind
    {
        Removable,
        Other
    }

    public interface IDriveProvider
    {
        DriveItem[] List();
        DriveItem Find(string name);
    }
}

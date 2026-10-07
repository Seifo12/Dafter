using System;
using System.IO;
using CourtDesk.Application.Ports;

namespace CourtDesk.Platform
{
    public sealed class PortsDriveProvider : IDriveProvider
    {
        public DriveItem[] List()
        {
            var outp = new System.Collections.Generic.List<DriveItem>();
            DriveInfo[] drives;
            try
            {
                drives = DriveInfo.GetDrives();
            }
            catch (IOException)
            {
                return outp.ToArray();
            }
            catch (UnauthorizedAccessException)
            {
                return outp.ToArray();
            }
            foreach (DriveInfo d in drives)
            {
                DriveItem item = new DriveItem { Name = d.Name };
                try
                {
                    item.Kind = d.DriveType == DriveType.Removable ? DriveKind.Removable : DriveKind.Other;
                    if (d.IsReady)
                    {
                        item.VolumeLabel = d.VolumeLabel;
                        item.TotalBytes = d.TotalSize;
                        item.FreeBytes = d.AvailableFreeSpace;
                    }
                }
                catch (IOException)
                {
                    item.Kind = DriveKind.Other;
                }
                catch (UnauthorizedAccessException)
                {
                }
                outp.Add(item);
            }
            return outp.ToArray();
        }

        public DriveItem Find(string name)
        {
            foreach (DriveItem d in List())
            {
                if (string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return d;
                }
            }
            return null;
        }
    }
}

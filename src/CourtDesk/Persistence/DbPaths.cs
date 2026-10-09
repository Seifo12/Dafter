using System;
using System.Globalization;
using System.IO;
namespace CourtDesk.Persistence
{
    public static class DbPaths
    {
        public const string DbFileName = "courtdesk.db";
        public const int SupportedSchemaVersion = 2;

        public static string ResolveHome(string homeArg)
        {
            if (!string.IsNullOrEmpty(homeArg))
            {
                return Path.GetFullPath(homeArg);
            }
            string env = Environment.GetEnvironmentVariable("COURTDESK_HOME");
            if (!string.IsNullOrEmpty(env))
            {
                return Path.GetFullPath(env);
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CourtDesk");
        }

        public static string Db(string home) { return Path.Combine(home, "db", DbFileName); }
        public static string AutoDir(string home) { return Path.Combine(home, "backups", "auto"); }
        public static string ExitDir(string home) { return Path.Combine(home, "backups", "exit"); }
        public static string PreOpDir(string home) { return Path.Combine(home, "backups", "pre-op"); }
        public static string QuarantineDir(string home) { return Path.Combine(home, "quarantine"); }
        public static string NativeDir(string home) { return Path.Combine(home, "native", "x86"); }
        public static string TmpDir(string home) { return Path.Combine(home, "tmp"); }
        public static string MedicalDir(string home) { return Path.Combine(home, "medical"); }
        public static string MedicalImagesDir(string home) { return Path.Combine(MedicalDir(home), "images"); }
        public static string DefendantMedicalDir(string home, long defendantId)
        {
            return Path.Combine(MedicalImagesDir(home), defendantId.ToString(CultureInfo.InvariantCulture));
        }
        public static string LogsDir(string home) { return Path.Combine(home, "logs"); }
        public static string RestoreMarker(string home) { return Path.Combine(home, "restore.pending"); }

        public static void EnsureAll(string home)
        {
            foreach (string d in new string[]
            {
                Path.Combine(home, "db"), AutoDir(home), ExitDir(home), PreOpDir(home),
                QuarantineDir(home), NativeDir(home), TmpDir(home), LogsDir(home),
                MedicalDir(home), MedicalImagesDir(home)
            })
            {
                if (!Directory.Exists(d))
                {
                    Directory.CreateDirectory(d);
                }
            }
        }

        public static void CleanTmp(string home)
        {
            string tmp = TmpDir(home);
            if (!Directory.Exists(tmp))
            {
                return;
            }
            foreach (string f in Directory.GetFiles(tmp))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext == ".tmp" || ext == ".partial")
                {
                    try { File.Delete(f); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }

        public static void VolumeInfo(string home, out bool isNtfs, out long freeBytes)
        {
            isNtfs = false;
            freeBytes = 0;
            try
            {
                string root = Path.GetPathRoot(Path.GetFullPath(home));
                foreach (DriveInfo di in DriveInfo.GetDrives())
                {
                    if (string.Equals(di.Name, root, StringComparison.OrdinalIgnoreCase))
                    {
                        isNtfs = string.Equals(di.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase);
                        if (di.IsReady)
                        {
                            freeBytes = di.AvailableFreeSpace;
                        }
                        return;
                    }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}

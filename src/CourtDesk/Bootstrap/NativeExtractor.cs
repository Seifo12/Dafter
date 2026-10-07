using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace CourtDesk.Bootstrap
{
    public sealed class NativeExtractReport
    {
        public bool Ok;
        public string Detail = "";
        public readonly List<string> Written = new List<string>();
        public readonly List<string> Verified = new List<string>();
    }

    public static class NativeExtractor
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string path);

        public static string Sha256ManagedSqlite { get { return "9b0581b003161d1605405ab4ae2a31e03bf3287673c148f4a1d90253aaad2c30"; } }

        public static string Sha256NativeSqlite { get { return "70e48ef5c14766f3601c97451b47859fddcbe7f237e1c5200cea8e7a7609d809"; } }

        public static NativeExtractReport Extract(string home)
        {
            var report = new NativeExtractReport();
            try
            {
                string nativeDir = Persistence.DbPaths.NativeDir(home);
                Directory.CreateDirectory(nativeDir);
                string interop = Path.Combine(nativeDir, "SQLite.Interop.dll");
                Write("vendor/x86/SQLite.Interop.dll", interop, Sha256NativeSqlite, report);
                SetDllDirectory(nativeDir);
                report.Verified.Add(interop);
                PlaceNextToExecutable(report);
                report.Ok = true;
            }
            catch (Exception e)
            {
                report.Ok = false;
                report.Detail = e.GetType().Name + ": " + e.Message;
            }
            return report;
        }

        /// <summary>
        /// System.Data.SQLite builds the native DLL path from the managed assembly
        /// location, which is empty when the assembly is loaded from the embedded
        /// resource, so the native library must also sit under the exe folder.
        /// </summary>
        public static void PlaceNextToExecutable(NativeExtractReport report)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                if (string.IsNullOrEmpty(baseDir))
                {
                    return;
                }
                string x86 = Path.Combine(baseDir, "x86");
                if (!Directory.Exists(x86))
                {
                    Directory.CreateDirectory(x86);
                }
                string dest = Path.Combine(x86, "SQLite.Interop.dll");
                if (!File.Exists(dest) || Hash(dest) != Sha256NativeSqlite)
                {
                    Assembly self = Assembly.GetExecutingAssembly();
                    using (Stream s = self.GetManifestResourceStream("vendor/x86/SQLite.Interop.dll"))
                    {
                        if (s == null)
                        {
                            return;
                        }
                        string tmp = dest + ".tmp";
                        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            s.CopyTo(fs);
                            fs.Flush(true);
                        }
                        if (Hash(tmp) != Sha256NativeSqlite)
                        {
                            File.Delete(tmp);
                            return;
                        }
                        if (File.Exists(dest))
                        {
                            File.Delete(dest);
                        }
                        File.Move(tmp, dest);
                    }
                }
                report.Verified.Add(dest);
            }
            catch (Exception e)
            {
                report.Detail = report.Detail.Length == 0
                    ? ("native next-to-exe: " + e.Message)
                    : report.Detail;
            }
        }

        private static void Write(string resourceName, string destPath, string expectedSha,
            NativeExtractReport report)
        {
            if (File.Exists(destPath))
            {
                try
                {
                    if (Hash(destPath) == expectedSha)
                    {
                        report.Verified.Add(destPath);
                        return;
                    }
                }
                catch (IOException)
                {
                }
            }
            Assembly self = Assembly.GetExecutingAssembly();
            using (Stream s = self.GetManifestResourceStream(resourceName))
            {
                if (s == null)
                {
                    throw new InvalidOperationException("embedded resource missing: " + resourceName);
                }
                string tmp = destPath + ".tmp";
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    s.CopyTo(fs);
                    fs.Flush(true);
                }
                string got = Hash(tmp);
                if (got != expectedSha)
                {
                    File.Delete(tmp);
                    throw new InvalidOperationException("sha256 mismatch for " + resourceName
                        + ": got " + got);
                }
                if (File.Exists(destPath))
                {
                    File.Delete(destPath);
                }
                File.Move(tmp, destPath);
                report.Written.Add(destPath);
            }
        }

        public static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Hex(sha.ComputeHash(fs));
            }
        }

        public static string Hex(byte[] bytes)
        {
            var sb = new System.Text.StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }
    }
}
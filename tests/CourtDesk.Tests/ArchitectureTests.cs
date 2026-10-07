using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace CourtDesk.Tests
{
    [Trait("Category", "Arch")]
    public sealed class ArchitectureTests
    {
        private static string RepoRoot()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, "CourtDesk.sln")))
                {
                    return dir;
                }
                dir = Path.GetDirectoryName(dir);
            }
            throw new InvalidOperationException("repo root not found");
        }

        private static IEnumerable<string> SourceFiles(string ext)
        {
            string src = Path.Combine(RepoRoot(), "src", "CourtDesk");
            foreach (string f in Directory.GetFiles(src, "*" + ext, SearchOption.AllDirectories))
            {
                if (f.IndexOf("\\obj\\", StringComparison.OrdinalIgnoreCase) >= 0
                    || f.IndexOf("\\bin\\", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }
                yield return f;
            }
        }

        private static bool IsStringsFile(string path)
        {
            return path.EndsWith(Path.Combine("Resources", "Strings.cs"), StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsArabic(string text)
        {
            foreach (char c in text)
            {
                if ((c >= '\u0600' && c <= '\u06FF')
                    || (c >= '\u0750' && c <= '\u077F')
                    || (c >= '\uFB50' && c <= '\uFDFF')
                    || (c >= '\uFE70' && c <= '\uFEFF'))
                {
                    return true;
                }
            }
            return false;
        }

        [Fact]
        public void NoNetworkApisInProduct()
        {
            string[] banned = new string[]
            {
                "System.Net", "HttpClient", "WebClient", "WebRequest",
                "TcpClient", "UdpClient", "Socket", "HttpListener"
            };
            var hits = new List<string>();
            foreach (string f in SourceFiles(".cs"))
            {
                string text = File.ReadAllText(f);
                foreach (string b in banned)
                {
                    if (text.IndexOf(b, StringComparison.Ordinal) >= 0)
                    {
                        hits.Add(Path.GetFileName(f) + ": " + b);
                    }
                }
            }
            Assert.True(hits.Count == 0, "network API references: " + string.Join("; ", hits));
        }

        [Fact]
        public void NoArabicLiteralsOutsideStrings()
        {
            var hits = new List<string>();
            foreach (string f in SourceFiles(".cs"))
            {
                if (IsStringsFile(f))
                {
                    continue;
                }
                if (ContainsArabic(File.ReadAllText(f)))
                {
                    hits.Add(f);
                }
            }
            foreach (string f in SourceFiles(".xaml"))
            {
                if (ContainsArabic(File.ReadAllText(f)))
                {
                    hits.Add(f);
                }
            }
            Assert.True(hits.Count == 0, "arabic outside Strings.cs: " + string.Join("; ", hits));
        }

        [Fact]
        public void NoSqliteOutsidePersistenceAndBootstrap()
        {
            var hits = new List<string>();
            foreach (string f in SourceFiles(".cs"))
            {
                string rel = f.Substring(Path.Combine(RepoRoot(), "src", "CourtDesk").Length);
                if (rel.IndexOf("Persistence", StringComparison.OrdinalIgnoreCase) >= 0
                    || rel.IndexOf("Bootstrap", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }
                if (File.ReadAllText(f).IndexOf("System.Data.SQLite", StringComparison.Ordinal) >= 0)
                {
                    hits.Add(f);
                }
            }
            Assert.True(hits.Count == 0, "sqlite refs outside Persistence/Bootstrap: " + string.Join("; ", hits));
        }

        [Fact]
        public void NoDirectClockOutsideSystemClock()
        {
            string[] banned = new string[] { "DateTime.Now", "DateTime.Today", "DateTime.UtcNow" };
            var hits = new List<string>();
            foreach (string f in SourceFiles(".cs"))
            {
                if (f.EndsWith("SystemClock.cs", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string text = File.ReadAllText(f);
                foreach (string b in banned)
                {
                    if (text.IndexOf(b, StringComparison.Ordinal) >= 0)
                    {
                        hits.Add(Path.GetFileName(f) + ": " + b);
                    }
                }
            }
            Assert.True(hits.Count == 0, "direct clock uses: " + string.Join("; ", hits));
        }

        [Fact]
        public void NoFileCopyInProduct()
        {
            var hits = new List<string>();
            foreach (string f in SourceFiles(".cs"))
            {
                if (File.ReadAllText(f).IndexOf("File.Copy", StringComparison.Ordinal) >= 0)
                {
                    hits.Add(f);
                }
            }
            Assert.True(hits.Count == 0, "File.Copy in product (live DB must never be copied): " + string.Join("; ", hits));
        }

        [Fact]
        public void NativeInteropImportAllowList()
        {
            string dll = Path.Combine(RepoRoot(), "vendor", "native", "x86", "SQLite.Interop.dll");
            Assert.True(File.Exists(dll), "vendored native dll missing: " + dll);
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "kernel32", "user32", "advapi32", "ole32", "oleaut32", "shell32", "ntdll", "msvcrt",
                // SPEC 9 intent is "no VC++ redistributable dependency". The genuine upstream 1.0.118.0
                // interop (verified strong-name db937bc2d44ff139, no CRT imports) additionally imports
                // mscoree!CorBindToRuntimeEx and WINTRUST!WinVerifyTrust (upstream CLR/signature checks).
                // Both are Windows-inbox on every supported target (SPEC 2.5): WINTRUST always,
                // mscoree wherever .NET 4.x is installed (hard prerequisite). See DECISIONS.md.
                "mscoree", "wintrust"
            };
            List<string> imports = PeImportReader.ReadImportDllNames(dll);
            Assert.True(imports.Count > 0, "could not parse imports of " + dll);
            var bad = new List<string>();
            foreach (string name in imports)
            {
                string bare = name;
                if (bare.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    bare = bare.Substring(0, bare.Length - 4);
                }
                if (!allowed.Contains(bare))
                {
                    bad.Add(name);
                }
            }
            Assert.True(bad.Count == 0, "non-allowlisted native imports: " + string.Join("; ", bad));
        }
    }

    internal static class PeImportReader
    {
        public static List<string> ReadImportDllNames(string path)
        {
            byte[] img = File.ReadAllBytes(path);
            var names = new List<string>();
            if (img.Length < 64 || img[0] != (byte)'M' || img[1] != (byte)'Z')
            {
                return names;
            }
            int pe = ReadI32(img, 60);
            if (pe <= 0 || pe + 6 > img.Length || img[pe] != (byte)'P' || img[pe + 1] != (byte)'E')
            {
                return names;
            }
            int coff = pe + 4;
            int numSections = ReadU16(img, coff + 2);
            int optSize = ReadU16(img, coff + 16);
            int opt = coff + 20;
            if (opt + optSize > img.Length)
            {
                return names;
            }
            ushort magic = (ushort)ReadU16(img, opt);
            int dirBase;
            if (magic == 0x10b)
            {
                dirBase = opt + 96;
            }
            else if (magic == 0x20b)
            {
                dirBase = opt + 112;
            }
            else
            {
                return names;
            }
            int importRva = ReadI32(img, dirBase + 8);
            int importSize = ReadI32(img, dirBase + 12);
            if (importRva == 0 || importSize == 0)
            {
                return names;
            }
            int secBase = opt + optSize;
            var sections = new List<Section>();
            for (int i = 0; i < numSections; i++)
            {
                int s = secBase + i * 40;
                if (s + 40 > img.Length)
                {
                    break;
                }
                sections.Add(new Section
                {
                    VAddr = ReadI32(img, s + 12),
                    VSize = ReadI32(img, s + 8),
                    Raw = ReadI32(img, s + 20),
                    RawSize = ReadI32(img, s + 16)
                });
            }
            int desc = RvaToOffset(importRva, sections);
            if (desc < 0)
            {
                return names;
            }
            for (int k = 0; k < 256; k++)
            {
                int off = desc + k * 20;
                if (off + 20 > img.Length)
                {
                    break;
                }
                int nameRva = ReadI32(img, off + 12);
                int firstThunk = ReadI32(img, off + 16);
                if (nameRva == 0 && firstThunk == 0)
                {
                    break;
                }
                if (nameRva != 0)
                {
                    int noff = RvaToOffset(nameRva, sections);
                    if (noff >= 0)
                    {
                        names.Add(ReadCString(img, noff));
                    }
                }
            }
            return names;
        }

        private sealed class Section
        {
            public int VAddr;
            public int VSize;
            public int Raw;
            public int RawSize;
        }

        private static int RvaToOffset(int rva, List<Section> sections)
        {
            foreach (Section s in sections)
            {
                int size = Math.Max(s.VSize, s.RawSize);
                if (rva >= s.VAddr && rva < s.VAddr + size)
                {
                    return s.Raw + (rva - s.VAddr);
                }
            }
            return -1;
        }

        private static string ReadCString(byte[] img, int off)
        {
            var sb = new StringBuilder();
            while (off < img.Length && img[off] != 0)
            {
                sb.Append((char)img[off]);
                off++;
            }
            return sb.ToString();
        }

        private static int ReadI32(byte[] img, int off)
        {
            return img[off] | (img[off + 1] << 8) | (img[off + 2] << 16) | (img[off + 3] << 24);
        }

        private static int ReadU16(byte[] img, int off)
        {
            return img[off] | (img[off + 1] << 8);
        }
    }
}

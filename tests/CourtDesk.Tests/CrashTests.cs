using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;
using CourtDesk.Persistence;
using Xunit;

namespace CourtDesk.Tests
{
    [Trait("Category", "Crash")]
    public sealed class CrashTests
    {
        private static string FindHarnessExe()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            string root = null;
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, "CourtDesk.sln")))
                {
                    root = dir;
                    break;
                }
                dir = Path.GetDirectoryName(dir);
            }
            if (root == null)
            {
                throw new InvalidOperationException("repo root not found");
            }
            string best = null;
            DateTime bestTime = DateTime.MinValue;
            foreach (string rel in new string[]
            {
                @"tools\CrashHarness\bin\Release\net462\CrashHarness.exe",
                @"tools\CrashHarness\bin\x86\Release\net462\CrashHarness.exe",
                @"tools\CrashHarness\bin\Debug\net462\CrashHarness.exe",
                @"tools\CrashHarness\bin\x86\Debug\net462\CrashHarness.exe"
            })
            {
                string p = Path.Combine(root, rel);
                if (File.Exists(p) && File.GetLastWriteTimeUtc(p) > bestTime)
                {
                    best = p;
                    bestTime = File.GetLastWriteTimeUtc(p);
                }
            }
            if (best == null)
            {
                throw new FileNotFoundException("CrashHarness.exe not built");
            }
            return best;
        }

        [Fact]
        public void KillDuringOps()
        {
            KillLoop(false, 300);
        }

        [Fact]
        public void KillDuringBackup()
        {
            KillLoop(true, 300);
        }

        private static void KillLoop(bool backup, int target)
        {
            string harness = FindHarnessExe();
            var rnd = new Random(1234 + (backup ? 1 : 0));
            int kills = 0;
            int attempts = 0;
            var failures = new List<string>();
            string progress = Path.Combine(Path.GetTempPath(), "cdk-progress-" + (backup ? "bak" : "ops") + ".txt");
            File.WriteAllText(progress, "start " + DateTime.UtcNow.ToString("o") + "\r\n");
            while (kills < target && attempts < target * 4)
            {
                attempts++;
                string home = Path.Combine(Path.GetTempPath(), "cdk-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(home);
                string ack = Path.Combine(home, "ack.txt");
                string args = "--home \"" + home + "\" --ops 800 --seed " + attempts + " --ack \"" + ack + "\"";
                if (backup)
                {
                    Directory.CreateDirectory(Path.Combine(home, "bak"));
                    args += " --backup-every 3 --backup-dir \"" + Path.Combine(home, "bak") + "\"";
                }
                var psi = new ProcessStartInfo(harness, args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardError = true;
                Process p = null;
                try
                {
                    p = Process.Start(psi);
                }
                catch (Exception e)
                {
                    failures.Add("iter " + attempts + ": start failed: " + e.GetType().Name + " " + e.Message);
                    Wipe(home);
                    break;
                }
                Thread.Sleep(rnd.Next(50, 800));
                if (p.HasExited)
                {
                    if (p.ExitCode != 0)
                    {
                        failures.Add("iter " + attempts + ": harness exited " + p.ExitCode + ": " + p.StandardError.ReadToEnd());
                        Wipe(home);
                        break;
                    }
                    p.Dispose();
                    Wipe(home);
                    continue;
                }
                try
                {
                    p.Kill();
                }
                catch (Exception)
                {
                    p.Dispose();
                    Wipe(home);
                    continue;
                }
                p.WaitForExit(15000);
                if (!p.HasExited)
                {
                    try { p.Kill(); }
                    catch (Exception) { }
                    p.WaitForExit(15000);
                }
                p.Dispose();
                kills++;
                if (kills % 25 == 0)
                {
                    File.AppendAllText(progress, "kills=" + kills + " attempts=" + attempts + " " + DateTime.UtcNow.ToString("o") + "\r\n");
                }
                try
                {
                    Verify(home, ack);
                }
                catch (Exception e)
                {
                    failures.Add("iter " + attempts + ": " + e.GetType().Name + " " + e.Message);
                    Wipe(home);
                    if (failures.Count >= 3)
                    {
                        break;
                    }
                }
                Wipe(home);
            }
            Assert.True(kills >= target, "only " + kills + "/" + target + " kills");
            Assert.True(failures.Count == 0, string.Join(" | ", failures));
        }

        private static void Verify(string home, string ack)
        {
            var clock = new FakeClock();
            RecoveryResult rec = DatabaseRecovery.OpenWithRecovery(home, clock, null);
            try
            {
                List<string> bad = rec.Gateway.CheckInvariants();
                if (bad.Count > 0)
                {
                    throw new InvalidOperationException("invariants: " + string.Join(",", bad));
                }
                if (File.Exists(ack))
                {
                    IEventStore events = new SqliteEventStore();
                    foreach (string uuid in File.ReadAllLines(ack))
                    {
                        string u = uuid.Trim();
                        if (u.Length == 0)
                        {
                            continue;
                        }
                        if (!rec.Gateway.Read(s => events.ExistsByRequestUuid(s, u)))
                        {
                            throw new InvalidOperationException("acked op missing: " + u);
                        }
                    }
                }
            }
            finally
            {
                rec.Gateway.Close();
            }
        }

        private static void Wipe(string home)
        {
            try
            {
                Directory.Delete(home, true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}

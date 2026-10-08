using System;
using System.Runtime.CompilerServices;
using CourtDesk.Bootstrap;
using CourtDesk.Persistence;

namespace CourtDesk
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            EmbeddedAssemblyLoader.Register();
            return RunApp(args);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int RunApp(string[] args)
        {
            bool selftest = false;
            string home = null;
            string shots = null;
            bool next = false;
            string pending = null;
            foreach (string a in args)
            {
                if (next)
                {
                    if (pending == "--home")
                    {
                        home = a;
                    }
                    else if (pending == "--screenshots")
                    {
                        shots = a;
                    }
                    next = false;
                    pending = null;
                    continue;
                }
                if (string.Equals(a, "--home", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(a, "--screenshots", StringComparison.OrdinalIgnoreCase))
                {
                    next = true;
                    pending = a.ToLowerInvariant();
                    continue;
                }
                if (string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase))
                {
                    selftest = true;
                }
            }
            if (!string.IsNullOrEmpty(shots))
            {
                return ScreenshotMode.Run(shots, null);
            }
            if (selftest)
            {
                string resolved = string.IsNullOrEmpty(home)
                    ? DbPaths.ResolveHome(null)
                    : home;
                return SelfTest.Run(resolved);
            }
            var app = new App();
            app.Run();
            return 0;
        }
    }
}
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
            bool next = false;
            foreach (string a in args)
            {
                if (next)
                {
                    home = a;
                    next = false;
                    continue;
                }
                if (string.Equals(a, "--home", StringComparison.OrdinalIgnoreCase))
                {
                    next = true;
                    continue;
                }
                if (string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase))
                {
                    selftest = true;
                }
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
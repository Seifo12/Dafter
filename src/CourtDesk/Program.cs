using System;
using System.Runtime.CompilerServices;
using CourtDesk.Bootstrap;

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
            foreach (string a in args)
            {
                if (string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine("selftest: implemented in Phase 8");
                    return 3;
                }
            }
            var app = new App();
            app.Run();
            return 0;
        }
    }
}

using System;
using System.IO;
using System.Reflection;

namespace CourtDesk.Bootstrap
{
    public static class EmbeddedAssemblyLoader
    {
        private static bool _registered;

        public static void Register()
        {
            if (_registered)
            {
                return;
            }
            _registered = true;
            AppDomain.CurrentDomain.AssemblyResolve += OnResolve;
        }

        private static Assembly OnResolve(object sender, ResolveEventArgs args)
        {
            string name = new AssemblyName(args.Name).Name;
            string wanted = "vendor/" + name + ".dll";
            Assembly self = Assembly.GetExecutingAssembly();
            foreach (string res in self.GetManifestResourceNames())
            {
                if (string.Equals(res, wanted, StringComparison.OrdinalIgnoreCase))
                {
                    using (Stream s = self.GetManifestResourceStream(res))
                    {
                        if (s == null)
                        {
                            return null;
                        }
                        byte[] buf = new byte[s.Length];
                        int offset = 0;
                        while (offset < buf.Length)
                        {
                            int n = s.Read(buf, offset, buf.Length - offset);
                            if (n <= 0)
                            {
                                break;
                            }
                            offset += n;
                        }
                        return Assembly.Load(buf);
                    }
                }
            }
            return null;
        }
    }
}

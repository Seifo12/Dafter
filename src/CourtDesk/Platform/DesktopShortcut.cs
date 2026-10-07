using System;
using System.Reflection;

namespace CourtDesk.Platform
{
    public static class DesktopShortcut
    {
        public static bool TryCreate(string exePath, string name)
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (string.IsNullOrEmpty(desktop))
                {
                    return false;
                }
                Type shell = Type.GetTypeFromProgID("WScript.Shell");
                if (shell == null)
                {
                    return false;
                }
                object obj = Activator.CreateInstance(shell);
                try
                {
                    object shortcut = shell.InvokeMember("CreateShortcut",
                        BindingFlags.InvokeMethod, null, obj, new object[] { desktop + "\\" + name + ".lnk" });
                    Type t = shortcut.GetType();
                    t.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { exePath });
                    t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { desktop });
                    t.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut,
                        new object[] { CourtDesk.Resources.Strings.AppTitle });
                    return true;
                }
                finally
                {
                    System.Runtime.InteropServices.Marshal.FinalReleaseComObject(obj);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
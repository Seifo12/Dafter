namespace CourtDesk.Persistence
{
    public sealed class HomePaths
    {
        public string Home;
        public string Db;
        public string AutoDir;
        public string ExitDir;
        public string PreOpDir;
        public string QuarantineDir;
        public string TmpDir;
    }

    public static class DbPathsFor
    {
        public static HomePaths Of(string home)
        {
            return new HomePaths
            {
                Home = home,
                Db = DbPaths.Db(home),
                AutoDir = DbPaths.AutoDir(home),
                ExitDir = DbPaths.ExitDir(home),
                PreOpDir = DbPaths.PreOpDir(home),
                QuarantineDir = DbPaths.QuarantineDir(home),
                TmpDir = DbPaths.TmpDir(home)
            };
        }
    }
}

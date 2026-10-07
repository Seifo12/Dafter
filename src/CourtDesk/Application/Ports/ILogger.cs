namespace CourtDesk.Application.Ports
{
    public interface ILogger
    {
        void Info(string code, string detail);
        void Warn(string code, string detail);
        void Error(string code, string detail);
    }

    public sealed class NullLogger : ILogger
    {
        public void Info(string code, string detail) { }
        public void Warn(string code, string detail) { }
        public void Error(string code, string detail) { }
    }
}

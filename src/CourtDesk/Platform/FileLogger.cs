using System;
using System.Globalization;
using System.IO;
using System.Text;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;

namespace CourtDesk.Platform
{
    public sealed class FileLogger : ILogger
    {
        private readonly string _dir;
        private readonly object _lock = new object();
        private string _currentDay = "";

        public FileLogger(string home)
        {
            _dir = Path.Combine(home, "logs");
        }

        public static int RetainLogs = 30;

        public string TodayPath()
        {
            return Path.Combine(_dir, "app-" + DayKey() + ".log");
        }

        private static string DayKey()
        {
            return new SystemClock().UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }

        public void Info(string code, string detail)
        {
            Log("info", code, detail);
        }

        public void Warn(string code, string detail)
        {
            Log("warn", code, detail);
        }

        public void Error(string code, string detail)
        {
            Log("error", code, detail);
        }

        public void Log(string level, string code, string detail)
        {
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(_dir);
                    if (DayKey() != _currentDay)
                    {
                        _currentDay = DayKey();
                        RollRetain();
                    }
                    var sb = new StringBuilder();
                    sb.Append(new SystemClock().UtcNow.ToString(
                        "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                    sb.Append(" [").Append(level).Append("] ");
                    sb.Append(code == null ? "" : code);
                    if (!string.IsNullOrEmpty(detail))
                    {
                        sb.Append(" - ").Append(detail.Replace("\r", " ").Replace("\n", " "));
                    }
                    sb.AppendLine();
                    File.AppendAllText(TodayPath(), sb.ToString(), Encoding.UTF8);
                }
            }
            catch (Exception)
            {
            }
        }

        private void RollRetain()
        {
            try
            {
                string[] files = Directory.GetFiles(_dir, "app-*.log");
                if (files.Length <= RetainLogs)
                {
                    return;
                }
                Array.Sort(files, delegate(string a, string b)
                {
                    return string.CompareOrdinal(
                        File.GetLastWriteTimeUtc(a).ToString("o", CultureInfo.InvariantCulture),
                        File.GetLastWriteTimeUtc(b).ToString("o", CultureInfo.InvariantCulture));
                });
                for (int i = 0; i < files.Length - RetainLogs; i++)
                {
                    File.Delete(files[i]);
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
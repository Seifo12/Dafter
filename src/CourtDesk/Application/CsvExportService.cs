using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;

namespace CourtDesk.Application
{
    public sealed class CsvExportService
    {
        private readonly IDatabase _db;
        private readonly IDefendantStore _defendants;
        private readonly IClock _clock;

        public CsvExportService(IDatabase db, IDefendantStore defendants, IClock clock)
        {
            _db = db;
            _defendants = defendants;
            _clock = clock;
        }

        public string ExportToDesktop()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string name = "CourtDesk_Export_" +
                _clock.UtcNow.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture) + ".csv";
            string path = Path.Combine(desktop, name);
            Export(path);
            return path;
        }

        public string BuildCsv()
        {
            List<DefendantRow> rows = _db.Read(s => _defendants.ListActiveRows(s));
            var sb = new StringBuilder(64 * (rows.Count + 1));
            sb.Append(CourtDesk.Resources.Strings.CsvHeaders);
            sb.Append("\r\n");
            int i = 0;
            foreach (DefendantRow r in rows)
            {
                i++;
                LocalDate? session = r.D.SessionDate;
                LocalDate? since = r.D.DetainedSince;
                sb.Append(i);
                Field(sb, r.D.FullName);
                Field(sb, r.D.NationalId);
                Field(sb, r.D.CaseNumber);
                Field(sb, r.D.CaseYear.ToString(CultureInfo.InvariantCulture));
                Field(sb, r.D.CaseType);
                Field(sb, r.D.Charges);
                Field(sb, r.CourtName);
                Field(sb, r.CircuitName);
                Field(sb, r.CustodyName);
                Field(sb, session.HasValue ? ShortDate(session.Value) : "");
                Field(sb, StatusLabel(r.D.Status));
                Field(sb, since.HasValue ? ShortDate(since.Value) : "");
                Field(sb, r.D.Notes);
                sb.Append("\r\n");
            }
            return sb.ToString();
        }

        public void Export(string path)
        {
            string csv = BuildCsv();
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            string tmp = path + ".partial";
            var bytes = new List<byte>();
            bytes.AddRange(new byte[] { 0xEF, 0xBB, 0xBF });
            bytes.AddRange(Encoding.UTF8.GetBytes(csv));
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                foreach (byte b in bytes)
                {
                    fs.WriteByte(b);
                }
                fs.Flush(true);
            }
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            File.Move(tmp, path);
        }

        private static string ShortDate(LocalDate d)
        {
            return d.Day.ToString("00", CultureInfo.InvariantCulture) + "/" +
                   d.Month.ToString("00", CultureInfo.InvariantCulture) + "/" +
                   d.Year.ToString("0000", CultureInfo.InvariantCulture);
        }

        private static string StatusLabel(string status)
        {
            if (status == DefendantStatuses.Released)
            {
                return CourtDesk.Resources.Strings.StatusReleased;
            }
            if (status == DefendantStatuses.Judged)
            {
                return CourtDesk.Resources.Strings.StatusJudged;
            }
            return CourtDesk.Resources.Strings.StatusDetained;
        }

        private static void Field(StringBuilder sb, string value)
        {
            sb.Append(',');
            if (string.IsNullOrEmpty(value))
            {
                return;
            }
            bool quote = false;
            foreach (char c in value)
            {
                if (c == ',' || c == '"' || c == '\n' || c == '\r')
                {
                    quote = true;
                    break;
                }
            }
            if (!quote)
            {
                sb.Append(value);
                return;
            }
            sb.Append('"');
            foreach (char raw in value)
            {
                char c = raw;
                if (c == '"')
                {
                    sb.Append('"');
                }
                if (c == '\n' || c == '\r')
                {
                    c = ' ';
                }
                sb.Append(c);
            }
            sb.Append('"');
        }
    }
}

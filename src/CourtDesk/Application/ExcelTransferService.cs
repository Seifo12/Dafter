using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;
using CourtDesk.Resources;

namespace CourtDesk.Application
{
    public sealed class ImportReport
    {
        public int Added;
        public int Skipped;
        public readonly List<string> Problems = new List<string>();
    }

    public sealed class ExcelTransferService
    {
        private const string HeaderFill = "1B3A6B";
        private const string HeaderText = "FFFFFF";
        private const string BandFill = "EEF1F5";
        private const string BandText = "1B2430";

        private readonly IDatabase _db;
        private readonly IDefendantStore _defendants;
        private readonly IEventStore _events;
        private readonly IClock _clock;
        private readonly DefendantService _service;

        public ExcelTransferService(IDatabase db, IDefendantStore defendants,
            IEventStore events, IClock clock, DefendantService service)
        {
            _db = db;
            _defendants = defendants;
            _events = events;
            _clock = clock;
            _service = service;
        }

        public static List<XlsxColumn> Columns()
        {
            return new List<XlsxColumn>
            {
                new XlsxColumn { Header = Strings.FieldName, Width = 34 },
                new XlsxColumn { Header = Strings.FieldCaseNumber, Width = 14 },
                new XlsxColumn { Header = Strings.FieldCaseYear, Width = 12 },
                new XlsxColumn { Header = Strings.FieldCaseType, Width = 16 },
                new XlsxColumn { Header = Strings.FieldCharges, Width = 34 },
                new XlsxColumn { Header = Strings.FieldCourt, Width = 20 },
                new XlsxColumn { Header = Strings.FieldCircuit, Width = 18 },
                new XlsxColumn { Header = Strings.FieldCustody, Width = 18 },
                new XlsxColumn { Header = Strings.FieldSessionDate, Width = 16 },
                new XlsxColumn { Header = Strings.FieldNationalId, Width = 16 },
                new XlsxColumn { Header = Strings.FieldNotes, Width = 30 }
            };
        }

        public void WriteTemplate(string path)
        {
            var sample = new List<string>
            {
                "\u0645\u062d\u0645\u062f \u0639\u0628\u062f \u0627\u0644\u0644\u0647 \u0627\u0644\u0633\u064a\u062f",
                "1001", "2026", "\u062c\u0631\u064a\u0645\u0629", "\u062c\u0631\u064a\u0645\u0629 \u0639\u0627\u062f\u0629",
                "\u0627\u0644\u0646\u0627\u0628\u0628\u0629 \u0627\u0644\u0639\u0627\u0645\u0629", "\u0627\u0644\u0623\u0648\u0644",
                "\u062d\u062c\u0632", _clock.Today.AddDays(7).ToIso(), "", ""
            };
            Xlsx.Write(path, Strings.NavRegister, Columns(),
                new List<List<string>> { sample }, HeaderFill, HeaderText,
                BandFill, BandText, 1);
        }

        public int Export(string path, System.Collections.Generic.IReadOnlyList<CourtDesk.UI.ViewModels.RegisterRow> rows)
        {
            var data = new List<List<string>>();
            if (rows != null)
            {
                foreach (CourtDesk.UI.ViewModels.RegisterRow r in rows)
                {
                    data.Add(new List<string>
                    {
                        r.Name,
                        CaseNumber(r.CaseText),
                        YearOf(r.CaseText),
                        "",
                        Clean(r.ChargesText),
                        Clean(r.CourtText),
                        "",
                        Clean(r.CustodyText),
                        r.SessionLine1,
                        "",
                        ""
                    });
                }
            }
            Xlsx.Write(path, Strings.NavRegister, Columns(), data,
                HeaderFill, HeaderText, BandFill, BandText, 1);
            return data.Count;
        }

        private static string Clean(string v)
        {
            if (string.IsNullOrWhiteSpace(v))
            {
                return "";
            }
            v = v.Trim();
            return v == "—" || v == "-" ? "" : v;
        }

        private static string CaseNumber(string caseText)
        {
            if (string.IsNullOrWhiteSpace(caseText))
            {
                return "";
            }
            int i = caseText.IndexOf(' ');
            return i < 0 ? caseText.Trim() : caseText.Substring(0, i).Trim();
        }

        private static string YearOf(string caseText)
        {
            if (string.IsNullOrWhiteSpace(caseText))
            {
                return "";
            }
            string[] parts = caseText.Trim().Split(' ');
            return parts.Length > 1 ? parts[parts.Length - 1].Trim() : "";
        }

        public ImportReport Import(string path, bool dryRun)
        {
            var report = new ImportReport();
            List<List<string>> grid = Xlsx.Read(path);
            if (grid.Count < 2)
            {
                report.Problems.Add(Strings.ExcelEmpty);
                return report;
            }
            List<XlsxColumn> cols = Columns();
            Dictionary<string, int> head = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            List<string> first = grid[0];
            for (int i = 0; i < first.Count; i++)
            {
                head[first[i].Trim()] = i;
            }
            foreach (XlsxColumn c in cols)
            {
                if (!head.ContainsKey(c.Header))
                {
                    report.Problems.Add(Strings.ExcelMissingColumn + " " + c.Header);
                }
            }
            if (report.Problems.Count > 0)
            {
                return report;
            }
            for (int r = 1; r < grid.Count; r++)
            {
                List<string> row = grid[r];
                string name = At(row, head[Strings.FieldName]);
                string caseNo = At(row, head[Strings.FieldCaseNumber]);
                if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(caseNo))
                {
                    report.Skipped++;
                    continue;
                }
                if (string.IsNullOrWhiteSpace(name))
                {
                    report.Problems.Add(Strings.ExcelRowProblem + " " + (r + 1)
                        + ": " + Strings.ValCaseNumber);
                    continue;
                }
                string year = At(row, head[Strings.FieldCaseYear]);
                if (string.IsNullOrWhiteSpace(year))
                {
                    year = _clock.Today.Year.ToString(CultureInfo.InvariantCulture);
                }
                LocalDate? session = ParseDate(At(row, head[Strings.FieldSessionDate]));
                var input = new DefendantInput
                {
                    FullName = name.Trim(),
                    CaseNumber = caseNo.Trim(),
                    CaseYear = year.Trim(),
                    CaseType = At(row, head[Strings.FieldCaseType]),
                    Charges = At(row, head[Strings.FieldCharges]),
                    NationalId = At(row, head[Strings.FieldNationalId]),
                    Notes = At(row, head[Strings.FieldNotes])
                };
                if (session.HasValue)
                {
                    input.SessionDate = session;
                }
                if (dryRun)
                {
                    report.Added++;
                    continue;
                }
                try
                {
                    CreateResult res = _service.Create(input, Guid.NewGuid().ToString("N"), false);
                    if (res.Duplicates != null && res.Duplicates.Count > 0)
                    {
                        report.Skipped++;
                        report.Problems.Add(Strings.ExcelRowProblem + " " + (r + 1)
                            + ": " + Strings.DupWarn);
                        continue;
                    }
                    report.Added++;
                }
                catch (System.Exception)
                {
                    report.Problems.Add(Strings.ExcelRowProblem + " " + (r + 1));
                }
            }
            return report;
        }

        private static string At(List<string> row, int index)
        {
            return index >= 0 && index < row.Count ? (row[index] ?? "").Trim() : "";
        }

        private static LocalDate? ParseDate(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }
            DateTime d;
            string[] formats = new string[]
            {
                "yyyy-MM-dd", "dd/MM/yyyy", "dd-MM-yyyy", "d/M/yyyy", "yyyy/MM/dd"
            };
            if (DateTime.TryParseExact(raw.Trim(), formats,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
            {
                return new LocalDate(d.Year, d.Month, d.Day);
            }
            return null;
        }
    }
}
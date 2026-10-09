using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using Xunit;

namespace CourtDesk.Tests
{
    [Trait("Category", "Persistence")]
    public sealed class ExcelTransferTests
    {
        private static string Out(string name)
        {
            string dir = Path.Combine(Path.GetTempPath(), "courtdesk-xlsx");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, name);
        }

        [Fact]
        public void TemplateHasArabicHeadersAndSampleRow()
        {
            using (var env = new DialogEnv())
            {
                var svc = new ExcelTransferService(env.Db, env.Defendants, env.Events,
                    env.Clock, env.DefendantService);
                string path = Out("template.xlsx");
                svc.WriteTemplate(path);
                Assert.True(new FileInfo(path).Length > 2000, "template too small");
                List<List<string>> grid = Xlsx.Read(path);
                Assert.True(grid.Count >= 2, "rows: " + grid.Count);
                List<string> head = grid[0];
                List<XlsxColumn> cols = ExcelTransferService.Columns();
                for (int i = 0; i < cols.Count; i++)
                {
                    Assert.Equal(cols[i].Header, head[i]);
                }
                Assert.True(head[0].Contains("\u0645"), "name header not arabic: " + head[0]);
                Assert.False(string.IsNullOrEmpty(grid[1][0]), "no sample name");
            }
        }

        [Fact]
        public void ExportWritesAllRowsAndImportReadsThem()
        {
            using (var env = new DialogEnv())
            {
                env.Register.Filter = Filters.All;
                env.Add("round one", "9101", new CourtDesk.Domain.LocalDate(2026, 10, 20));
                env.Add("round two", "9102", new CourtDesk.Domain.LocalDate(2026, 10, 21));
                env.Refresh();
                var svc = new ExcelTransferService(env.Db, env.Defendants, env.Events,
                    env.Clock, env.DefendantService);
                string path = Out("export2.xlsx");
                int n = svc.Export(path, env.Register.Rows);
                Assert.Equal(2, n);
                List<List<string>> grid = Xlsx.Read(path);
                Assert.Equal(3, grid.Count);
                bool one = false, two = false;
                for (int r = 1; r < grid.Count; r++)
                {
                    if (grid[r].Exists(v => v.Contains("round one"))) one = true;
                    if (grid[r].Exists(v => v.Contains("round two"))) two = true;
                }
                Assert.True(one && two, "rows missing: " + grid.Count);
                ImportReport dry = svc.Import(path, true);
                Assert.True(dry.Problems.Count == 0,
                    string.Join(" | ", dry.Problems.ToArray()));
                Assert.Equal(2, dry.Added);

                string other = Out("other.xlsx");
                List<XlsxColumn> cs = ExcelTransferService.Columns();
                var lines = new List<List<string>>();
                lines.Add(new List<string> { "اسم جديد", "7777", "2026" });
                Xlsx.Write(other, "x", cs, lines, "1B3A6B", "FFFFFF", "EEF1F5",
                    "1B2430", 1);
                ImportReport add = svc.Import(other, false);
                Assert.True(add.Problems.Count == 0,
                    string.Join(" | ", add.Problems.ToArray()));
                Assert.Equal(1, add.Added);
                Assert.Equal(3, env.Db.Read(s => env.Defendants.CountActive(s)));
            }
        }
[Fact]
        public void BackupScreenHasExcelButtons()
        {
            StaRunner.Run(delegate
            {
                using (var env = new DialogEnv())
                {
                    var engine = new CourtDesk.Persistence.SqliteBackupEngine(env.Db, env.Clock, env.Home);
                    var backups = new BackupService(env.Db, engine,
                        new FakeDriveProvider(), new CourtDesk.Persistence.SqliteMetaStore(),
                        env.Clock, env.Settings, BackupService.ForHome(env.Home),
                        new NullLogger());
                    var view = new CourtDesk.UI.Dialogs.BackupSettingsView(
                        backups, env.Settings, env.Home);
                    view.Excel = new ExcelTransferService(env.Db, env.Defendants,
                        env.Events, env.Clock, env.DefendantService);
                    view.RowsProvider = delegate { return env.Register.Rows; };
                    view.Measure(new Size(900, 680));
                    view.Arrange(new Rect(0, 0, 900, 680));
                    view.UpdateLayout();
                    Assert.NotNull(view.FindName("TemplateButton"));
                    Assert.NotNull(view.FindName("ImportButton"));
                    Assert.NotNull(view.FindName("ExportButton"));
                    var b = (Button)view.FindName("ExportButton");
                    Assert.True(b.IsEnabled, "export button disabled");
                    var tpl = (Button)view.FindName("TemplateButton");
                    Assert.True(tpl.IsEnabled, "template button disabled");
                }
            });
        }

        [Fact]
        public void ImportRejectsMissingColumnAndReports()
        {
            using (var env = new DialogEnv())
            {
                var svc = new ExcelTransferService(env.Db, env.Defendants, env.Events,
                    env.Clock, env.DefendantService);
                var cols = new List<XlsxColumn>
                {
                    new XlsxColumn { Header = CourtDesk.Resources.Strings.FieldName, Width = 30 }
                };
                string path = Out("bad.xlsx");
                Xlsx.Write(path, "x", cols,
                    new List<List<string>> { new List<string> { "بدون الأعمدة" } },
                    "1B3A6B", "FFFFFF", "EEF1F5", "1B2430", 1);
                ImportReport r = svc.Import(path, false);
                Assert.Equal(0, r.Added);
                Assert.True(r.Problems.Count > 0, "no problem reported");
                Assert.Contains(CourtDesk.Resources.Strings.ExcelMissingColumn, r.Problems[0]);
            }
        }
    }
}
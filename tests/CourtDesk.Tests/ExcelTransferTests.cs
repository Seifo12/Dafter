using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;
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
        public void ImportThenBackupScreenRowsProviderSeesThem()
        {
            using (var env = new DialogEnv())
            {
                env.Register.Filter = Filters.All;
                var svc = new ExcelTransferService(env.Db, env.Defendants, env.Events,
                    env.Clock, env.DefendantService);
                string path = Out("roundtrip.xlsx");
                List<XlsxColumn> cs = ExcelTransferService.Columns();
                var lines = new List<List<string>>();
                lines.Add(new List<string> { "مستورد واحد", "5150", "2026" });
                lines.Add(new List<string> { "مستورد اثنين", "5151", "2026" });
                Xlsx.Write(path, "x", cs, lines, "1B3A6B", "FFFFFF", "EEF1F5",
                    "1B2430", 1);

                List<List<string>> raw = Xlsx.Read(path);
                Assert.True(raw.Count >= 3,
                    "rows read back: " + raw.Count + " / first='"
                    + (raw.Count > 0 && raw[0].Count > 0 ? raw[0][0] : "?") + "'");

                // this is exactly what App.xaml.cs ScopedRowsProvider does
                System.Func<string, IReadOnlyList<CourtDesk.UI.ViewModels.RegisterRow>> scoped =
                    delegate(string scope)
                    {
                        env.Register.Filter = string.IsNullOrEmpty(scope) ? Filters.All : scope;
                        env.Register.Refresh();
                        return env.Register.Rows;
                    };

                int before = scoped("ALL").Count;
                ImportReport r = svc.Import(path, false);
                Assert.True(r.Problems.Count == 0,
                    string.Join(" | ", r.Problems.ToArray()));
                Assert.Equal(2, r.Added);

                // App.xaml.cs must rebuild the search index after import,
                // otherwise Refresh() re-reads stale rows and nothing shows up.
                env.Index.Rebuild();
                int after = scoped("ALL").Count;
                Assert.Equal(before + 2, after);
                Assert.Equal(after, scoped("ALL").Count);
            }
        }

        [Fact]
        public void ReleasedWithBailAppearsInRoster()
        {
            using (var env = new DialogEnv())
            {
                var rosters = new RosterService(env.Db, env.Defendants, env.Events,
                    env.Clock, env.Settings);
                var decisions = new DecisionService(env.Db, env.Defendants, env.Events,
                    env.Clock, env.Settings);
                int id = env.Add("بريء بكفالة", "7100", env.Clock.Today);
                int version = env.Db.Read(s => env.Defendants.GetById(s, id).RowVersion);
                decisions.Decide(new DecisionRequest
                {
                    DefendantId = id,
                    ExpectedVersion = version,
                    RequestUuid = Guid.NewGuid().ToString("N"),
                    Code = DecisionCodes.ReleaseBail,
                    BailAmount = 5000
                });

                // the whole roster for today must carry the acquitted person
                RosterModel all = rosters.Build(env.Clock.Today);
                RosterEntry found = null;
                foreach (RosterEntry e in all.Entries)
                {
                    if (e.DefendantId == id) { found = e; }
                }
                Assert.NotNull(found);
                Assert.Equal(CourtDesk.Resources.Strings.DecBail, found.DecisionLabel);
                Assert.Equal("5000", found.BailAmount);

                // and the released scope must be able to show them on its own
                RosterModel released = rosters.Build(env.Clock.Today, Filters.Released);
                Assert.Equal(1, released.Count);
            }
        }

        [Fact]
        public void ScopedRosterPrintsOnlyChosenSection()
        {
            using (var env = new DialogEnv())
            {
                var rosters = new RosterService(env.Db, env.Defendants, env.Events,
                    env.Clock, env.Settings);
                env.Add("محتجز واحد", "7001", env.Clock.Today);
                env.Add("محتجز اثنين", "7002", env.Clock.Today);

                RosterModel all = rosters.Build(env.Clock.Today);
                Assert.Equal(2, all.Count);

                RosterModel detained = rosters.Build(env.Clock.Today, Filters.Detained);
                Assert.Equal(2, detained.Count);

                RosterModel released = rosters.Build(env.Clock.Today, Filters.Released);
                Assert.Equal(0, released.Count);
            }
        }

        [Fact]
        public void BackupScreenSavesOfficerNameAndSection()
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
                    Assert.NotNull(view.FindName("OperatorBox"));
                    Assert.NotNull(view.FindName("DirectorateBox"));
                    Assert.NotNull(view.FindName("SaveOfficerButton"));
                    Assert.NotNull(view.FindName("ScopeBox"));
                    Assert.NotNull(view.FindName("ScopePrintButton"));
                    Assert.NotNull(view.FindName("ScopePreviewButton"));

                    var rosters = new RosterService(env.Db, env.Defendants, env.Events,
                        env.Clock, env.Settings);

                    var op = (System.Windows.Controls.TextBox)view.FindName("OperatorBox");
                    var dir = (System.Windows.Controls.TextBox)view.FindName("DirectorateBox");
                    op.Text = "عميد أحمد";
                    dir.Text = "قسم البحث";
                    view.UpdateLayout();
                    var btn = (Button)view.FindName("SaveOfficerButton");
                    Assert.True(btn.IsEnabled, "save officer button disabled");
                    btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                    Assert.Equal("عميد أحمد", env.Settings.Get(SettingKeys.OperatorName));
                    Assert.Equal("قسم البحث", env.Settings.Get(SettingKeys.DirectorateName));

                    // the roster preview window is built from the model, not the
                    // dialog, so make sure the model carries the values too.
                    RosterModel m = new RosterService(env.Db, env.Defendants, env.Events,
                        env.Clock, env.Settings).Build(env.Clock.Today);
                    Assert.Equal("عميد أحمد", m.OperatorName);
                    Assert.Equal("قسم البحث", m.DirectorateName);

                    // and the printed page must contain them, not blank boxes
                    var printer = new CourtDesk.Printing.PrintService(env.Db,
                        new CourtDesk.Persistence.SqliteRosterPrintLog(), rosters);
                    object doc = printer.BuildDocument(m);
                    Assert.NotNull(doc);
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
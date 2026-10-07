using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Xps.Packaging;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;
using CourtDesk.Persistence;
using CourtDesk.Printing;
using Xunit;

namespace CourtDesk.Tests
{
    [Trait("Category", "Printing")]
    public sealed class PrintingTests
    {
        private static RosterModel Fake(int n, long seed)
        {
            var m = new RosterModel
            {
                Date = new LocalDate(2026, 10, 7),
                DirectorateName = "\u0645\u062f\u0631\u064a\u0629 \u0623\u0645\u0646 \u0627\u0644\u0642\u0627\u0647\u0631\u0629",
                StationName = "\u0642\u0633\u0645 \u0634\u0631\u0637\u0629 \u0627\u0644\u0631\u062d\u064a\u0642",
                OperatorName = "\u0639\u0628\u062f \u0627\u0644\u0633\u0644\u0628\u064a"
            };
            var rnd = new Random((int)seed);
            string[] names = new string[]
            {
                "\u0645\u062d\u0645\u062f \u0639\u0628\u062f \u0627\u0644\u0644\u0647 \u0627\u0644\u0633\u064a\u062f",
                "\u0627\u0644\u0633\u064a\u062f \u062d\u0633\u0646 \u0639\u0644\u064a \u0639\u0644\u064a",
                "\u0643\u0631\u064a\u0645 \u0641\u0624\u0627\u062f \u0625\u0628\u0631\u0627\u0647\u064a\u0645",
                "\u0637\u0627\u0631\u0642 \u0645\u062d\u0645\u0648\u062f \u062d\u0633\u0646 \u0628\u0627\u0644\u0637\u0648\u0644"
            };
            for (int i = 0; i < n; i++)
            {
                m.Entries.Add(new RosterEntry
                {
                    DefendantId = i + 1,
                    FullName = names[i % names.Length] + " " + (100 + i),
                    CaseNumber = (1000 + i).ToString(),
                    CaseYear = 2026,
                    Charges = "\u062c\u0631\u064a\u0645\u0629 " + rnd.Next(1, 900),
                    CourtName = "\u0645\u062d\u0643\u0645\u0629 " + (i % 3),
                    CircuitName = "\u0627\u0644\u062f\u0627\u0626\u0631\u0629 " + (1 + (i % 4)),
                    CustodyName = "\u062d\u062c\u0632",
                    FromDecision = i % 5 == 0
                });
            }
            return m;
        }

        [Fact]
        public void EmptyRosterIsOnePageWithHeaderAndSignature()
        {
            var p = new RosterPaginator(0, delegate { return 22.0; });
            List<RosterPage> pages = p.Paginate();
            Assert.Single(pages);
            Assert.True(pages[0].HasPageHeader);
            Assert.True(pages[0].HasSummary);
            Assert.True(pages[0].HasSignature);
        }

        [Fact]
        public void Invariants_HoldForZeroToTwoHundredRandomRows()
        {
            for (int n = 0; n <= 200; n++)
            {
                var rnd = new Random(9000 + n);
                var heights = new double[n];
                for (int i = 0; i < n; i++)
                {
                    heights[i] = 20 + rnd.Next(0, 70);
                }
                var paginator = new RosterPaginator(n, delegate(int i) { return heights[i]; });
                List<RosterPage> pages = paginator.Paginate();

                Assert.True(pages.Count >= 1);

                var seen = new List<int>();
                int sigPages = 0;
                for (int p = 0; p < pages.Count; p++)
                {
                    RosterPage page = pages[p];
                    if (page.HasSignature)
                    {
                        sigPages++;
                        Assert.Equal(pages.Count - 1, p);
                    }
                    double avail = RosterGeometry.PageH - RosterGeometry.FooterH
                        - (page.HasPageHeader ? RosterGeometry.Page1HeaderH : RosterGeometry.RunningTitleH)
                        - RosterGeometry.TableHeaderH;
                    double used = 0;
                    foreach (int r in page.RowIndices)
                    {
                        used += heights[r];
                    }
                    double need = (page.HasSummary ? RosterGeometry.SummaryH : 0)
                        + (page.HasSignature ? 118.0 : 0);
                    Assert.True(used + need <= avail + 0.5,
                        "n=" + n + " page=" + p + " uses " + (used + need) + " > " + avail);
                    seen.AddRange(page.RowIndices);
                }

                Assert.Equal(1, sigPages);
                Assert.Equal(n, seen.Count);
                for (int i = 0; i < n; i++)
                {
                    Assert.Equal(i, seen[i]);
                }
            }
        }

        [Fact]
        public void SignatureBlockNeverAloneOnAnOrphanPageWhenRowsFit()
        {
            var heights = new double[3];
            for (int i = 0; i < heights.Length; i++)
            {
                heights[i] = 24;
            }
            var pages = new RosterPaginator(3, delegate(int i) { return heights[i]; }).Paginate();
            int last = pages.Count - 1;
            Assert.True(pages[last].RowIndices.Count >= 1);
        }

        [Fact]
        public void DocumentHasOnePagePerPaginatedPageAndRepeatsHeaderRow()
        {
            StaRunner.Run(delegate
            {
                foreach (int n in new int[] { 0, 1, 12, 40 })
                {
                    RosterModel m = Fake(n, 7 + n);
                    RosterDocumentBuilder builder = new RosterDocumentBuilder(m);
                    FixedDocument doc = builder.Build();
                    Assert.Equal(builder.PageCount, doc.Pages.Count);
                    Assert.True(doc.Pages.Count >= 1);
                    List<FixedPage> built = builder.BuildPages();
                    Assert.Equal(doc.Pages.Count, built.Count);
                    foreach (FixedPage page in built)
                    {
                        Assert.Equal(RosterGeometry.PageWidth, page.Width, 1);
                        Assert.Equal(RosterGeometry.PageHeight, page.Height, 1);
                        Assert.True(RtlChildren(page), "page has no right-to-left children");
                    }
                }
            });
        }

[Fact]
        public void DocumentPaginatorPageCountMatchesPagination()
        {
            StaRunner.Run(delegate
            {
                foreach (int n in new int[] { 0, 1, 12, 40 })
                {
                    RosterModel m = Fake(n, 11 + n);
                    RosterDocumentBuilder builder = new RosterDocumentBuilder(m);
                    FixedDocument doc = builder.Build();
                    Assert.Equal(builder.PageCount, doc.DocumentPaginator.PageCount);
                    Assert.Equal(doc.Pages.Count, doc.DocumentPaginator.PageCount);
                    Assert.True(doc.DocumentPaginator.PageCount >= 1);
                    FixedPage first = builder.BuildPages()[0];
                    Assert.Equal(RosterGeometry.PageWidth, first.Width, 1);
                    Assert.Equal(RosterGeometry.PageHeight, first.Height, 1);
                    Assert.True(RtlChildren(first), "page has no right-to-left children");
                }
            });
        }

        [Fact]
        public void RosterPagesRenderToPng()
        {
            StaRunner.Run(delegate
            {
                string dir = OutDir();
                foreach (int n in new int[] { 1, 12, 40 })
                {
                    RosterModel m = Fake(n, 31 + n);
                    RosterDocumentBuilder builder = new RosterDocumentBuilder(m);
                    FixedDocument doc = builder.Build();
                    int index = 0;
                    foreach (PageContent pc in doc.Pages)
                    {
                        FixedPage page = pc.Child;
                        page.Measure(new Size(page.Width, page.Height));
                        page.Arrange(new Rect(0, 0, page.Width, page.Height));
                        page.UpdateLayout();
                        int w = (int)RosterGeometry.PageWidth;
                        int h = (int)RosterGeometry.PageHeight;
                        var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap(
                            w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                        bmp.Render(page);
                        var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                        enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
                        string file = Path.Combine(dir, "roster-" + n + "-p" + (index + 1) + ".png");
                        using (FileStream fs = new FileStream(file, FileMode.Create))
                        {
                            enc.Save(fs);
                        }
                        index++;
                        if (index >= 2)
                        {
                            break;
                        }
                    }
                }
            });
        }

        private static bool RtlChildren(DependencyObject root)
        {
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                DependencyObject c = VisualTreeHelper.GetChild(root, i);
                var fe = c as FrameworkElement;
                if (fe != null && fe.ReadLocalValue(FrameworkElement.FlowDirectionProperty)
                    != null
                    && (FlowDirection)fe.GetValue(FrameworkElement.FlowDirectionProperty)
                        == FlowDirection.RightToLeft)
                {
                    return true;
                }
                if (RtlChildren(c))
                {
                    return true;
                }
            }
            return false;
        }

        private static string OutDir()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, "CourtDesk.sln")))
                {
                    string d = Path.Combine(dir, "artifacts", "roster");
                    Directory.CreateDirectory(d);
                    return d;
                }
                dir = Path.GetDirectoryName(dir);
            }
            throw new InvalidOperationException("repo root not found");
        }

        [Fact]
        public void RosterSetIncludesPendingAndDecidedButExcludesUndoneAndReopen()
        {
            string home = TestDb.NewHome();
            try
            {
                var clock = new FixedClock();
                RecoveryResult rec = TestDb.Open(home, clock);
                DbGateway db = rec.Gateway;
                var defendants = new SqliteDefendantStore();
                var events = new SqliteEventStore();
                var settings = new SettingsService(db, new SqliteSettingsStore());
                db.Write(s =>
                {
                    SeedData.SeedLookups((DbContext)s,
                        CourtDesk.Resources.Strings.SeedCourts,
                        CourtDesk.Resources.Strings.SeedCustody,
                        CourtDesk.Resources.Strings.SeedReasons);
                    return 0;
                });
                var svc = new DefendantService(db, defendants, events, clock, settings, new NullLogger());
                var decide = new DecisionService(db, defendants, events, clock, settings);
                var rosters = new RosterService(db, defendants, events, clock, settings);
                LocalDate today = new LocalDate(2026, 10, 7);

                int pending = svc.Create(new DefendantInput
                {
                    FullName = "\u0639\u0644\u0649 \u0645\u062c\u062f\u0648\u062f",
                    CaseNumber = "5001",
                    CaseYear = "2026",
                    SessionDate = today
                }, Guid.NewGuid().ToString("N"), true).Id;

                int decidedId = svc.Create(new DefendantInput
                {
                    FullName = "\u0633\u0645\u064a\u0631 \u0645\u062d\u0645\u0648\u062f",
                    CaseNumber = "5002",
                    CaseYear = "2026",
                    SessionDate = today
                }, Guid.NewGuid().ToString("N"), true).Id;
                Defendant d2 = svc.Get(decidedId);
                decide.Decide(new DecisionRequest
                {
                    RequestUuid = Guid.NewGuid().ToString("N"),
                    DefendantId = decidedId,
                    ExpectedVersion = d2.RowVersion,
                    Code = DecisionCodes.Release,
                    NextDate = null
                });

                int undoneId = svc.Create(new DefendantInput
                {
                    FullName = "\u0645\u0631\u062f\u0648\u062f \u0627\u0644\u0645\u0648\u062c\u0648\u062f",
                    CaseNumber = "5003",
                    CaseYear = "2026",
                    SessionDate = today
                }, Guid.NewGuid().ToString("N"), true).Id;
Defendant d3 = svc.Get(undoneId);
                var undo = new UndoService(db, defendants, events, clock, settings);
                DecideResult release = decide.Decide(new DecisionRequest
                {
                    RequestUuid = Guid.NewGuid().ToString("N"),
                    DefendantId = undoneId,
                    ExpectedVersion = d3.RowVersion,
                    Code = DecisionCodes.Release,
                    NextDate = null
                });
undo.Undo(release.EventId, Guid.NewGuid().ToString("N"));

                List<SessionEvent> heldToday =
                    db.Read(s => ((IEventStore)events).ListDecisionsOn(s, today.ToIso()));
                var heldIds = new List<int>();
                foreach (SessionEvent ev in heldToday)
                {
                    if (ev.EventType == "DECISION")
                    {
                        heldIds.Add(ev.DefendantId);
                    }
                }
                Assert.Contains(decidedId, heldIds);
                Assert.DoesNotContain(undoneId, heldIds);

                RosterModel model = rosters.Build(today);
                var ids = new List<int>();
                foreach (RosterEntry e in model.Entries)
                {
                    ids.Add(e.DefendantId);
                }
                Assert.Contains(pending, ids);
                Assert.Contains(decidedId, ids);
                Assert.Contains(undoneId, ids);
                Assert.Equal(3, ids.Count);
                int perCourt = 0;
                foreach (KeyValuePair<string, int> kv in model.PerCourtCounts)
                {
                    perCourt += kv.Value;
                }
                Assert.Equal(ids.Count, perCourt);
                db.Close();
            }
            finally
            {
                TestDb.Wipe(home);
            }
        }

        [Fact]
        public void PrintLogAppendIsAppendOnlyAndCounted()
        {
            string home = TestDb.NewHome();
            try
            {
                var clock = new FixedClock();
                RecoveryResult rec = TestDb.Open(home, clock);
                DbGateway db = rec.Gateway;
                IRosterPrintLog log = new SqliteRosterPrintLog();
                var ids = new List<int> { 4, 9, 11 };
                db.Write(s =>
                {
                    log.Append(s, "2026-10-07", 3, ids, "\u0639\u0628\u062f");
                    return 0;
                });
                int count = db.Read(s => log.CountFor(s, "2026-10-07"));
                Assert.Equal(1, count);
                db.Close();
            }
            finally
            {
                TestDb.Wipe(home);
            }
        }

        [Fact]
        public void ColumnFractionsSumToOne()
        {
            double[] f = RosterDocumentBuilder.ColumnFractions();
            double total = 0;
            foreach (double x in f)
            {
                total += x;
            }
            Assert.Equal(1.0, total, 3);
        }
    }
}
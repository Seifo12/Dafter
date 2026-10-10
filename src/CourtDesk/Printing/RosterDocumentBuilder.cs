using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using CourtDesk.Application;
using CourtDesk.Domain;
using CourtDesk.Resources;

namespace CourtDesk.Printing
{
    public sealed class RosterDocumentBuilder
    {
        private readonly RosterModel _model;
        private readonly Func<DateTime> _now;

        public RosterDocumentBuilder(RosterModel model)
            : this(model, delegate { return new SystemClock().UtcNow; })
        {
        }

        public RosterDocumentBuilder(RosterModel model, Func<DateTime> now)
        {
            _model = model;
            _now = now;
        }

        internal const double FontSizeBody = 13.0;
        internal const double FontSizeCharges = 13.0;

        public const double ColM = 0.06;
        public const double ColName = 0.22;
        public const double ColCase = 0.13;
        public const double ColCharges = 0.21;
        public const double ColCourt = 0.17;
        public const double ColCustody = 0.10;
        public const double ColNotes = 0.11;

        public static double[] ColumnFractions()
        {
            return new double[]
            {
                ColM, ColName, ColCase, ColCharges, ColCourt, ColCustody, ColNotes
            };
        }

        public FixedDocument Build()
        {
            var doc = new FixedDocument();
            foreach (FixedPage fp in BuildPages())
            {
                var pc = new PageContent();
                pc.Child = fp;
                doc.Pages.Add(pc);
            }
            return doc;
        }

        public List<FixedPage> BuildPages()
        {
            RosterPaginator paginator = new RosterPaginator(_model.Count, MeasureRow);
            List<RosterPage> pages = paginator.Paginate();
            int total = pages.Count;
            var out_ = new List<FixedPage>();
            for (int p = 0; p < total; p++)
            {
                out_.Add(BuildPage(pages[p], p, total));
            }
            return out_;
        }

        public int PageCount
        {
            get
            {
                RosterPaginator paginator = new RosterPaginator(_model.Count, MeasureRow);
                return paginator.Paginate().Count;
            }
        }

        internal double MeasureRow(int index)
        {
            RosterEntry e = _model.Entries[index];
            double[] frac = ColumnFractions();
            int lines = 1;
            lines = Math.Max(lines, LinesFor(e.FullName, frac[1]));
            lines = Math.Max(lines, LinesFor(e.Charges, frac[3]));
            lines = Math.Max(lines, LinesFor(Combine(e.CourtName, e.CircuitName), frac[4]));
            lines = Math.Max(lines, LinesFor(e.CustodyName, frac[5]));
            double h = (lines * FontSizeCharges * 1.45) + 12.0;
            return Math.Min(120.0, h);
        }

        private static int LinesFor(string text, double fraction)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 1;
            }
            double avail = (RosterGeometry.ContentWidth * fraction) - 14;
            if (avail < 20)
            {
                avail = 20;
            }
            double w = Measure(text, FontSizeCharges);
            int lines = (int)Math.Ceiling(w / avail);
            return lines < 1 ? 1 : lines;
        }

        private static double Measure(string text, double size)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }
            var ft = new FormattedText(text, CultureInfo.InvariantCulture,
                FlowDirection.RightToLeft, new Typeface("Tahoma"),
                size, Brushes.Black, 1.0);
            return ft.WidthIncludingTrailingWhitespace;
        }

        private static string LongestOf(params string[] parts)
        {
            string best = "";
            foreach (string p in parts)
            {
                if (!string.IsNullOrEmpty(p) && p.Length > best.Length)
                {
                    best = p;
                }
            }
            return best;
        }

        private FixedPage BuildPage(RosterPage page, int index, int total)
        {
            var fp = new FixedPage
            {
                Width = RosterGeometry.PageWidth,
                Height = RosterGeometry.PageHeight,
                FlowDirection = FlowDirection.RightToLeft,
                Background = Brushes.White
            };
            var root = new Grid
            {
                FlowDirection = FlowDirection.RightToLeft,
                Margin = new Thickness(RosterGeometry.Margin)
            };
            root.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(RosterGeometry.ContentWidth)
            });
            fp.Children.Add(root);

            int r = 0;
            AddRow(root, ref r, BuildPageHeader(page), GridLength.Auto);
            AddRow(root, ref r, BuildTableHeader(), new GridLength(RosterGeometry.TableHeaderH));
            foreach (int rowIndex in page.RowIndices)
            {
                AddRow(root, ref r, BuildTableRow(rowIndex), new GridLength(MeasureRow(rowIndex)));
            }
            if (page.HasSummary)
            {
                AddRow(root, ref r, BuildSummary(), new GridLength(RosterGeometry.SummaryH));
            }
            if (page.HasSignature)
            {
                AddRow(root, ref r, BuildSignatureBlock(), new GridLength(118));
            }
            AddRow(root, ref r, BuildFooter(index, total), new GridLength(RosterGeometry.FooterH));
            return fp;
        }

        private static void AddRow(Grid root, ref int index, FrameworkElement el, GridLength height)
        {
            root.RowDefinitions.Add(new RowDefinition { Height = height });
            el.HorizontalAlignment = HorizontalAlignment.Stretch;
            el.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetRow(el, index);
            root.Children.Add(el);
            index++;
        }

        private FrameworkElement BuildPageHeader(RosterPage page)
        {
            var panel = new StackPanel { FlowDirection = FlowDirection.RightToLeft };
            if (page.HasPageHeader)
            {
                panel.Children.Add(Line(Strings.RosterMinistry, 16, true));
                if (!string.IsNullOrEmpty(_model.DirectorateName))
                {
                    panel.Children.Add(Line(string.Format(CultureInfo.InvariantCulture,
                        Strings.RosterDirectorate, _model.DirectorateName), 16, true));
                }
                if (!string.IsNullOrEmpty(_model.StationName))
                {
                    panel.Children.Add(Line(string.Format(CultureInfo.InvariantCulture,
                        Strings.RosterStation, _model.StationName), 16, true));
                }
            }
            panel.Children.Add(new TextBlock
            {
                Text = Strings.RosterTitle,
                FontSize = page.HasPageHeader ? 20 : 13,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                FlowDirection = FlowDirection.RightToLeft
            });
            if (page.HasPageHeader)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = _model.AllDates
                        ? Strings.RosterAllDates
                        : string.Format(CultureInfo.InvariantCulture, Strings.RosterDate,
                            WeekdayOf(_model.Date),
                            _model.Date.Day.ToString("00") + "/" + _model.Date.Month.ToString("00")
                                + "/" + _model.Date.Year),
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    FlowDirection = FlowDirection.RightToLeft
                });
            }
            return panel;
        }

        private FrameworkElement BuildTableHeader()
        {
            return BuildRow(Strings.RosterCols, 13, true, null);
        }

        private FrameworkElement BuildTableRow(int index)
        {
            RosterEntry e = _model.Entries[index];
            string[] cells = new string[]
            {
                (index + 1).ToString(CultureInfo.InvariantCulture),
                e.FullName ?? "",
                e.CaseNumber + "/" + e.CaseYear,
                e.Charges ?? "",
                Combine(e.CourtName, e.CircuitName),
                e.CustodyName ?? "",
                NotesCell(e)
            };
            return BuildRow(cells, FontSizeBody, false, MeasureRow(index));
        }

        private static string NotesCell(RosterEntry e)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(e.DecisionLabel))
            {
                parts.Add(e.DecisionLabel);
            }
            if (!string.IsNullOrEmpty(e.BailAmount))
            {
                parts.Add(Strings.WizBailAmount + " " + e.BailAmount);
            }
            if (!string.IsNullOrEmpty(e.DetainedSince))
            {
                parts.Add(Strings.FieldDetainedSince + " " + e.DetainedSince);
            }
            return string.Join(" - ", parts.ToArray());
        }

        private static string Combine(string a, string b)
        {
            if (string.IsNullOrEmpty(a))
            {
                return b ?? "";
            }
            if (string.IsNullOrEmpty(b))
            {
                return a;
            }
            return a + " / " + b;
        }

        private Grid BuildRow(string[] cells, double fontSize, bool isHeader, double? fixedHeight)
        {
            double[] frac = ColumnFractions();
            var grid = new Grid { FlowDirection = FlowDirection.RightToLeft };
            for (int i = 0; i < frac.Length; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(frac[i], GridUnitType.Star)
                });
            }
            for (int i = 0; i < cells.Length; i++)
            {
                var border = new Border
                {
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(4, 2, 4, 2),
                    Background = isHeader
                        ? new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE6))
                        : Brushes.White
                };
                if (fixedHeight.HasValue)
                {
                    border.Height = fixedHeight.Value;
                }
                var tb = new TextBlock
                {
                    Text = cells[i] ?? "",
                    FontSize = fontSize,
                    FontWeight = isHeader ? FontWeights.Bold : FontWeights.Normal,
                    TextWrapping = TextWrapping.Wrap,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = i == 0 ? TextAlignment.Center : TextAlignment.Right,
                    FlowDirection = FlowDirection.RightToLeft
                };
                border.Child = tb;
                Grid.SetColumn(border, i);
                grid.Children.Add(border);
            }
            return grid;
        }

        private static TextBlock Line(string text, double size, bool bold)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = size,
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                TextAlignment = TextAlignment.Right,
                FlowDirection = FlowDirection.RightToLeft
            };
        }

        private FrameworkElement BuildSummary()
        {
            string text = string.Format(CultureInfo.InvariantCulture, Strings.RosterCount, _model.Count) + "‏";
            if (_model.PerCourtCounts.Count > 1)
            {
                var parts = new List<string>();
                foreach (KeyValuePair<string, int> kv in _model.PerCourtCounts)
                {
                    parts.Add((kv.Key ?? "") + ": " + kv.Value);
                }
                parts.Sort(StringComparer.Ordinal);
                text += "  (" + string.Join(" - ", parts.ToArray()) + ")";
            }
            TextBlock tb = Line(text, 14, true);
            tb.TextAlignment = TextAlignment.Center;
            return tb;
        }

        private FrameworkElement BuildSignatureBlock()
        {
            string[] captions = new string[]
            {
                Strings.RosterSigPrepared, Strings.RosterSigCustody,
                Strings.RosterSigChief
            };
            var grid = new Grid { FlowDirection = FlowDirection.RightToLeft };
            for (int i = 0; i < 3; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star)
                });
            }
            for (int i = 0; i < captions.Length; i++)
            {
                var box = new Border
                {
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(4),
                    Padding = new Thickness(6),
                    VerticalAlignment = VerticalAlignment.Top,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                var inner = new StackPanel();
                inner.Children.Add(Line(captions[i], 12, false));
                inner.Children.Add(Line(Strings.RosterSigCaption, 11, false));
                if (i == 0)
                {
                    inner.Children.Add(Line(_model.PreparerName ?? "", 12, true));
                }
                if (i == 1)
                {
                    inner.Children.Add(Line(_model.CustodyOfficerName ?? "", 12, true));
                }
                if (i == 2)
                {
                    inner.Children.Add(Line(_model.OfficerName ?? "", 12, true));
                }
                box.Child = inner;
                Grid.SetColumn(box, i);
                grid.Children.Add(box);
            }
            var boxes = new Grid { FlowDirection = FlowDirection.RightToLeft };
            boxes.Children.Add(grid);
            var panel = new StackPanel { FlowDirection = FlowDirection.RightToLeft };
            panel.Children.Add(boxes);
            panel.Children.Add(Line(Strings.RosterReceipt, 12, false));
            return panel;
        }

        private FrameworkElement BuildFooter(int index, int total)
        {
            DateTime now = _now();
            var panel = new Grid { FlowDirection = FlowDirection.RightToLeft };
            for (int i = 0; i < 3; i++)
            {
                panel.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star)
                });
            }
            var page = new TextBlock
            {
                Text = string.Format(CultureInfo.InvariantCulture, Strings.RosterPage, index + 1, total),
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                FlowDirection = FlowDirection.RightToLeft
            };
            var printed = new TextBlock
            {
                Text = string.Format(CultureInfo.InvariantCulture, Strings.RosterPrinted,
                    now.Day.ToString("00") + "/" + now.Month.ToString("00") + "/" + now.Year,
                    now.Hour.ToString("00") + ":" + now.Minute.ToString("00")),
                FontSize = 11,
                TextAlignment = TextAlignment.Right,
                FlowDirection = FlowDirection.RightToLeft
            };
            Grid.SetColumn(page, 1);
            Grid.SetColumn(printed, 0);
            panel.Children.Add(printed);
            panel.Children.Add(page);
            return panel;
        }

        private static string WeekdayOf(LocalDate date)
        {
            string[] wd = Strings.Weekdays;
            if (wd == null || wd.Length != 7)
            {
                return "";
            }
            return wd[(int)date.DayOfWeek];
        }
    }
}
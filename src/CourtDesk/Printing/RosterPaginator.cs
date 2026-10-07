using System;
using System.Collections.Generic;

namespace CourtDesk.Printing
{
    public sealed class RosterPage
    {
        public readonly List<int> RowIndices = new List<int>();
        public bool HasPageHeader;
        public bool HasSummary;
        public bool HasSignature;
    }

    public static class RosterGeometry
    {
        public const double PageWidth = 1122.5;
        public const double PageHeight = 793.7;
        public const double Margin = 45.4;
        public const double ContentWidth = PageWidth - (2 * Margin);
        public const double PageH = 703.0;
        public const double FooterH = 30.0;
        public const double TableHeaderH = 36.0;
        public const double Page1HeaderH = 132.0;
        public const double RunningTitleH = 30.0;
        public const double SummaryH = 26.0;
    }

    public sealed class RosterPaginator
    {
        private readonly int _count;
        private readonly Func<int, double> _measure;
        private readonly double _page1HeaderH;
        private readonly double _runningTitleH;
        private readonly double _summaryH;
        private readonly double _signatureH;
        private readonly double _footerH;
        private readonly double _pageH;
        private readonly double _tableHeaderH;

        public RosterPaginator(int count, Func<int, double> measure)
            : this(count, measure, RosterGeometry.Page1HeaderH, RosterGeometry.RunningTitleH,
                RosterGeometry.SummaryH, 118.0, RosterGeometry.FooterH,
                RosterGeometry.PageH, RosterGeometry.TableHeaderH)
        {
        }

        public RosterPaginator(int count, Func<int, double> measure,
            double page1HeaderH, double runningTitleH, double summaryH, double signatureH,
            double footerH, double pageH, double tableHeaderH)
        {
            _count = count < 0 ? 0 : count;
            _measure = measure ?? delegate { return 22.0; };
            _page1HeaderH = page1HeaderH;
            _runningTitleH = runningTitleH;
            _summaryH = summaryH;
            _signatureH = signatureH;
            _footerH = footerH;
            _pageH = pageH;
            _tableHeaderH = tableHeaderH;
        }

        public List<RosterPage> Paginate()
        {
            var pages = new List<RosterPage>();
            if (_count == 0)
            {
                var only = new RosterPage();
                only.HasPageHeader = true;
                only.HasSummary = true;
                only.HasSignature = true;
                pages.Add(only);
                return pages;
            }

            var current = new RosterPage();
            current.HasPageHeader = true;
            double avail = _pageH - _footerH - _page1HeaderH - _tableHeaderH;
            for (int i = 0; i < _count; i++)
            {
                double h = _measure(i);
                if (h > avail && current.RowIndices.Count >= 1)
                {
                    pages.Add(current);
                    current = new RosterPage();
                    avail = _pageH - _footerH - _runningTitleH - _tableHeaderH;
                }
                current.RowIndices.Add(i);
                avail -= h;
            }

            double need = _summaryH + _signatureH;
            if (need > avail)
            {
                if (current.RowIndices.Count >= 2)
                {
                    int moved = current.RowIndices[current.RowIndices.Count - 1];
                    current.RowIndices.RemoveAt(current.RowIndices.Count - 1);
                    pages.Add(current);
                    current = new RosterPage();
                    current.RowIndices.Add(moved);
                    avail = _pageH - _footerH - _runningTitleH - _tableHeaderH - _measure(moved);
                }
                if (_summaryH + _signatureH > avail)
                {
                    pages.Add(current);
                    current = new RosterPage();
                }
            }
            current.HasSummary = true;
            current.HasSignature = true;
            pages.Add(current);
            return pages;
        }

        public int PageCount
        {
            get { return Paginate().Count; }
        }
    }
}
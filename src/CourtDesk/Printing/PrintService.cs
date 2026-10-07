using System;
using System.Collections.Generic;
using System.Printing;
using System.Windows.Documents;
using System.Windows.Xps;
using CourtDesk.Application;
using CourtDesk.Application.Ports;

namespace CourtDesk.Printing
{
    public sealed class PrintService : IPrintService
    {
        private readonly IDatabase _db;
        private readonly IRosterPrintLog _log;
        private readonly RosterService _rosters;

        public PrintService(IDatabase db, IRosterPrintLog log, RosterService rosters)
        {
            _db = db;
            _log = log;
            _rosters = rosters;
        }

        public string LastError { get; private set; }

        public bool HasDefaultPrinter
        {
            get
            {
                try
                {
                    using (PrintQueue q = LocalPrintServer.GetDefaultPrintQueue())
                    {
                        return q != null;
                    }
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public static PrintTicket MakeTicket()
        {
            var ticket = new PrintTicket();
            ticket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA4, 297.0, 210.0);
            ticket.PageOrientation = PageOrientation.Landscape;
            ticket.CopyCount = 1;
            return ticket;
        }

        public object BuildDocument(RosterModel model)
        {
            if (model == null)
            {
                return null;
            }
            return new RosterDocumentBuilder(model).Build();
        }

        public int PageCount(RosterModel model)
        {
            if (model == null)
            {
                return 0;
            }
            return new RosterDocumentBuilder(model).PageCount;
        }

        public PrintOutcome PrintRoster(RosterModel model, bool direct)
        {
            LastError = null;
            if (model == null || model.Count == 0)
            {
                return PrintOutcome.EmptyRoster;
            }
            if (!direct)
            {
                return PrintOutcome.PreviewRequested;
            }
            if (!HasDefaultPrinter)
            {
                LastError = CourtDesk.Resources.Strings.PrintFail;
                return PrintOutcome.NoPrinter;
            }
            try
            {
                WriteDirect((FixedDocument)BuildDocument(model), MakeTicket());
            }
            catch (Exception)
            {
                LastError = CourtDesk.Resources.Strings.PrintFail;
                return PrintOutcome.Failed;
            }
            LogPrint(model);
            return PrintOutcome.Printed;
        }

        public static void WriteDirect(FixedDocument doc, PrintTicket ticket)
        {
            using (PrintQueue queue = LocalPrintServer.GetDefaultPrintQueue())
            {
                var writer = PrintQueue.CreateXpsDocumentWriter(queue);
                writer.Write(doc, ticket);
            }
        }

        public void LogPrint(RosterModel model)
        {
            try
            {
                List<int> ids = _rosters.DefendantIds(model);
                _db.Write(delegate(ISession s)
                {
                    _log.Append(s, model.Date.ToIso(), model.Count, ids, model.OperatorName);
                    return 0;
                });
            }
            catch (Exception)
            {
            }
        }
    }
}
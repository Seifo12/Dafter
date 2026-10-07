using System;
using System.Collections.Generic;

namespace CourtDesk.Application.Ports
{
    public interface IRosterPrintLog
    {
        void Append(ISession s, string rosterDateIso, int count,
            IReadOnlyList<int> defendantIds, string operatorName);
        int CountFor(ISession s, string rosterDateIso);
    }
}
using System.Collections.Generic;

namespace CourtDesk.Application
{
    public interface IChangeSeqSource
    {
        long CurrentChangeSeq();
        List<string> CheckInvariants();
    }
}

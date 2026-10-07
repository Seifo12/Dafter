using System.Collections.Generic;

namespace CourtDesk.Application.Ports
{
    public interface IChangeSeqSource
    {
        long CurrentChangeSeq();
        List<string> CheckInvariants();
    }
}

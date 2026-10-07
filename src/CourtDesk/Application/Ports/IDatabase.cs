using System;

namespace CourtDesk.Application.Ports
{
    public interface IDatabase
    {
        event Action<long> DataChanged;
        T Read<T>(Func<ISession, T> work);
        T Write<T>(Func<ISession, T> work);
    }
}

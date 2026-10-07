using System;
using System.Threading;

namespace CourtDesk.Platform
{
    public sealed class SingleInstanceGuard : IDisposable
    {
        private Mutex _mutex;
        private bool _owned;

        public bool IsPrimary { get; private set; }

        public string Name { get; private set; }

        public SingleInstanceGuard()
            : this("Global\\CourtDesk.SingleInstance")
        {
        }

        public SingleInstanceGuard(string name)
        {
            Name = name;
            try
            {
                _mutex = new Mutex(true, name, out _owned);
                IsPrimary = _owned;
            }
            catch (UnauthorizedAccessException)
            {
                _mutex = null;
                _owned = false;
                IsPrimary = true;
            }
            catch (Exception)
            {
                _mutex = null;
                _owned = false;
                IsPrimary = true;
            }
        }

        public void Dispose()
        {
            if (_mutex != null)
            {
                try
                {
                    if (_owned)
                    {
                        _mutex.ReleaseMutex();
                    }
                }
                catch (Exception)
                {
                }
                try
                {
                    _mutex.Dispose();
                }
                catch (Exception)
                {
                }
                _mutex = null;
            }
        }
    }
}
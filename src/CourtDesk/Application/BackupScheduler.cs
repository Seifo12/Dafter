using System;
using System.Threading;
using CourtDesk.Application.Ports;

namespace CourtDesk.Application
{
    public interface IBackupActions
    {
        void OnAutoSnapshot(string path);
        void OnExitSnapshot(string path);
        void OnSessionEnding();
        void OnUsbReminder();
    }

    public sealed class BackupScheduler
    {
        private readonly BackupService _backups;
        private readonly ILogger _log;
        private readonly IBackupActions _actions;
        private Timer _timer;
        private int _running;

        public int AutoMinutes { get; set; }

        public BackupScheduler(BackupService backups, ILogger log, IBackupActions actions)
        {
            _backups = backups;
            _log = log;
            _actions = actions;
            AutoMinutes = 30;
        }

        public void Start()
        {
            Stop();
            if (AutoMinutes <= 0)
            {
                return;
            }
            _timer = new Timer(OnTick, null,
                TimeSpan.FromMinutes(AutoMinutes), TimeSpan.FromMinutes(AutoMinutes));
        }

        public void Stop()
        {
            if (_timer != null)
            {
                try
                {
                    _timer.Dispose();
                }
                catch (Exception)
                {
                }
                _timer = null;
            }
        }

        private void OnTick(object state)
        {
            if (Interlocked.Exchange(ref _running, 1) == 1)
            {
                return;
            }
            try
            {
                BackupReport report = _backups.TakeAutoSnapshot();
                if (report != null && _actions != null)
                {
                    _actions.OnAutoSnapshot(report.Path);
                }
            }
            catch (Exception e)
            {
                _log.Error("backup.auto", e.Message);
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);
            }
        }

        public bool RunAutoSnapshotNow()
        {
            BackupReport report = _backups.TakeAutoSnapshot();
            if (report != null && _actions != null)
            {
                _actions.OnAutoSnapshot(report.Path);
                return true;
            }
            return false;
        }

        public bool RunExitSnapshotNow()
        {
            BackupReport report = _backups.TakeExitSnapshot();
            if (report != null && _actions != null)
            {
                _actions.OnExitSnapshot(report.Path);
                return true;
            }
            return false;
        }

        public void RunSessionEnding()
        {
            RunExitSnapshotNow();
            if (_actions != null)
            {
                _actions.OnSessionEnding();
            }
        }

        public bool UsbReminderDue()
        {
            try
            {
                return _backups.UsbReminderDue();
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
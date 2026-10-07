using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Threading;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;

namespace CourtDesk.Persistence
{
    public sealed class DbGateway : IDatabase, IChangeSeqSource, IDisposable
    {
        private readonly string _dbPath;
        private readonly IClock _clock;
        private readonly SynchronizationContext _marshal;
        private readonly BlockingCollection<WorkItem> _queue = new BlockingCollection<WorkItem>();
        private readonly Thread _thread;
        private SQLiteConnection _conn;
        private bool _disposed;

        public event Action<long> DataChanged;

        public DbGateway(string dbPath, IClock clock, SynchronizationContext marshal)
        {
            _dbPath = dbPath;
            _clock = clock;
            _marshal = marshal;
            _thread = new Thread(Loop);
            _thread.Name = "DbThread";
            _thread.IsBackground = true;
            _thread.Start();
            Exception openError = Enqueue(conn => { OpenOwned(); return 0; });
            if (openError != null)
            {
                throw new DbException(ErrorCodes.DbState, "db thread open failed", openError);
            }
        }

        public T Read<T>(Func<ISession, T> work)
        {
            return Run(ctx => work(ctx));
        }

        public T Write<T>(Func<ISession, T> work)
        {
            return Run(ctx =>
            {
                long before = Meta.GetLong(ctx.Connection, "change_seq");
                Execute(ctx.Connection, "BEGIN IMMEDIATE;");
                var wtx = new WriteTx(ctx.Connection, ctx.UtcNowIso, ctx.LocalDateIso);
                T result;
                try
                {
                    result = work(wtx);
                    BumpMeta(ctx.Connection, wtx.UtcNowIso);
                    Execute(ctx.Connection, "COMMIT;");
                }
                catch (Exception)
                {
                    try
                    {
                        Execute(ctx.Connection, "ROLLBACK;");
                    }
                    catch (SQLiteException)
                    {
                        Reconnect(ctx.Connection);
                        throw;
                    }
                    try
                    {
                        long after = Meta.GetLong(ctx.Connection, "change_seq");
                        if (after != before)
                        {
                            RaiseChanged(after);
                        }
                    }
                    catch (SQLiteException)
                    {
                        Reconnect(ctx.Connection);
                    }
                    throw;
                }
                long seq = Meta.GetLong(ctx.Connection, "change_seq");
                RaiseChanged(seq);
                return result;
            });
        }

        public void CheckpointTruncate()
        {
            Run(ctx =>
            {
                using (var cmd = new SQLiteCommand("PRAGMA wal_checkpoint(TRUNCATE);", ctx.Connection))
                {
                    cmd.ExecuteNonQuery();
                }
                return 0;
            });
        }

        public long CurrentChangeSeq()
        {
            return Run(ctx => Meta.GetLong(ctx.Connection, "change_seq"));
        }

        List<string> IChangeSeqSource.CheckInvariants()
        {
            return Run(ctx => IntegrityChecker.CheckInvariants(ctx.Connection));
        }

        public string GetMaxSeenUtc()
        {
            return Run(ctx => Meta.Get(ctx.Connection, "max_seen_utc"));
        }

        public List<string> CheckInvariants()
        {
            return Run(ctx => IntegrityChecker.CheckInvariants(ctx.Connection));
        }
        public void Close()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            try
            {
                EnqueueRaw(conn =>
                {
                    try
                    {
                        using (var cmd = new SQLiteCommand("PRAGMA wal_checkpoint(TRUNCATE);", conn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }
                    catch (SQLiteException) { }
                    conn.Close();
                    return null;
                });
            }
            finally
            {
                _queue.CompleteAdding();
                _thread.Join(10000);
            }
        }

        public void Dispose()
        {
            Close();
        }

        private T Run<T>(Func<DbContext, T> work)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("DbGateway");
            }
            string utc = FormatUtc(_clock.UtcNow);
            string local = _clock.Today.ToIso();
            object box = null;
            Exception error = Enqueue(conn =>
            {
                var ctx = new DbContext(conn, utc, local, false);
                box = work(ctx);
                return 0;
            });
            if (error != null)
            {
                throw Unwrap(error);
            }
            return (T)box;
        }

        private Exception Enqueue(Func<SQLiteConnection, object> fn)
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<object>();
            try
            {
                _queue.Add(new WorkItem(fn, tcs));
            }
            catch (InvalidOperationException e)
            {
                return e;
            }
            try
            {
                tcs.Task.GetAwaiter().GetResult();
                return null;
            }
            catch (Exception e)
            {
                return e;
            }
        }

        private object EnqueueRaw(Func<SQLiteConnection, object> fn)
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<object>();
            _queue.Add(new WorkItem(fn, tcs));
            return tcs.Task.GetAwaiter().GetResult();
        }

        private void Loop()
        {
            foreach (WorkItem item in _queue.GetConsumingEnumerable())
            {
                try
                {
                    object r = item.Fn(_conn);
                    item.Tcs.SetResult(r);
                }
                catch (Exception e)
                {
                    item.Tcs.SetException(e);
                }
            }
        }

        private void OpenOwned()
        {
            _conn = new SQLiteConnection(ConnectionString(_dbPath));
            _conn.Open();
            SqliteBootstrap.ApplyPragmas(_conn);
        }

        private static void Reconnect(SQLiteConnection conn)
        {
            try { conn.Close(); }
            catch (SQLiteException) { }
            conn.Open();
            SqliteBootstrap.ApplyPragmas(conn);
        }

        internal static string ConnectionString(string dbPath)
        {
            return "Data Source=" + dbPath + ";Version=3;Pooling=False;Default Timeout=5";
        }

        internal static void Execute(SQLiteConnection conn, string sql)
        {
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.ExecuteNonQuery();
            }
        }

        private static void BumpMeta(SQLiteConnection conn, string nowUtc)
        {
            using (var cmd = new SQLiteCommand(
                "UPDATE app_meta SET value = CAST(value AS INTEGER) + 1 WHERE key = 'change_seq'", conn))
            {
                cmd.ExecuteNonQuery();
            }
            Meta.Set(conn, "max_seen_utc", nowUtc);
        }

        private void RaiseChanged(long seq)
        {
            Action<long> h = DataChanged;
            if (h == null)
            {
                return;
            }
            if (_marshal == null)
            {
                h(seq);
            }
            else
            {
                _marshal.Post(state => h(seq), null);
            }
        }

        private static Exception Unwrap(Exception e)
        {
            if (e is DbException || e is ConcurrencyException || e is ValidationException
                || e is InvariantException || e is ObjectDisposedException)
            {
                return e;
            }
            if (e is SQLiteException)
            {
                return new DbException(ErrorCodes.DbWrite, "sqlite: " + e.Message, e);
            }
            return e;
        }

        public static string FormatUtc(DateTime utc)
        {
            return utc.ToString("yyyy-MM-ddTHH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);
        }

        private sealed class WorkItem
        {
            public readonly Func<SQLiteConnection, object> Fn;
            public readonly System.Threading.Tasks.TaskCompletionSource<object> Tcs;

            public WorkItem(Func<SQLiteConnection, object> fn, System.Threading.Tasks.TaskCompletionSource<object> tcs)
            {
                Fn = fn;
                Tcs = tcs;
            }
        }
    }
}

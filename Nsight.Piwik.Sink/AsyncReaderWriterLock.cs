using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//  Sourced from http://blogs.msdn.com/b/pfxteam/archive/2012/02/12/10267069.aspx

namespace Nsight.Piwik.Sink
{
    /// <summary>
    /// Reader/writer lock that works with TPL.
    /// </summary>
    internal sealed class AsyncReaderWriterLock
    {
        private readonly Task<Releaser> m_readerReleaser;
        private readonly Task<Releaser> m_writerReleaser;

        private readonly Queue<TaskCompletionSource<Releaser>> m_waitingWriters = new Queue<TaskCompletionSource<Releaser>>();
        private TaskCompletionSource<Releaser> m_waitingReader = new TaskCompletionSource<Releaser>();
        private int m_readersWaiting;

        /// <summary>
        /// The value of 0 means that no one has acquired the lock, a value of –1 means that a writer has acquired the lock, and 
        /// a positive value means that one or more readers have acquired the lock, where the positive value indicates how many.
        /// </summary>
        private int m_status;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncReaderWriterLock"/> class.
        /// </summary>
        public AsyncReaderWriterLock()
        {
            m_readerReleaser = Task.FromResult(new Releaser(this, false));
            m_writerReleaser = Task.FromResult(new Releaser(this, true));
        }

        /// <summary>
        /// Acquires reader lock.
        /// </summary>
        public Task<Releaser> ReaderLockAsync()
        {
            lock (m_waitingWriters)
            {
                if (m_status >= 0 && m_waitingWriters.Count == 0)
                {
                    ++m_status;
                    return m_readerReleaser;
                }
                else
                {
                    ++m_readersWaiting;
                    return m_waitingReader.Task.ContinueWith(t => t.Result);
                }
            }
        }

        /// <summary>
        /// Acquires writer lock.
        /// </summary>
        public Task<Releaser> WriterLockAsync()
        {
            lock (m_waitingWriters)
            {
                if (m_status == 0)
                {
                    m_status = -1;
                    return m_writerReleaser;
                }
                else
                {
                    var waiter = new TaskCompletionSource<Releaser>();
                    m_waitingWriters.Enqueue(waiter);

                    return waiter.Task;
                }
            }
        }

        /// <summary>
        /// Releases reader.
        /// </summary>
        private void ReaderRelease()
        {
            TaskCompletionSource<Releaser> toWake = null;

            lock (m_waitingWriters)
            {
                --m_status;
                if (m_status == 0 && m_waitingWriters.Count > 0)
                {
                    m_status = -1;
                    toWake = m_waitingWriters.Dequeue();
                }
            }

            if (toWake != null)
            {
                toWake.SetResult(new Releaser(this, true));
            }
        }

        /// <summary>
        /// Releases writer.
        /// </summary>
        private void WriterRelease()
        {
            TaskCompletionSource<Releaser> toWake = null;
            bool toWakeIsWriter = false;

            lock (m_waitingWriters)
            {
                if (m_waitingWriters.Count > 0)
                {
                    toWake = m_waitingWriters.Dequeue();
                    toWakeIsWriter = true;
                }
                else if (m_readersWaiting > 0)
                {
                    toWake = m_waitingReader;
                    m_status = m_readersWaiting;
                    m_readersWaiting = 0;
                    m_waitingReader = new TaskCompletionSource<Releaser>();
                }
                else
                {
                    m_status = 0;
                }
            }

            if (toWake != null)
            {
                toWake.SetResult(new Releaser(this, toWakeIsWriter));
            }
        }

        /// <summary>
        /// Releases lock on dispose.
        /// </summary>
        public struct Releaser : IDisposable
        {
            private readonly AsyncReaderWriterLock m_toRelease;
            private readonly bool m_writer;

            /// <summary>
            /// Initializes a new instance of the <see cref="Releaser"/> class.
            /// </summary>
            /// <param name="toRelease">Lock to release.</param>
            /// <param name="writer">Indicates whether the lock is a writer lock.</param>
            internal Releaser(AsyncReaderWriterLock toRelease, bool writer)
            {
                m_toRelease = toRelease;
                m_writer = writer;
            }

            #region IDisposable Members

            public void Dispose()
            {
                if (m_toRelease != null)
                {
                    if (m_writer)
                    {
                        m_toRelease.WriterRelease();
                    }
                    else
                    {
                        m_toRelease.ReaderRelease();
                    }
                }
            }

            #endregion
        }
    }
}

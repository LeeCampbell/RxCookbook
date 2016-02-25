using System;
using System.Reactive.Disposables;
using System.Threading;

namespace RxCookbook.DisposableOptimisation
{
    /// <summary>
    /// Represents a disposable resource whose underlying disposable resource can be replaced by another disposable resource, causing automatic disposal of the previous underlying disposable resource.
    /// </summary>
    public sealed class SerialDisposableLockFree2 : ICancelable
    {
        private IDisposable _current;
        private bool _hasDisposeBeenRequested = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Reactive.Disposables.SerialDisposable"/> class.
        /// </summary>
        public SerialDisposableLockFree2()
        {
        }

        /// <summary>
        /// Gets or sets the underlying disposable.
        /// </summary>
        /// <remarks>
        /// If the SerialDisposable has already been disposed, assignment to this property causes immediate disposal of the given disposable object.
        /// Assigning this property disposes the previous disposable object.
        /// </remarks>
        public IDisposable Disposable
        {
            get { return _current; }
            set
            {
                if (_hasDisposeBeenRequested)
                {
                    if (value != null)
                    {
                        value.Dispose();
                    }
                }
                else
                {
                    var old = Interlocked.Exchange(ref _current, value);

                    if (old != null)
                        old.Dispose();

                    //Mitigate race condition
                    if (_hasDisposeBeenRequested)
                    {
                        PerformDisposal();
                    }
                }
            }
        }

        /// <summary>
        /// Disposes the underlying disposable as well as all future replacements.
        /// </summary>
        public void Dispose()
        {
            _hasDisposeBeenRequested = true;
            PerformDisposal();
        }

        private void PerformDisposal()
        {
            var old = Interlocked.Exchange(ref _current, null);
            if (old != null)
                old.Dispose();
        }

        //Note really accurate. -LC
        public bool IsDisposed => _hasDisposeBeenRequested;
    }
}
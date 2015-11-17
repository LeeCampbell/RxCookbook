using System;
using System.Reactive.Disposables;
using System.Threading;

namespace RxCookbook.DisposableOptimisation
{
    /// <summary>
    /// Represents a disposable resource whose underlying disposable resource can be replaced by another disposable resource, causing automatic disposal of the previous underlying disposable resource.
    /// </summary>
    public sealed class SerialDisposableLockFree1 : ICancelable
    {
        private IDisposable _current;

        public SerialDisposableLockFree1()
        {
        }

        public bool IsDisposed => _current == Disposed.Instance;

        /// <summary>
        /// Gets or sets the underlying disposable.
        /// </summary>
        /// <remarks>If the SerialDisposable has already been disposed, assignment to this property causes immediate disposal of the given disposable object. Assigning this property disposes the previous disposable object.</remarks>
        public IDisposable Disposable
        {
            get
            {
                return _current;
            }

            set
            {
                IDisposable previous;
                do
                {
                    previous = _current;
                    if (ReferenceEquals(previous, Disposed.Instance)) break;
                }
                //If the location is still set to the value we just checked (previous), it will get replaced. Else, try again.
                while (!ReferenceEquals(Interlocked.CompareExchange(ref _current, value, previous), previous));
                var wasReplaced = !ReferenceEquals(previous, Disposed.Instance);

                if (wasReplaced)
                {
                    if (previous != null)
                        previous.Dispose();
                }
                else
                {
                    if (value != null)
                        value.Dispose();
                }

            }
        }

        /// <summary>
        /// Disposes the underlying disposable as well as all future replacements.
        /// </summary>
        public void Dispose()
        {
            var old = Interlocked.Exchange(ref _current, Disposed.Instance);
            old?.Dispose();
        }

        private sealed class Disposed : IDisposable
        {
            public static readonly Disposed Instance = new Disposed();
            private Disposed()
            { }
            public void Dispose()
            { }
        }
    }
}

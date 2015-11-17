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
                //If Disposed, then dispose of value, 
                //else, store value, dispose old.
                var previous = _current;
                var wasReplaced = InterlockedConditionalReplace(ref _current, value, disposable => disposable != Disposed.Instance);
                if (wasReplaced)
                {
                    previous?.Dispose();
                }
                else
                {
                    value?.Dispose();
                }
            }
        }
        private static bool InterlockedConditionalReplace<T>(ref T location, T newValue, Func<T, bool> predicate) where T : class
        {
            T initialValue;
            do
            {
                initialValue = location;
                if (predicate(initialValue)) return false;
            }
            while (Interlocked.CompareExchange(ref location, newValue, initialValue) != initialValue);
            return true;
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

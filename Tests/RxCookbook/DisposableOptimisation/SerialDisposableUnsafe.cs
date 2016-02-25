using System;
using System.Reactive.Disposables;

namespace RxCookbook.DisposableOptimisation
{
    public sealed class SerialDisposableUnsafe : ICancelable
    {
        private IDisposable _current;

        public SerialDisposableUnsafe()
        {
        }

        public bool IsDisposed { get; private set; }

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
                if (IsDisposed)
                {
                    value?.Dispose();
                }
                else
                {
                    var previous = _current;
                    _current = value;
                    previous?.Dispose();
                }
                
            }
        }
        
        /// <summary>
        /// Disposes the underlying disposable as well as all future replacements.
        /// </summary>
        public void Dispose()
        {
            IsDisposed = true;
            _current?.Dispose();
        }        
    }
}
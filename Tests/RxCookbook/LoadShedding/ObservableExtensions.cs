using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;

namespace RxCookbook.LoadShedding
{
    public static class ObservableExtensions
    {
        //Code taken from the ObserveOn(this IObservable<T> source, IScheduler schedule) v1 implementation. Queue<T> has been replaced with a field of T.
        //  Some renaming for improved readability
        public static IObservable<T> ObserveLatestOn<T>(this IObservable<T> source, IScheduler scheduler)
        {
            return Observable.Create<T>(observer =>
            {
                //Replace the queue with just the single notification;
                var gate = new object();
                bool active = false;

                var cancelable = new SerialDisposable();
                var disposable = source.Materialize()
                    .Subscribe(thisNotification =>
                        {
                            bool alreadyActive;
                            Notification<T> outsideNotification;
                            lock (gate)
                            {
                                alreadyActive = active;
                                active = true;
                                outsideNotification = thisNotification;
                            }

                            if (!alreadyActive)
                            {
                                if (!cancelable.IsDisposed)
                                    cancelable.Disposable = scheduler.Schedule(
                                        self =>
                                        {
                                            Notification<T> localNotification;
                                            lock (gate)
                                            {
                                                localNotification = outsideNotification;
                                                outsideNotification = null;
                                            }
                                            localNotification.Accept(observer);
                                            bool hasPendingNotification;
                                            lock (gate)
                                            {
                                                hasPendingNotification = active = (outsideNotification != null);
                                            }
                                            if (hasPendingNotification)
                                            {
                                                if (!cancelable.IsDisposed)
                                                    self();
                                            }
                                        });
                            }
                            //Edge case where (yet to be explained) the recursive scheduler never fires.
                            else if (outsideNotification.Kind != NotificationKind.OnNext)
                            {
                                if(!cancelable.IsDisposed)
                                    cancelable.Disposable = scheduler.Schedule(thisNotification, (self, notification) =>
                                    {
                                        notification.Accept(observer);
                                        return Disposable.Empty;
                                    });
                            }
                        });
                return new CompositeDisposable(disposable, cancelable);
            });
        }

        public static IObservable<T> ObserveLatestOnCAS<T>(this IObservable<T> source, IScheduler scheduler)
        {
            return Observable.Create<T>(observer =>
            {
                //Replace the queue with just the single notification;
                var gate = new object();
                bool active = false;

                var cancelable = new SerialDisposable();
                var disposable = source.Materialize()
                    .Subscribe(thisNotification =>
                        {
                            bool alreadyActive;
                            Notification<T> outsideNotification;
                            //Can this be a CAS operation
                            lock (gate)
                            {
                                alreadyActive = active;
                                active = true;
                                outsideNotification = thisNotification;
                            }

                            if (!alreadyActive)
                            {
                                if (!cancelable.IsDisposed)
                                    cancelable.Disposable = scheduler.Schedule(
                                        self =>
                                        {
                                            Notification<T> localNotification;
                                            //Can this be a CAS operation
                                            lock (gate)
                                            {
                                                localNotification = outsideNotification;
                                                outsideNotification = null;
                                            }
                                            localNotification.Accept(observer);
                                            bool hasPendingNotification;
                                            //Can this be a CAS operation
                                            lock (gate)
                                            {
                                                hasPendingNotification = active = (outsideNotification != null);
                                            }
                                            if (hasPendingNotification)
                                            {
                                                if (!cancelable.IsDisposed)
                                                    self();
                                            }
                                        });
                            }
                            //Edge case where (yet to be explained) the recursive scheduler never fires.
                            else if (outsideNotification.Kind != NotificationKind.OnNext)
                            {
                                if(!cancelable.IsDisposed)
                                    cancelable.Disposable = scheduler.Schedule(thisNotification, (self, notification) =>
                                    {
                                        notification.Accept(observer);
                                        return Disposable.Empty;
                                    });
                            }
                        });
                return new CompositeDisposable(disposable, cancelable);
            });
        }

        public static IObservable<T> ObserveLatestOnOptimisedSerialDisposable<T>(this IObservable<T> source, IScheduler scheduler)
        {
            return Observable.Create<T>(observer =>
            {
                //Replace the queue with just the single notification;
                var gate = new object();
                bool active = false;

                var cancelable = new LockFreeSerialDisposable();
                var disposable = source.Materialize()
                    .Subscribe(thisNotification =>
                        {
                            bool alreadyActive;
                            Notification<T> outsideNotification;
                            //Can this be a CAS operation
                            lock (gate)
                            {
                                alreadyActive = active;
                                active = true;
                                outsideNotification = thisNotification;
                            }

                            if (!alreadyActive)
                            {
                                if (!cancelable.IsDisposed)
                                    cancelable.Disposable = scheduler.Schedule(
                                        self =>
                                        {
                                            Notification<T> localNotification;
                                            //Can this be a CAS operation
                                            lock (gate)
                                            {
                                                localNotification = outsideNotification;
                                                outsideNotification = null;
                                            }
                                            localNotification.Accept(observer);
                                            bool hasPendingNotification;
                                            //Can this be a CAS operation
                                            lock (gate)
                                            {
                                                hasPendingNotification = active = (outsideNotification != null);
                                            }
                                            if (hasPendingNotification)
                                            {
                                                if (!cancelable.IsDisposed)
                                                    self();
                                            }
                                        });
                            }
                            //Edge case where (yet to be explained) the recursive scheduler never fires.
                            else if (outsideNotification.Kind != NotificationKind.OnNext)
                            {
                                if(!cancelable.IsDisposed)
                                    cancelable.Disposable = scheduler.Schedule(thisNotification, (self, notification) =>
                                    {
                                        notification.Accept(observer);
                                        return Disposable.Empty;
                                    });
                            }
                        });
                return new CompositeDisposable(disposable, cancelable);
            });
        }

        public static IObservable<T> TakeMostRecent<T>(this IObservable<T> source, IScheduler scheduler)
        {
            return source.Latest().ToObservable(scheduler);
        }
    }

    /// <summary>
    /// Represents a disposable resource whose underlying disposable resource can be replaced by another disposable resource, causing automatic disposal of the previous underlying disposable resource.
    /// </summary>
    public sealed class LockFreeSerialDisposable : ICancelable
    {
        private IDisposable _current;
        
        public LockFreeSerialDisposable()
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
                if(predicate(initialValue)) return false;
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
            {}
            public void Dispose()
            {}
        }

        
    }
}

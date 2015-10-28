using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

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

        public static IObservable<T> TakeMostRecent<T>(this IObservable<T> source, IScheduler scheduler)
        {
            return source.Latest().ToObservable(scheduler);
        }
    }
}

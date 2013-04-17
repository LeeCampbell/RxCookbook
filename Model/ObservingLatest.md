#Observing the latest value

There are numerous cases where you will be consuming an observable sequence, but maybe you are consuming the values slower than they are being produced.
Sometimes you just want to have the latest value and ignore any other values that may have arrived between your last consumed value and the most recently produced value.

Imagine that for example we have a WPF application that is displaying fast moving pricing data.
Consider the scenario where we recieve 5 prices in 100ms.

    price1 1.01
    price2 1.02
    price3 1.03
    price4 1.04
    price5 1.05

When we recieve the first price, we display it on the UI.
While the dispatcher is rendering this update, it is tasked with doing some other work before our next price arrives.
If this work takes some time, i.e. over 100ms we will have 4 other prices queued up to display.
Once the dispatcher is ready to show the next price (1.02), the price is actually out of date.
We want to display the most recent price (1.05).

The solution i have used before is to take pull apart the `ObserveOn(IScheduler)` method and replace the internal queue with a single backing field.

  	public static IObservable<TSource> ObserveLatestOn<TSource>(this IObservable<TSource> source, IScheduler scheduler)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (scheduler == null) throw new ArgumentNullException("scheduler");
			
			return Observable.CreateWithDisposable<TSource>(observer =>
			{
					//var q = new Queue<Notification<TSource>>();
					//var q = new Stack<Notification<TSource>>();
					Notification<TSource> q = null;
					var gate = new object();
					bool active = false;
					var cancelable = new MutableDisposable();
					var disposable = source.Materialize().Subscribe(delegate(Notification<TSource> n)
							{
									bool flag;
									lock (gate)
									{
											flag = !active;
											active = true;
											//q.Enqueue(n);
											//q.Push(n);
											q = n;
									}
		
		
									if (flag)
									{
											cancelable.Replace(scheduler.Schedule(self =>
											{
													Notification<TSource> notification = null;
													lock (gate)
													{
															//notification = q.Dequeue();
															notification = q;
															q = null;
															//notification = q.Pop();
															//q.Clear();
													}
													notification.Accept(observer);
													bool flag2 = false;
													lock (gate)
													{
															flag2 = active = (q != null);
													}
													if (flag2)
													{
															self();
													}
											}));
									}
							});
					return new CompositeDisposable(new[] { disposable, cancelable });
			});
		}


###Links
http://social.msdn.microsoft.com/Forums/en-US/rx/thread/bbcc1af9-64b4-456b-9038-a540cb5f5de5



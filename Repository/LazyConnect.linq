<Query Kind="Program">
  <NuGetReference>Microsoft.Reactive.Testing</NuGetReference>
  <Namespace>Microsoft.Reactive.Testing</Namespace>
  <Namespace>System</Namespace>
  <Namespace>System.Linq</Namespace>
  <Namespace>System.Reactive</Namespace>
  <Namespace>System.Reactive.Concurrency</Namespace>
  <Namespace>System.Reactive.Disposables</Namespace>
  <Namespace>System.Reactive.Joins</Namespace>
  <Namespace>System.Reactive.Linq</Namespace>
  <Namespace>System.Reactive.PlatformServices</Namespace>
  <Namespace>System.Reactive.Subjects</Namespace>
  <Namespace>System.Reactive.Threading.Tasks</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

void Main()
{
	//Tests
	FirstSubscriberConnectsSequence();
	SecondSubscriberGetsSharedData();
	ResurectionKeepsOriginalSubscription();
	CanDisposeTheConnection();
}

// Define other methods and classes here


public static class ObservableExtensions
{
	/*For first 2 tests
	public static IObservable<T> LazyConnect<T>(this IConnectableObservable<T> source)
	{
		return source.RefCount();
	}*/
	
	/// <summary>
	/// Returns an observable sequence that connects on first subscription. Connection is terminated by the provided <see cref="SingleAssignmentDisposable"/>.
	/// </summary>
	/// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
	/// <param name="source">The source sequence that will be connected on first subscription.</param>
	/// <param name="connection">The resource that the connection will be attached to.</param>
	/// <returns>An observable sequence that connects on first subscription.</returns>
	public static IObservable<T> LazyConnect<T>(this IConnectableObservable<T> source, SingleAssignmentDisposable connection)
	{
		int isConnected = 0;
		return Observable.Create<T>(
			o =>
			{
				var subscription = source.Subscribe(o);
				var isDisconnected = Interlocked.CompareExchange(ref isConnected, 1, 0) == 0;
				if (isDisconnected)
					connection.Disposable = source.Connect();
				return subscription;
			});
	}
}

#region Tests
public void FirstSubscriberConnectsSequence()
{
	var testScheduler = new TestScheduler();
	var source = Observable.Timer(TimeSpan.FromSeconds(5), testScheduler)
		.Timestamp(testScheduler)
		.Replay(1)
		.LazyConnect(new SingleAssignmentDisposable());
	
	var observer = testScheduler.CreateObserver<Timestamped<long>>();
	
	testScheduler.AdvanceTo(60.Seconds());
	
	source.Subscribe(observer);
	testScheduler.Start();
		
	//If we connect after 60s and the value take 5s to be produced, we should recieve it at 65s
	ReactiveAssert.AssertEqual(observer.Messages,
		ReactiveTest.OnNext(65.Seconds(), new Timestamped<long>(0L, 65.Seconds().SinceEpoch())),
		ReactiveTest.OnCompleted<Timestamped<long>>(65.Seconds())
	);
}
public void SecondSubscriberGetsSharedData()
{
	var testScheduler = new TestScheduler();
	var source = Observable.Timer(TimeSpan.FromSeconds(5), testScheduler)
		.Timestamp(testScheduler)
		.Replay(1)
		.LazyConnect(new SingleAssignmentDisposable());

	var observer1 = testScheduler.CreateObserver<Timestamped<long>>();
	var observer2 = testScheduler.CreateObserver<Timestamped<long>>();

	testScheduler.Schedule(TimeSpan.FromSeconds(60), ()=>source.Subscribe(observer1));
	testScheduler.Schedule(TimeSpan.FromSeconds(120), ()=>source.Subscribe(observer2));
	
	testScheduler.Start();

	//We subscribe at 120s and should immediately get the cached value.
	//	The cached value was producd at 65s (and timestamped as such).
	//	We should recieve it as soon as we subscribe at 120s.
	ReactiveAssert.AssertEqual(observer2.Messages,
		ReactiveTest.OnNext(120.Seconds(), new Timestamped<long>(0L, 65.Seconds().SinceEpoch())),
		ReactiveTest.OnCompleted<Timestamped<long>>(120.Seconds())
	);
}
public void ResurectionKeepsOriginalSubscription()
{
	var testScheduler = new TestScheduler();
	var source = Observable.Timer(TimeSpan.FromSeconds(5), testScheduler)
		.Timestamp(testScheduler)
		.Replay(1)
		.LazyConnect(new SingleAssignmentDisposable());

	var observer1 = testScheduler.CreateObserver<Timestamped<long>>();
	var observer2 = testScheduler.CreateObserver<Timestamped<long>>();

	testScheduler.Schedule(TimeSpan.FromSeconds(60), () => source.Take(1).Subscribe(observer1));
	testScheduler.Schedule(TimeSpan.FromSeconds(120), () => source.Subscribe(observer2));

	testScheduler.Start();

	//We subscribe at 120s and should immediately get the cached value.
	//	The cached value was producd at 65s (and timestamped as such).
	//	We should recieve it as soon as we subscribe at 120s.
	ReactiveAssert.AssertEqual(observer2.Messages,
		ReactiveTest.OnNext(120.Seconds(), new Timestamped<long>(0L, 65.Seconds().SinceEpoch())),
		ReactiveTest.OnCompleted<Timestamped<long>>(120.Seconds())
	);
}
public void CanDisposeTheConnection()
{
	var testScheduler = new TestScheduler();
	var connection = new SingleAssignmentDisposable();
	var source = testScheduler.CreateColdObservable<int>();

	//First subscriber will connect the sequence.
	source.Replay(1)
		.LazyConnect(connection)
		.Subscribe();
	
	connection.Dispose();

	ReactiveAssert.AssertEqual(source.Subscriptions,
		new Subscription(0,0));	
}
#endregion

#region Test helpers
public static class TemporalExtensions
{
	public static long Seconds(this int seconds)
	{
		return TimeSpan.FromSeconds(seconds).Ticks;
	}
	public static DateTimeOffset SinceEpoch(this long ticks)
	{
		return DateTimeOffset.MinValue.AddTicks(ticks);
	}
}
#endregion
# Lazily Connecting

Rx exposes the `IConnectableObservable<T>` interface for scenarios where the subscription is not the trigger to start production of values.
It is often used in case of sharing sequences by multicasting the data.

For example if I want to expose the events from a temperature sensor, we could share that data via Rx instead of each consumer subscribing directly to the sensor.
In addition to this we could `Replay(1)` this sequence so that future consumers would immediately get the most recent temperature.

This kind of sharing is most useful when there is a significant cost to each subscription.
Some subscriptions may require a network request, server side processing, transport of messages,  and deserialization of messages for each subscription.
Now if each subscription was in effect going to get back the same data, this is obviously wasteful.
In this case, connecting once and amortizing the cost across multiple consumers makes sense.

In contrast, if the observable sequence is merely a .NET event being converted to an observable sequence, then the cost of subscription is negligible, so sharing/publishing/multicasting would probably be an unnecessary complication to add.

## How to share an observable sequence
There are numerous ways to share an observable sequence as an `IConnectableObservable<T>` instance.
The most generalized way is to use the `Multicast(ISubject<T>)` operator.
You can pass in implementations of each of the `ISubject<T>` types, or more popularly you can use the convenience methods like `Publish()`, `PublishLast()` & `Replay()`.
Sharing and Multicasting sequences is covered in more detail at http://introtorx.com/Content/v1.0.10621.0/14_HotAndColdObservables.html

## Standard usage of IConnectableObservable<T>

Common usage of a `IConnectableObservable<T>` is to create it with the `Publish()` operator, and then automatically connect it with the `RefCount()` operator.
This way when the first consumer subscribes, the cost of the connection is paid.
Subsequent consumers will just see the sequence as a hot observable and receive any future values.
Another common alternative is instead of using `Publish()`, is to use `Replay(1)` so that subsequent consumers will also get the most recent value as well as all future values.

### RefCount
The use of `RefCount()` will deal with calling `Connect()` on the underlying `IConnectableObservable<T>` for us.
As explained above, this will be performed when the first subscription is made.
As the name indicates, once all the subscriptions have been terminated, the connection is disposed.
If after the connection has been dispose, another subscription is made, then the connection is reestablished.
Effectively, `Connect()` is called when the subscription count goes from 0 to 1.
The connections is disposed when the subscription count goes from 1 to 0.

### Problems with RefCount
This automatic reference counting is incredibly useful.
However, in some scenarios we want the lazy connecting of the sequence that `RefCount()` gives us, but not the automatic disposal of the connection.
For example we may know that sometimes subscription counts may drop to 0 but only briefly.
Other scenarios we may want the lazy connection, but are happy to manually dispose of the connection ourselves.

## Lazy Connect
I find that this can be very useful for important but slow moving data like configuration, permissions and calendars (in financial domains).
In my experience each of these types of data, I want to know about when it changes, and it will access it regularly in my application.
Therefore it fits perfectly with a `Replay(1)` implementation.
When a specific module is loaded, then the cached sequence can be made available.
On first subscription the sequence is connected.
Once the data is produced, the most recent value is cached.
All subsequent requests will get the cached data immediately and any future changes.
Finally when the module is unloaded, the connection can be disposed.
This design prevents us from incurring a chatty exchange each time the subscription count move from zero or back to zero.

So how do we implement a lazy connecting sequence?
Well lets start with some tests to define our behavior.

### First subscriber connects sequence
The first subscriber, like with `RefCount()`, should call `Connect()` on the underlying `IConnectableObservable<T>` instance.
I will not perform any mocking, but instead assert on the behavior.

```csharp
public void FirstSubscriberConnectsSequence()
{
	var testScheduler = new TestScheduler();
	var source = Observable.Timer(TimeSpan.FromSeconds(5), testScheduler)
		.Timestamp(testScheduler)
		.Replay(1)
		.LazyConnect();

	var observer = testScheduler.CreateObserver<Timestamped<long>>();

	testScheduler.AdvanceTo(60.Seconds());

	source.Subscribe(observer);
	testScheduler.Start();

	ReactiveAssert.AssertEqual(observer.Messages,
		ReactiveTest.OnNext(65.Seconds(), new Timestamped<long>(0L, 65.Seconds().SinceEpoch())),
		ReactiveTest.OnCompleted<Timestamped<long>>(65.Seconds())
	);
}
```

To make this test pass I can simply delegate stright through to the `RefCount()` operator.
I know this is not what I will need eventually, but we should aim to do the minimum to make the test pass.

```csharp
public static IObservable<T> LazyConnect<T>(this IConnectableObservable<T> source)
{
  return source.RefCount();
}
```

### Second subscriber gets shared sequence
When a subsequent subscription is made, it should subscribe to the shared instance.
In our case we are using a `Replay(1)` as the underlying, so we should receive a notification immediately upon subscribing.

```csharp
public void SecondSubscriberGetsSharedData()
{
	var testScheduler = new TestScheduler();
	var source = Observable.Timer(TimeSpan.FromSeconds(5), testScheduler)
		.Timestamp(testScheduler)
		.Replay(1)
		.LazyConnect();

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
```

This test will also pass while we just delegate through to the `RefCount()` operator.

### Resurrection reuses shared connection
Unlike the `RefCount()` operator, when the subscription count drops to zero, we do not want to dispose of the connection.
Instead, subsequent subscriptions should still connect to the underlying shared sequence.

Here the first subscription now has a `Take(1)` clause that will dispose of its subscription as soon as it receives the first value.
We assert that the second subscription still receives the same data regardless of if the first subscription is still subscribed or not.

```csharp
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
```

Now we need to stop using the `RefCount()` operator and actually implement a solution.
Here we set a flag to indicate if the sequence has been connected or not, and simply `Connect()` the underlying sequence on the first subscription.

```csharp
public static IObservable<T> LazyConnect<T>(this IConnectableObservable<T> source)
{
  int isConnected = 0;
  return Observable.Create<T>(
    o =>
    {
      var subscription = source.Subscribe(o);
      var isDisconnected = Interlocked.CompareExchange(ref isConnected, 1, 0) == 0;
      if (isDisconnected)
        source.Connect();
      return subscription;
    });
}
```

### Disposal of the connection
The solution above, has an obvious flaw: The `IDisposable` instance returned from the `Connect()` operator is not captured.
As it is not captured, we can never dispose of it.
Thus we have a resource leak.

We have a small problem here.
As the extension method returns an `IObservable<T>` already, how can we also return the instance of `IDisposable`?
One option would be to have an `out` parameter.
However this wouldn't work as the value cant be guarantee to be set on exit of the method.
Instead I prefer to require a `SingleAssignmentDisposable` instance to be provided to the method.
This type implements `IDisposable` and allows you to assign an instance of an `IDisposable` to it, but only once.
When you dispose of the `SingleAssignmentDisposable` instance, it will dispose of the assigned instance if it has been set.
If the assignment happens after the `SingleAssignmentDisposable` instance has been disposed, then the assigned disposable instance will be disposed.

The signature of our method is now updated to

```csharp
public static IObservable<T> LazyConnect<T>(this IConnectableObservable<T> source, SingleAssignmentDisposable connection)
	{
  }
```

The test then will inspect now use the `TestScheduler`'s `CreateColdObservable` factory method to create the underlying sequence.
We will then be able to interrogate the `Subscription` property to assert that subscription was made and that it was also disposed of.

```csharp
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
```

#Final implementation

Now our final implementation is updated to capture the connection.

```csharp
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
```

The full [LinqPad](http://www.linqpad.net) sample in available as [LazyConnect.linq](LazyConnect.linq)



See also :
 * http://introtorx.com/Content/v1.0.10621.0/14_HotAndColdObservables.html
 * https://leecampbell.com/2014/01/05/replaysubject-performance-improvements/

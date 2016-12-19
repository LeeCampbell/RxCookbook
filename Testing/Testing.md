# Testing in Rx

> This is an excerpt from the Practical Rx training course  
> For more information on the Practical Rx training course see https://leecampbell.com/training/

To ensure quality in our software, testing has taking a front row seat in recent years.
It has been proven that the earlier a bug can be found in the software delivery life cycle, the cheaper and faster it can be fixed.
Also value can be found in automation testing as a form of documentation for the code base.

For testing to be effective it needs to have the following characteristics. Test should be:

- Fast
- Descriptive
- Predictable
- Repeatable

This can prove to be very difficult in concurrent systems.

## Testing Multi-threaded software

Testing asynchronous code has historically been a difficult thing to do.
Testing concurrent multi-threaded code proves to be even harder.
Often the engineer testing the software had the option of a run-and-hope style of non-deterministic testing.
Maybe they would leave a note on the tests, so that *when* they would occasionally fail, then we would know to just run-and-hope again.
Maybe they would test around the concurrent part of the code base by just testing the synchronous units that composed together under the concurrency model - i.e. the concurrent nature of the system was just untested.

The real problem with multi-threaded testing is that you are at the mercy of the Virtual Machine, the Operating System and the CPU Architecture.
It is virtually impossible to guarantee a consistent test bed for all of these layers, and even more unrealistic to expect that each of these layers will schedule the threads in the same way for each repeated test run.

### Testing with concurrency and wait handles.
It can be interesting to see the attempts some have made to test concurrent code.
Littering tests with new Threads, spin-waits, sleeps and other synchronization constructs eventually dilutes the meaning of the test - making it less **descriptive**.
Worse still, you may find that the side effects of these extra synchronization actually can have unpredictable adverse effects on your tests - making them less **predictable** and there less **repeatable**.
The other concern of adding locks, waits and sleeps to your tests is that they are no longer **fast**.

So how can Rx help us?

When initially using Rx, it may feel natural to write code like this sample.

```csharp
//Note that this test actually takes >2seconds to run
[Test, Timeout(2500)]
public void Testing_Timer_the_slow_way()
{
    var source = Observable.Timer(TimeSpan.FromSeconds(2));

    var actualValues = new List<long>();
    var hasCompleted = false;

    source.Subscribe(actualValues.Add, () => hasCompleted = true);

    Thread.Sleep(2100);
    CollectionAssert.AreEqual(new[]{0}, actualValues);
    Assert.That(hasCompleted);
}
```

In the sample above, we are just testing the implementation of the `Observable.Timer`.
Note that we have a list to capture values as they arrive and a flag to check if the sequence completed.
Next we have a `Thread.Sleep` that is slightly longer than the 2 seconds we expect it to run just to account for any OS or VM level thread scheduling.
Finally we have a safety net of a test time of 2.5 seconds just in case things over ran.

We have several problems here:

1. The test takes over 2 seconds to run. Tests should take milliseconds, not seconds.
2. We need to have multiple structures; the list and the flag.
3. While we verify we received the value, we don't actually verify that the value was received when we wanted it. `Observable.Timer(TimeSpan.FromSeconds(1))` would satisfy this test too.
4. We need to remember to use a feature like NUnit's `TimeoutAttribute` feature to know when the test fails.

### Testing with blocking operators
Other similarly problematic ways that people may try to test Rx code is with the blocking operators.
Operators like `First()` and `Single()` may seem useful (even though they are marked as `Obsolete`) when testing.
I often see a style of test such as below

```csharp
[Test, Timeout(2500)]
public void Testing_Timer_the_slow_way2()
{
    var source = Observable.Timer(TimeSpan.FromSeconds(2));

    Assert.AreEqual(0, source.Single());
}
```

In this sample we do have a lot less code than above.
However, we test still have similar problems
 * The test is slowly
 * The test doesn't validate the timeliness of the notifications
 * The `Timeout` attribute is still required to check for missing `OnCompleted` notifications.


## Rx to the rescue?
Rx can actually help us write testable asynchronous and concurrent code.
Due to some key properties of the Rx protocol we can achieve our goal of have fast, descriptive, predictable & repeatable tests.

_Observable sequences in Rx are serialized_.
This allows us to capture notifications in an ordered manner.
This makes verification easy.

_Concurrency is introduced via Schedulers_.
This allows us to introduce our own version of the Scheduler at test time.

## Test Schedulers
As each of our schedulers implement an interface (or a common protocol) we are able to substitute it at test time.
Out the box, Rx provides the testing libraries to do this for you.
The key type is the `TestScheduler`.

### Virtual time
The `TestScheduler` takes the concept of time and makes it virtual.
Running code in production on the real schedulers can be compared to watching a film standard speed.
But just like the way media players allow you to play videos at 1.5x or 2x speed, test schedulers allow to you do the same with your code.

This has the benefit of allowing tests to run synchronously and allows the compression of time.
This allows testing of long running queries to be consistently be run in milliseconds almost regardless of how long it would take to run in wall-clock time.



### Pre-canned observable sequences
When testing queries it can be useful to create pre-canned input sequences not only with specific notification types and value, but at specific times.

For example, if you wanted a sequence with that produced a value of "banana" at 3 seconds and then threw a timeout exception after another 10 seconds you could construct something like this:

```csharp
Observable.Timer(3.Seconds())
	.Select(_=> return "banana")
	.Concat(Observable.Never<string>())
	.Timeout(10.Seconds());
```

While this will work, we have lost some expressiveness to detailing how we construct the input sequence instead of what the sequence should be.

With `TestScheduler`s you can create either a Hot or a Cold sequence and just specify the types and values for each notification and the time they should occur.

```csharp
var scheduler = new TestScheduler();
var source = scheduler.CreateColdObservable(
                  ReactiveTest.OnNext(TimeSpan.FromSeconds(3).Ticks, "banana"),
                  ReactiveTest.OnError<long>(TimeSpan.FromSeconds(13).Ticks, new TimeoutException())
              );
```

Here we get to specify just the _what_ not the _how_.


### Test Observers
In our sample where we used a list to capture values and a flag to capture completion we had several issues.
First was that we had two structures for capturing state, and a third if we wanted to check for errors.
Second was that we can't validate the timings of our data.
We could solve the later by introducing the `TimeStamp` operator to our queries, but sometime this is difficult.
The preferred way to verify the behaviour of an observable sequence is to use an `ITestableObserver<T>` instance.

In addition to being able to create pre-canned sequences, the `TestScheudler` can be used as a factory to create an instance of an `ITestableObserver<T>`.

Here we create a observer and use it to validate the notifications and their value with timing information.

```csharp
//Note that this test takes ~1ms to execute
[Test]
public void Testing_Timer()
{
    var scheduler = new TestScheduler();
    var observer = scheduler.CreateObserver<long>();
    var source = Observable.Timer(TimeSpan.FromSeconds(2), scheduler);

    source.Subscribe(observer);
    scheduler.Start();

    ReactiveAssert.AssertEqual(observer.Messages,
        ReactiveTest.OnNext<long>(TimeSpan.FromSeconds(2).Ticks, 0L),
        ReactiveTest.OnCompleted<long>(TimeSpan.FromSeconds(2).Ticks));
}
```

Note that this test will correctly test what our previous examples were attempting to test.
But note the differences
 * Runs almost instantly ~1ms
 * Validates the payload of the notifications i.e. `OnNext(0L)`
 * Validates the timeliness of the notifications i.e. that the `OnNext` occurred at 2s immediately followed by the `OnCompleted`.

## Helper methods
The `TestScheduler` and its companion types are a great help in being able to test Rx effectively.
However, it can be a bit verbose sometimes.
And the API maybe isn't as discoverable as it could be.
The helper classes `ReactiveTest` and `ReactiveAssert` are helpful, but are often overlooked in how they can be used.

First lets look at the `CreateColdObservable` and `CreateHotObservable` factory methods.
They both take a `params` array of `Recorded<Notification<T>>`.
So it is natural to see code where these types are manually being instantiated like this

```csharp
testScheduler.CreateColdObservable<int>(
		new Recorded<Notification<int>>(10000000, Notification.CreateOnNext(1)),
		new Recorded<Notification<int>>(100000000, Notification.CreateOnCompleted<int>()))
```

This is very verbose and provides a low signal-to-nose ratio.
Instead we can use the often overloaded factory methods from `ReactiveTest`.

```csharp
testScheduler.CreateColdObservable<int>(
		ReactiveTest.OnNext<int>>(10000000, 1),
		ReactiveTest.OnCompleted<int>>(100000000))
```

This is much better.
However we still can improve this.
If you pay attention to the type definition you can see that while `ReactiveTest` exposes those static factory methods `OnNext`, `OnError` and `OnCompleted`, that the class is not defined as static.
This means you can further reduce the noise in the above example if the class definition the test inherited from `ReactiveTest`.

```csharp
testScheduler.CreateColdObservable<int>(
		OnNext<int>>(10000000, 1),
		OnCompleted<int>>(100000000))
```

Finally, there are some magic numbers of `10000000` and `10000000`.
Are they different numbers you ask.
Hard tell with that many zeros.
These two values represent the time measured in ticks that the notification should be produced; 1seconds and 10seconds respectively.
A more expressive way to write that is normally with the `TimeSpan` factory methods.

```csharp
testScheduler.CreateColdObservable<int>(
		OnNext<int>>(TimeSpan.FromSeconds(1).Ticks, 1),
		OnCompleted<int>>(TimeSpan.FromSeconds(10).Ticks))
```

This is better, but having written a lot of Rx unit test I prefer a further optimization.
I create some simple extension methods that allow me to rewrite the code so it reads even more naturally.

```csharp
testScheduler.CreateColdObservable<int>(
		OnNext<int>>(1.Seconds(), 1),
		OnCompleted<int>>(10.Seconds()))
```

I think this is a vast improvement from where we started.

One final tip is to consider using the `ReactiveAssert` type and its helper methods.
In our `TimeTest` above we used it to assert our observed notifications.
However there is an improvement that can be made here too
Instead of the following :

```csharp
    ReactiveAssert.AssertEqual(observer.Messages,
        ReactiveTest.OnNext<long>(TimeSpan.FromSeconds(2).Ticks, 0L),
        ReactiveTest.OnCompleted<long>(TimeSpan.FromSeconds(2).Ticks));
```

we can use the `AssertEqual` as an extension methods.
Then applying our other improvements we can end up with.

```csharp
    observer.Messages.AssertEqual(
        OnNext<long>(2.Seconds(), 0L),
        OnCompleted<long>(2.Seconds()));
```

#Final implementation

The full [LinqPad](http://www.linqpad.net) sample in available as [Testing.linq](Testing.linq)

The final implementation is below.

```csharp
void Main()
{
	Testing_Timer_the_slow_way();
	Testing_Timer_the_slow_way2();
	Testing_Timer();
	new MyTests().Testing_Timer();
}

// Define other methods and classes here
public void Testing_Timer_the_slow_way()
{
	var source = Observable.Timer(TimeSpan.FromSeconds(2));

	var actualValues = new List<long>();
	var hasCompleted = false;

	source.Subscribe(actualValues.Add, () => hasCompleted = true);

	Thread.Sleep(2100);
	CollectionAssert.AreEqual(new[] { 0L }, actualValues);
	Assert.That(hasCompleted);
}
public void Testing_Timer_the_slow_way2()
{
	var source = Observable.Timer(TimeSpan.FromSeconds(2));

	Assert.AreEqual(0, source.Single());
}
public void Testing_Timer()
{
	var scheduler = new TestScheduler();
	var observer = scheduler.CreateObserver<long>();
	var source = Observable.Timer(TimeSpan.FromSeconds(2), scheduler);

	source.Subscribe(observer);
	scheduler.Start();

	ReactiveAssert.AssertEqual(observer.Messages,
		ReactiveTest.OnNext<long>(TimeSpan.FromSeconds(2).Ticks, 0L),
		ReactiveTest.OnCompleted<long>(TimeSpan.FromSeconds(2).Ticks));
}

public class MyTests : ReactiveTest
{
	public void Testing_Timer()
	{
		var scheduler = new TestScheduler();
		var observer = scheduler.CreateObserver<long>();
		var source = Observable.Timer(TimeSpan.FromSeconds(2), scheduler);

		source.Subscribe(observer);
		scheduler.Start();

		observer.Messages.AssertEqual(
			OnNext<long>(2.Seconds(), 0L),
			OnCompleted<long>(2.Seconds()));
	}
}

public static class TemoralExtensions
{
	public static long Seconds(this int seconds)
	{
		return TimeSpan.FromSeconds(seconds).Ticks;
	}
}
```


See also:
 * http://www.introtorx.com/content/v1.0.10621.0/16_TestingRx.html

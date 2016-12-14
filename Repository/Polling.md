# Polling with Rx

Often if a system is not already inherently Reactive, we may resort to polling to give the illusion of a reactive interface.
This can happen at many layers.
You may need to poll a data store (database/file system) if it cannot emit change notifications.
You may need to poll an HTTP endpoint if a WebSocket endpoint is not exposed.

While embracing a Reactive first platform is preferred as it should be less resource intensive, polling can be a necessary intermediate step to full adoption of a Reactive System (https://youtu.be/np2aIaojc10).

# Constant cadence polling
While it may seem somewhat obvious to experienced Rx practitioners how you may go about polling with Rx, there are still a few curve balls depending on your exact requirements.
So, lets specify what our goals will be.

I think that most use cases of polling want the following semantics:
 * Constant cadence/frequency of polling
 * Only start re-polling once the previous request has completed. This is important if you don't want to have over lapping requests.
 * Log errors, but do not allow this to stop polling.



## Creating our tests
First, I want to have a test that ensures that for a given function that I can call it after the specified period.
It is also worth considering out API.
I say that I want to execute a function, but I do know that this function could take some time (probably due to some IO).
This leads me to think that I should actually be thinking about a `Task<T>` instead of a `Func<T>`.
However, this now leads me to question why would I mix TPL and Rx.
I have had poor experience of this before.
Thus, I think my API will be an Extension method over an `IObservable<T>`.

The extension method will need to take the source `IObservable<T>`, a `TimeSpan` to specify the period to poll, and an `IScheduler` to control time.

```
public static IObservable<T> Poll<T>(this IObservable<T> request, TimeSpan period, IScheduler scheduler)
{
   ...
}
```

Now to our tests.

### Issues a request each time period

```
public void IssuesRequestEachTimePeriod()
{
	var period = TimeSpan.FromSeconds(10);
	var testScheduler = new TestScheduler();
	var request = Observable.Defer(()=>Observable.Return(testScheduler.Now.Ticks));

	var observer = testScheduler.CreateObserver<long>();
	request.Poll(period, testScheduler)
		.Subscribe(observer);

	testScheduler.AdvanceTo(30.Seconds());

	ReactiveAssert.AssertEqual(observer.Messages,
		ReactiveTest.OnNext(10.Seconds(), 10.Seconds()),
		ReactiveTest.OnNext(20.Seconds(), 20.Seconds()),
		ReactiveTest.OnNext(30.Seconds(), 30.Seconds())
	);
}
```

A fairly simple implementation using `Observable.Interval` and `SelectMany` works well here.

```
public static IObservable<T> Poll<T>(this IObservable<T> request, TimeSpan period, IScheduler scheduler)
{
    return Observable.Interval(period, scheduler)
      .SelectMany(_ => request);
}
```

### Waits specified period after response before requesting again
In most implementations that I have worked with we don't want to poll every x seconds.
Instead they aim to re-poll x seconds after the previous response was received.
The difference is subtle but is becomes obvious when you realize that requests generally cant guarantee to be instant (https://en.wikipedia.org/wiki/Fallacies_of_distributed_computing).

For example, if we poll every 10seconds and a response takes 1s to return, should we poll 9seconds after the response or 10seconds after the response?
To further extend the example, imagine the response took 8seconds to respond.
Should we poll again in only 2seconds?
Finally, to extend the example to its obvious conclusion, what happens when the response takes 11seconds?
Should we have two request in flight at the same time?
What if the second response arrives before the first?

With this in mind, I prefer to opt for the simpler tactic of only starting to re-poll after previous request are complete.


This test shows a request that is taking 5seconds to respond where we have a polling interval of 10seconds.
This means we should receive responses at times 15s and 30s.

```
public void IssuesRequestEachTimePeriodAfterPreviousProcessingIsComplete()
{
	var testScheduler = new TestScheduler();
	var period = TimeSpan.FromSeconds(10);

	var request = Observable.Timer(TimeSpan.FromSeconds(5), testScheduler);

	var observer = testScheduler.CreateObserver<long>();
	request.Poll(period, testScheduler)
		.Subscribe(observer);

	testScheduler.AdvanceTo(30.Seconds());

	ReactiveAssert.AssertEqual(observer.Messages,
		ReactiveTest.OnNext(15.Seconds(), 0L),
		ReactiveTest.OnNext(30.Seconds(), 0L)
	);
}
```

Now we will need to update our implementation to get this test to pass.
Here we can make effective use of `Observable.Timer` combined with `Repeat`.

```
public static IObservable<T> Poll<T>(this IObservable<T> request, TimeSpan period, IScheduler scheduler)
{
    return Observable.Timer(period, scheduler)
      .SelectMany(_ => request)
      .Repeat();  //Loop on success
}
```

### Fault tolerant
Requests can fail.
We should be resilient to these failures, especially if we plan on polling a system.
It is much more likely for a polling system to experience downstream failures than system that just issues single requests occasionally.

In this test, I return successfully, then fail, then return successfully.
I expect (in this implementation) that the failure is swallowed and the polling continues.

```
public void ErrorsDontStopThePolling()
{
	var testScheduler = new TestScheduler();
	var period = TimeSpan.FromSeconds(10);

	var request = Observable.Create<string>(obs => {
		if(testScheduler.Now.Ticks < TimeSpan.FromSeconds(15).Ticks)
			return Observable.Return("Hello").Subscribe(obs);
		else if(testScheduler.Now.Ticks < TimeSpan.FromSeconds(25).Ticks)
			return Observable.Throw<String>(new Exception("boom")).Subscribe(obs);
		return Observable.Return("Back again").Subscribe(obs);
	});

	var observer = testScheduler.CreateObserver<string>();
	request.Poll(period, testScheduler)
		.Subscribe(observer);

	testScheduler.AdvanceTo(30.Seconds());

	ReactiveAssert.AssertEqual(observer.Messages,
		ReactiveTest.OnNext(10.Seconds(), "Hello"),
		ReactiveTest.OnNext(30.Seconds(), "Back again")
	);
}
```

Now we only need to simply add a `Retry()` operator here.

```
public static IObservable<T> Poll<T>(this IObservable<T> request, TimeSpan period, IScheduler scheduler)
{
    return Observable.Timer(period, scheduler)
      .SelectMany(_ => request)
      .Retry()    //Loop on errors
      .Repeat();  //Loop on success
}
```

### Exposing Errors
I am not so happy about the idea of just swallowing errors and not exposing them to the consumer.
They may care about them or they may not, but hiding the failure seems to be a bad design.

Now we cannot `OnError` the exceptions because as per the Rx contracts, this would mean the sequence would have to end.
An alternative would be to update our extension method to return an `IObservable<IObservable<T>>` but I think that makes the API more difficult to consume for users that are not sophistacated Rx eusers.

Instead I am going to change the API to expose an `IObservable<Try<T>>`.
The new `Try<T>` type (similar to the Maybe/Either/Option types) allows me to return either a value, or and error.
As we will not be throwing the exception or `OnError`ing it, the pipeline will still be intact.

The `Try<T>` type definition is effectively

```
public class Try<T>
{
	public TResult Switch<TResult>(Func<T, TResult> caseValue, Func<Exception, TResult> caseError);
	public void Switch(Action<T> caseValue, Action<Exception> caseError);
}
```

Which means our updated `Poll` signature is now
```
public static IObservable<Try<T>> Poll<T>(this IObservable<T> request, TimeSpan period, IScheduler scheduler)
{
  ...
}
```

We update our previous tests to use the `Try<T>.Switch` method, using a dummy value of `-1` for exceptions.

```
request.Poll(period, testScheduler)
		.Select(i=>i.Switch(v=>v, ex=>-1))
		.Subscribe(observer);
```

Our new test will now have a request that will return successfully on the first and 3rd calls, but fail for the 2nd call.

```
public void ErrorsDontStopThePolling()
{
	var testScheduler = new TestScheduler();
	var period = TimeSpan.FromSeconds(10);

	var callCount = 0;
	var request = Observable.Create<string>(obs => {
		callCount++;
		if(callCount==1)
			return Observable.Return("Hello").Subscribe(obs);
		else if(callCount==2)
			return Observable.Throw<String>(new Exception("boom")).Subscribe(obs);
		return Observable.Return("Back again").Subscribe(obs);
	});

	var observer = testScheduler.CreateObserver<Try<string>>();
	request.Poll(period, testScheduler)
		.Subscribe(observer);

	testScheduler.AdvanceTo(30.Seconds());

	ReactiveAssert.AssertEqual(observer.Messages,
		ReactiveTest.OnNext(10.Seconds(), Try<string>.Create("Hello")),
		ReactiveTest.OnNext(20.Seconds(), Try<string>.Fail(new Exception("boom"))),
		ReactiveTest.OnNext(30.Seconds(), Try<string>.Create("Back again"))
	);
}
```


We now need to update our implementation to both match the new signature and satisfy the tests.
Values will be exposed from a `Select` operator via the new `Try<T>.Create` factory method.
Exceptions will be caught by the `Catch` operator and converted to a `Try<T>` with the `Try<T>.Fail` factory method.

```
public static IObservable<Try<T>> Poll<T>(this IObservable<T> request, TimeSpan period, IScheduler scheduler)
{
return Observable.Timer(period, scheduler)
      .SelectMany(_ => request)
      .Select(response=>Try<T>.Create(response))
      .Catch<Try<T>, Exception>(ex=>Observable.Return(Try<T>.Fail(ex)))
      .Repeat();  //Loop
}
```


### Silence as an errors
If our request takes too long to respond, then we don't want to surrender to waiting indefinitely.
In the spirit of the Fallacies of Distributed Computing, we don't want our system to be at the mercy of the dependency sub-system.
There are numerous reasons that a request could time out ranging from local system resource exhaustion, to network failures, the service being under load or perhaps being patched.
Regardless of the reasons, our polling operation should be resilient to these extended periods of silence.

In this case, we will not have to alter our API.
Instead it is a concern of the provided `IObservable<T>`.
Consumers of our API should apply the `Timeout` operator to the provided sequence before applying the `Poll` operator. For example:

```
 mySource
     .Timeout(TimeSpan.FromSeconds(5), scheduler)
     .Poll(TimeSpan.FromSeconds(30), scheduler)
```


#Final implementation

The full [LinqPad](http://www.linqpad.net) sample in available as [Polling.linq](Polling.linq)

The final implementation is below.

```
public static class ObservableExtensions
{
	/// <summary>
	/// Periodically repeats the observable sequence exposing a responses or failures.
	/// </summary>
	/// <typeparam name="T">The type of the sequence response values.</typeparam>
	/// <param name="source">The source observable sequence to re-subscribe to after each <paramref name="period"/>.</param>
	/// <param name="period">The period of time to wait before subscribing to the <paramref name="source"/> sequence. Subsequent subscriptions will occur this period after the previous sequence completes.</param>
	/// <param name="scheduler">The <see cref="IScheduler"/> to use to schedule the polling.</param>
	/// <returns>Returns an infinite observable sequence of values or errors.</returns>
	public static IObservable<Try<T>> Poll<T>(this IObservable<T> source, TimeSpan period, IScheduler scheduler)
	{
		return Observable.Timer(period, scheduler)
					.SelectMany(_ => source)    //Flatten the response sequence.
					.Select(Try<T>.Create)      //Project successful values to the Try<T> return type.
					.Catch<Try<T>, Exception>(ex => Observable.Return(Try<T>.Fail(ex))) //Project exceptions to the Try<T> return type
					.Repeat();  //Loop
	}
}
public abstract class Try<T>
    {
        private Try()
        {
        }

        public static Try<T> Create(T value)
        {
            return new Success(value);
        }

        public static Try<T> Fail(Exception value)
        {
            return new Error(value);
        }

        public abstract TResult Switch<TResult>(Func<T, TResult> caseValue, Func<Exception, TResult> caseError);
        public abstract void Switch(Action<T> caseValue, Action<Exception> caseError);

        private sealed class Success : Try<T>, IEquatable<Success>
        {
            private readonly T _value;

            public Success(T value)
            {
                _value = value;
            }

            public override TResult Switch<TResult>(Func<T, TResult> caseValue, Func<Exception, TResult> caseError)
            {
                return caseValue(_value);
            }

            public override void Switch(Action<T> caseValue, Action<Exception> caseError)
            {
                caseValue(_value);
            }

            public bool Equals(Success other)
            {
                if (ReferenceEquals(other, this))
                    return true;
                if (other == null)
                    return false;
                return EqualityComparer<T>.Default.Equals(_value, other._value);
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as Success);
            }

            public override int GetHashCode()
            {
                return EqualityComparer<T>.Default.GetHashCode(_value);
            }

            public override string ToString()
            {
                return string.Format(CultureInfo.CurrentCulture, "Success({0})", _value);
            }
        }

        private sealed class Error : Try<T>, IEquatable<Error>
        {
            private readonly Exception _exception;

            public Error(Exception exception)
            {
                if (exception == null)
                    throw new ArgumentNullException(nameof(exception));
                _exception = exception;
            }

            public override TResult Switch<TResult>(Func<T, TResult> caseValue, Func<Exception, TResult> caseError)
            {
                return caseError(_exception);
            }

            public override void Switch(Action<T> caseValue, Action<Exception> caseError)
            {
                caseError(_exception);
            }

            public bool Equals(Error other)
		{
			if (ReferenceEquals(other, this))
				return true;
			if (other == null)
				return false;
			return Equals(_exception, other._exception);
		}
		private static bool Equals(Exception a, Exception b)
		{
			if (a == null && b == null)
				return true;
			if (a == null || b == null)
				return false;
			if (a.GetType() != b.GetType())
				return false;
			if (!string.Equals(a.Message, b.Message))
				return false;
			return Equals(a.InnerException, b.InnerException);
		}

		public override bool Equals(object obj)
		{
			return Equals(obj as Error);
		}

		public override int GetHashCode()
		{
			return EqualityComparer<Exception>.Default.GetHashCode(_exception);
		}

		public override string ToString()
		{
			return string.Format(CultureInfo.CurrentCulture, "Error({0})", _exception);
		}
	}
}
```

See also:
 * http://www.enterpriseintegrationpatterns.com/patterns/conversation/Polling.html
 * http://www.enterpriseintegrationpatterns.com/patterns/messaging/PollingConsumer.html

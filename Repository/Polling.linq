<Query Kind="Program">
  <NuGetReference>Microsoft.Reactive.Testing</NuGetReference>
  <Namespace>Microsoft.Reactive.Testing</Namespace>
  <Namespace>System</Namespace>
  <Namespace>System.Reactive</Namespace>
  <Namespace>System.Reactive.Concurrency</Namespace>
  <Namespace>System.Reactive.Disposables</Namespace>
  <Namespace>System.Reactive.Joins</Namespace>
  <Namespace>System.Reactive.Linq</Namespace>
  <Namespace>System.Reactive.PlatformServices</Namespace>
  <Namespace>System.Reactive.Subjects</Namespace>
  <Namespace>System.Reactive.Threading.Tasks</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>System.Globalization</Namespace>
</Query>

void Main()
{
	IssuesRequestEachTimePeriod();
	IssuesRequestEachTimePeriodAfterPreviousProcessingIsComplete();
	ErrorsDontStopThePolling();
	ExampleWithTimeout();
}

// Define other methods and classes here
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

#region Tests

public void IssuesRequestEachTimePeriod()
{
	var period = TimeSpan.FromSeconds(10);
	var testScheduler = new TestScheduler();
	var request = Observable.Defer(() => Observable.Return(testScheduler.Now.Ticks));

	var observer = testScheduler.CreateObserver<long>();
	request.Poll(period, testScheduler)
		.Select(i => i.Switch(v => v, ex => -1))
		.Subscribe(observer);

	testScheduler.AdvanceTo(30.Seconds());

	ReactiveAssert.AssertEqual(observer.Messages,
		ReactiveTest.OnNext(10.Seconds(), 10.Seconds()),
		ReactiveTest.OnNext(20.Seconds(), 20.Seconds()),
		ReactiveTest.OnNext(30.Seconds(), 30.Seconds())
	);
}

public void IssuesRequestEachTimePeriodAfterPreviousProcessingIsComplete()
{
	var testScheduler = new TestScheduler();
	var period = TimeSpan.FromSeconds(10);

	var request = Observable.Timer(TimeSpan.FromSeconds(5), testScheduler);

	var observer = testScheduler.CreateObserver<long>();
	request.Poll(period, testScheduler)
		.Select(i => i.Switch(v => v, ex => -1))
		.Subscribe(observer);

	testScheduler.AdvanceTo(30.Seconds());

	ReactiveAssert.AssertEqual(observer.Messages,
		ReactiveTest.OnNext(15.Seconds(), 0L),
		ReactiveTest.OnNext(30.Seconds(), 0L)
	);
}

public void ErrorsDontStopThePolling()
{
	var testScheduler = new TestScheduler();
	var period = TimeSpan.FromSeconds(10);

	var callCount = 0;
	var request = Observable.Create<string>(obs =>
	{
		callCount++;
		if (callCount == 1)
			return Observable.Return("Hello").Subscribe(obs);
		else if (callCount == 2)
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

public void ExampleWithTimeout()
{
	var testScheduler = new TestScheduler();
	var period = TimeSpan.FromSeconds(10);

	var callCount = 0;
	var request = Observable.Create<string>(obs =>
	{
		callCount++;
		if (callCount == 2)
			return Observable.Never<string>().Subscribe(obs);
		return Observable.Return("response").Subscribe(obs);
	});

	var observer = testScheduler.CreateObserver<Try<string>>();
	request
		.Timeout(TimeSpan.FromSeconds(5), testScheduler)
		.Poll(period, testScheduler)
		.Subscribe(observer);

	testScheduler.AdvanceTo(40.Seconds());

	ReactiveAssert.AssertEqual(observer.Messages,
		ReactiveTest.OnNext(10.Seconds(), Try<string>.Create("response")),
		ReactiveTest.OnNext(25.Seconds(), Try<string>.Fail(new TimeoutException("The operation has timed out."))),
		ReactiveTest.OnNext(35.Seconds(), Try<string>.Create("response"))
	);
}

#endregion

#region Test helpers
public static class TemporalExtensions
{
	public static long Seconds(this int seconds)
	{
		return TimeSpan.FromSeconds(seconds).Ticks;
	}
}
#endregion
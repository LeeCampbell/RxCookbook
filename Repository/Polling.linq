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
</Query>

void Main()
{
	IssuesRequestEachTimePeriod();
	IssuesRequestEachTimePeriodAfterPreviousProcessingIsComplete();
	ErrorsDontStopThePolling();
}

// Define other methods and classes here
public static class ObservableExtensions
{
    public static IObservable<T> Poll<T>(this IObservable<T> request, TimeSpan period, IScheduler scheduler)
	{
		return Observable.Timer(period, scheduler)
					.SelectMany(_ => request)
					.Timeout(TimeSpan.FromTicks(period.Ticks*3), scheduler)
					.Retry()    //Loop on errors
					.Repeat();  //Loop on success
	}
}



#region Tests

public void IssuesRequestEachTimePeriod()
{
	var period = TimeSpan.FromSeconds(10);
	var testScheduler = new TestScheduler();
	var responder = Observable.Defer(()=>Observable.Return(testScheduler.Now.Ticks));
	
	var observer = testScheduler.CreateObserver<long>();
	responder.Poll(period, testScheduler)
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
	
	var responder = Observable.Timer(TimeSpan.FromSeconds(5), testScheduler);

	var observer = testScheduler.CreateObserver<long>();
	responder.Poll(period, testScheduler)
		.Subscribe(observer);

	testScheduler.AdvanceTo(30.Seconds());

	ReactiveAssert.AssertEqual(observer.Messages,
		ReactiveTest.OnNext(15.Seconds(), 0L),
		ReactiveTest.OnNext(30.Seconds(), 0.Seconds())
	);
}

public void ErrorsDontStopThePolling()
{
	var testScheduler = new TestScheduler();
	var period = TimeSpan.FromSeconds(10);

	var responder = Observable.Create<string>(obs => {
		if(testScheduler.Now.Ticks < TimeSpan.FromSeconds(15).Ticks)
			return Observable.Return("Hello").Subscribe(obs);
		else if(testScheduler.Now.Ticks < TimeSpan.FromSeconds(25).Ticks)
			return Observable.Throw<String>(new Exception("boom")).Subscribe(obs);
		return Observable.Return("Back again").Subscribe(obs);
	});

	var observer = testScheduler.CreateObserver<string>();
	responder.Poll(period, testScheduler)
		.Subscribe(observer);

	testScheduler.AdvanceTo(30.Seconds());

	ReactiveAssert.AssertEqual(observer.Messages,
		ReactiveTest.OnNext(10.Seconds(), "Hello"),
		ReactiveTest.OnNext(30.Seconds(), "Back again")
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
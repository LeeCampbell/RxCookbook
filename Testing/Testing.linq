<Query Kind="Program">
  <NuGetReference>Microsoft.Reactive.Testing</NuGetReference>
  <NuGetReference>NUnit</NuGetReference>
  <NuGetReference>System.Reactive</NuGetReference>
  <Namespace>Microsoft.Reactive.Testing</Namespace>
  <Namespace>NUnit</Namespace>
  <Namespace>NUnit.Framework</Namespace>
  <Namespace>System</Namespace>
  <Namespace>System.Collections.Concurrent</Namespace>
  <Namespace>System.Collections.ObjectModel</Namespace>
  <Namespace>System.ComponentModel</Namespace>
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

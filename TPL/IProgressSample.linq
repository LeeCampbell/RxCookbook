<Query Kind="Program">
  <NuGetReference>Newtonsoft.Json</NuGetReference>
  <NuGetReference>Rx-Main</NuGetReference>
  <Namespace>System</Namespace>
  <Namespace>System.Collections.Concurrent</Namespace>
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
	Solve(new Progress<int>(i=>Console.Write(".")));
	Console.WriteLine("Done");
	
	ObservableProgress.Create<int>(Solve)
		.Subscribe(
			i => Console.Write("."),
			() => Console.WriteLine("Done"));


	var veryLargeFile = @"C:\Users\Lee\Videos\RobertWalters201609_ReactiveSystem\ReactiveSystems_Perth_2016_RobertWalters.mp4";
	ObservableProgress.CreateAsync<double>(progressReporter=>ReadFile(veryLargeFile, progressReporter))
		.Sample(TimeSpan.FromMilliseconds(250))
		.Select(i=>i.ToString("p2"))
		.Subscribe(
			p=>Console.WriteLine(p),
			()=>Console.WriteLine("Done"));
}

// Define other methods and classes here
private void Solve(IProgress<int> progress)
{
	for (int i = 0; i < 100; i++)
	{
		Thread.Sleep(10);
		progress.Report(i);
	}
}

public static async Task ReadFile(string url, IProgress<double> progressReporter)
{
	double totalBytes = new FileInfo(url).Length;
	var bufferSize = 1024 * 4; //4k;
	var buffer = new byte[bufferSize];
	var offset = 0;
	var bytesRead = 0;
	var totalBytesRead = 0L;

	using (var fs = File.OpenRead(url))
	{
		do
		{
			bytesRead = await fs.ReadAsync(buffer, offset, bufferSize);
			//Do something here with the data that was just read.
			totalBytesRead += bytesRead;
			var fractionDone = totalBytesRead / totalBytes;
			progressReporter.Report(fractionDone);
		} while (bytesRead > 0);
	}
}

public static class ObservableProgress
{
	public static IObservable<T> Create<T>(this Action<IProgress<T>> action)
	{
		return Observable.Create<T>(obs =>
		{
			action(new Progress<T>(obs.OnNext));
			obs.OnCompleted();
			//No apparent cancellation support.
			return Disposable.Empty;
		});
	}

	public static IObservable<T> CreateAsync<T>(Func<IProgress<T>, Task> action)
	{
		return Observable.Create<T>(async obs =>
		{
			await action(new Progress<T>(obs.OnNext));
			obs.OnCompleted();
			//No apparent cancellation support. Add an overload that accepts a CancellationToken instead
			return Disposable.Empty;
		});
	}
}
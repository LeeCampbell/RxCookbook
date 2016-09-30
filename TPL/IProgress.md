# IProgress integration

As of .NET 4.5 the `IProgress<T>` interface has allowed long running code to be able to _callback_ to consuming code to report progress.

The `IProgress<T>` is a simple interface with just a single method defined

```csharp
public interface IProgress<in T>
{
    Report<T>(T value);  
}
```

As Rx is a library built on callbacks, it seems natural to be able to get and `IObservable<T>` output from a library that implements the `IProgress<T>` pattern.

Here is a simple sample of a method using the IProgress pattern.

```csharp
private void Solve(IProgress<int> progress)
{
	for (int i = 0; i < 100; i++)
	{
		Thread.Sleep(10);
		progress.Report(i);
	}
}
```

To see how we might consumer this method, t is first useful to note that `Progress<T>` is a default implementation of the `IProgress<T>` interface.

So to get a trail of periods printed to our console while this is running we could call the `Solve` method as such:

```csharp
Solve(new Progress<int>(i=>Console.Write(".")));
Console.WriteLine("Done");
```

Which would output.

```
...................................................................................................Done
```

As we can see that that `Progress<T>` implementation is just taking a delegate, we can follow suit and use this class to help us transition to `IObservable<T>`.

We could simply wrap our `Solve` method with an `Observable.Create` as such to get us off the ground.

```csharp
Observable.Create<T>(obs =>
	{
		Solve(new Progress<T>(obs.OnNext));
		obs.OnCompleted();
		//No apparent cancellation support.
		return Disposable.Empty;
	})
  .Subscribe(
		i=>Console.Write("."),
		()=>Console.WriteLine("Done"));
```

Now that seems to work fine as it produces the same output.
However that might be a bit cumbersome to type everywhere.
This is easily turned into a factory method however.

```csharp
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
}
```

Which now means we can call it like this.

```csharp
ObservableProgress.Create<int>(Solve)
  .Subscribe(
    i => Console.Write("."),
    () => Console.WriteLine("Done"));
```

So far, in my opinion we just adding Rx for Rx sake.
It is not really paying its way yet.
To find out where Rx can really shine, lets look at a more useful sample of reading a very large file and reporting progress.

This simple block of code will take a file, stream into memory, and report progress.

```csharp
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
			totalBytesRead += bytesRead;
			var fractionDone = totalBytesRead / totalBytes;
			//Do something here with the data that was just read.
			progressReporter.Report(fractionDone);
		} while (bytesRead > 0);
	}
}
```

You can see here that it knows nothing about Rx.
It is however using `async`/`await` features.
The following code can offer us a nice bridge between the two styles of code (`async`/`await` and Rx).

As before we will wrap our `Task` with `Observable.Create`.
Here we jump straight to making it a factory method.

```csharp
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
```

We can consume our new factory method as such.

```csharp
ObservableProgress.CreateAsync<double>(reporter=>ReadFile(veryLargeFile, reporter))
  .Subscribe(
    p=>Console.WriteLine(p),
    ()=>Console.WriteLine("Done"));
```

Note that in my implementation of the `ReadFile` method I am reading in 4k blocks and reporting after each block.
For my ~7GB test file I was getting 1,736,643 callbacks in the 10s it took to process.
That is over 100,000 progress updates per second, which is just too much for me.
However this is where Rx shines.
Instead of taking all progress updates, why not just sample every quarter of a second?
And lets output the value as a percentage string.

This is a simple upgrade to our query.

```csharp
ObservableProgress.CreateAsync<double>(progressReporter=>ReadFile(veryLargeFile, progressReporter))
  .Sample(TimeSpan.FromMilliseconds(250))
  .Select(i=>i.ToString("p2"))
  .Subscribe(
    p=>Console.WriteLine(p),
    ()=>Console.WriteLine("Done"));
```

Ah much better, now I only see around 40-50 updates (4 per second).

```
2.05%
4.23%
6.38%
8.54%
10.70%
12.86%
...
94.96%
97.09%
99.06%
100.00%
Done
```

You obviously have the full power of Rx at your disposal now, including `ObserveOn` and `SubscribeOn` to ensure your long running processes are running on the correct thread (scheduler) and that progress is reported to you on the correct scheduler too.

The full [LinqPad](http://www.linqpad.net) sample is available as [IProgressSample.linq](IProgressSample.linq)

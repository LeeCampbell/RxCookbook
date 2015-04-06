<Query Kind="Program">
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
	var fr = new FileReader();
	fr.StreamLines(Util.CurrentQueryPath)
		.Dump("This file's contents");
}

// Define other methods and classes here
public class FileReader
{
	public IObservable<string> StreamLines_Wrong(string filePath)
	{
   		//This is not really 'Observable'. We have just created a pull (Enumerble) loop and masked it as IObservable
		//	We have also lost the ability to provide cancellation to the consumer.
		//	Here we would be better off having stayed with a simple `yeild return` pattern and returned an IEnumerable.
       	return Observable.Create<string>(o =>
		{
			using (var reader = new StreamReader(filePath))
			{
				while (!reader.EndOfStream)
				{
					try
					{
						var line = reader.ReadLine();
						o.OnNext(line);
					}
					catch (Exception e)
					{
						o.OnError(e);
					}
				}				
				o.OnCompleted();
			}
			return Disposable.Empty;
		});
	}
	
	public IObservable<string> StreamLines_AsyncButStillWrong(string filePath)
	{
   		//Here we leverage the async/await features of C#. However we are still just creating an enumerator.
		//	However we still have lost the ability to provide cancellation to the consumer.
		//	Still would be better off having stayed with a simple `yeild return` pattern returning an IEnumerable.
       	return Observable.Create<string>(async o =>
		{
			using (var reader = new StreamReader(filePath))
			{
				while (!reader.EndOfStream)
				{
					try
					{
						var line = await reader.ReadLineAsync();
						o.OnNext(line);
					}
					catch (Exception e)
					{
						o.OnError(e);
					}
				}				
				o.OnCompleted();
			}
			return Disposable.Empty;
		});
	}
	
	public IObservable<string> StreamLines_Scheduler(string filePath, IScheduler scheduler)
    {
   		//Here we use an the recursive scheduler technique.
		//	This does alter the metho signature as an IScheduler implementation needs to be provided now.
       	return Observable.Create<string>(o =>
		{
			var reader = new StreamReader(filePath);
			var cancelation = scheduler.Schedule(async self=>
			{
				if(!reader.EndOfStream)
				{
					try
					{
						var line = await reader.ReadLineAsync();
						o.OnNext(line);
						self();	//Recursively call back into this lambda.
					}
					catch (Exception e)
					{
						o.OnError(e);
					}
				}
				else
				{
					o.OnCompleted();
				}					
				
			});
			
			return new CompositeDisposable(cancelation, reader);
		});
	}
		
	public IObservable<string> StreamLines(string filePath)
    {
   		//Here we use an overload of Create that provides a CancellationTokenSource.
		//	This can be used to signal the subscription has been disposed.
		//	In this case it is more elegant than trying to return an IDisposable instance.
       	return Observable.Create<string>(async (o, cts) =>
		{
			using (var reader = new StreamReader(filePath))
			{
				while (!cts.IsCancellationRequested && !reader.EndOfStream)
				{
					try
					{
						var line = await reader.ReadLineAsync();
						o.OnNext(line);
					}
					catch (Exception e)
					{
						o.OnError(e);
					}
				}
				if (!cts.IsCancellationRequested && reader.EndOfStream)
				{
					o.OnCompleted();
				}
			}
		});
	}
	
}
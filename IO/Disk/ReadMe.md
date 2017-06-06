# Disk I/O with Rx
Reading and writing to the disk has for a long time on many platforms been faked to look like a synchronous activity.
As the disk is a separate system to the CPU, it can more naturally be thought of as a candidate for an asynchronous system.
Writes are really posts of data, and reads are really requests for data.

Modern platforms have embraced this concept with more popularity.
NodeJs for example performs all I/O asynchronously.
Recent versions of .NET have made a move towards asynchronous I/O to be the default way of interacting with network and disk subsystems.

In .NET this asynchronous approach is natively provided with the `Task` type and `async/await` keywords.
These are very effective, however only provide either a long pause while all data is sent or received, or only provide a chunk of data quickly.
Rx can provided a much needed bridge for streaming data to and from the disk.

## Using async/await chunking with Rx
A common road block when using Rx to perform I/O is the impedance mismatch between the pulling nature of reading from a disk and the push nature of Rx.
While you can use asynchronous read methods, these still need to be invoked for each batch of data, creating a pooling loop.

When a somewhat experienced Rx developer initially faces this issue, the go to Operator is often `Observable.Create`.
The issue here is that the overload the user will look to requires a `IDisposable` to be returned to enable consumers to cancel the subscription.

This leaves us with the question
> How do I return an `IDisposable` if I am in a loop?

Let's see the problem in code

```csharp
public IObservable<string> StreamLines_Wrong(string filePath)
{
    // This is not really 'Observable'. We have just created a pull (IEnumerable) loop and masked it as IObservable
    // We have also lost the ability to provide cancellation to the consumer.
    // Here we would be better off having stayed with a simple `yield return` pattern and returned an IEnumerable.
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
```

We can make a slight improvement by at least using the asynchronous methods from the `StreamReader`.

```csharp
public IObservable<string> StreamLines_AsyncButStillWrong(string filePath)
{
    // Here we leverage the async/await features of C#. However we are still just creating an enumerator.
    // However we still have lost the ability to provide cancellation to the consumer.
    // Still would be better off having stayed with a simple `yield return` pattern returning an IEnumerable.
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
```

A corrected approach might be to introduce a scheduler. This would allow us to schedule work and potentially invoke a recursive scheduling loop. This would also allow us to return the scheduled `IDisposable`.

```csharp
public IObservable<string> StreamLines_Scheduler(string filePath, IScheduler scheduler)
{
    // Here we use an the recursive scheduler technique.
    // This does alter the metho signature as an IScheduler implementation needs to be provided now.
    return Observable.Create<string>(o =>
    {
        var reader = new StreamReader(filePath);
        var cancelation = scheduler.Schedule(async self=>
        {
            if (!reader.EndOfStream)
            {
                try
                {
                    var line = await reader.ReadLineAsync();
                    o.OnNext(line);
                    self();    //Recursively call back into this lambda.
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
```

Finally we could consider a more simple approach just by using a different overload to `Observable.Create`.
If we adopt the overload that takes a `CancellationToken` as well as and `IObserver` we now don't have to return that `IDisposable` from our method.
Instead we can check the `IsCancellationRequested` property in our loop.

```csharp
public IObservable<string> StreamLines(string filePath)
{
    // Here we use an overload of Create that provides a CancellationTokenSource.
    // This can be used to signal the subscription has been disposed.
    // In this case it is more elegant than trying to return an IDisposable instance.
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
```

Full [LinqPad](http://www.linqpad.net) sample at [ReadFromDisk.linq](ReadFromDisk.linq).

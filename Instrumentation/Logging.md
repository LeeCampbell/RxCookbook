#Logging

Logging of observable sequences is made easy with the `Do` operator. 
However we can further generalise it create a fully featured extension method for logging.

Simple usage of the `Do` operator allows us to intercept `OnNext`, `OnError` and `OnCompleted` actions.

```csharp
ILogger log = new Logger();
var source = new Subject<int>();

source.Do(
	i=>log.Debug("OnNext({0})", i),
	ex=>log.Error("OnError({0})", ex),
	()=>log.Debug("OnCompleted()"))
.Subscribe();

source.OnNext(1);
//source.OnError(new InvalidOperationException("Some failure"));
source.OnCompleted();
```

Output

> **Debug**  
> OnNext(1) 

> **Debug**  
> OnCompleted() 

This show cases how simple it is to add some logging to an observable sequence, however it clearly could be generalised.
Writing all that code each time you want to log an observable sequence seems silly. 
Here we create an extension method that takes an `ILogger` (or what ever you logging interface is) parameter.

```csharp
public static IObservable<T> Log<T>(this IObservable<T> source, ILogger logger)
{
	return source.Do(
                   i => logger.Trace("OnNext({0})", i),
                   ex => logger.Trace("OnError({0})", ex),
                   () => logger.Trace("OnCompleted()"));
}
```

For a sample we could leave it as it is. 
However, in practice, this implementation proves to be quite useless.
If this logging was applied to multiple sequences concurrently, we would not be able to identify which values belonged to which sequences.
A simple fix to this is to provide a name for the sequence that is to be logged.

```csharp
public static IObservable<T> Log<T>(this IObservable<T> source, ILogger logger, string name)
{
  return source.Do(
                  i => logger.Trace("{0}.OnNext({1})", name, i),
                  ex => logger.Trace("{0}.OnError({1})", name, ex),
                  () => logger.Trace("{0}.OnCompleted()", name));
}
```


In additional to being able to identify which values belong to which sequence, we also will want to know when the subscription was made and when the subscription was disposed.
Often the subscription will be disposed once the sequence completes or errors.
But if we do not log the disposal, then we will not know if a sequence is still running and just not producing any values or if it has no remaining subscriptions.

This is easily achieved by wrapping our existing code in an `Observable.Create`. this gives us access to the point in time when the subscription was made and also disposed.

```csharp
public static IObservable<T> Log<T>(this IObservable<T> source, ILogger logger, string name)
{
  return Observable.Create<T>(
          o =>
          {
              logger.Trace("{0}.Subscribe()", name);
              var subscription = source
                  .Do(
                      i => logger.Trace("{0}.OnNext({1})", name, i),
                      ex => logger.Trace("{0}.OnError({1})", name, ex),
                      () => logger.Trace("{0}.OnCompleted()", name))
                  .Subscribe(o);
              var disposal = Disposable.Create(() => logger.Trace("{0}.Dispose()", name));
              return new CompositeDisposable(subscription, disposal);
          });
}
```

A final thing you may wish to log is the period the sequence was subscribed for.
To do this I use another logging extension method.

```csharp
public static IDisposable Time(this ILogger logger, string name)
{
   return new Timer(logger, name);
}

private sealed class Timer : IDisposable
{
   private readonly ILogger _logger;
   private readonly string _name;
   private readonly Stopwatch _stopwatch;

   public Timer(ILogger logger, string name)
   {
       _logger = logger;
       _name = name;
       _stopwatch = Stopwatch.StartNew();
   }

   public void Dispose()
   {
       _stopwatch.Stop();
       _logger.Debug("{0} took {1}", _name, _stopwatch.Elapsed);
   }
}
```

This method effectively allows you to get a handle to an `IDisposable` instance that will log the time it was alive for.

```csharp
using(log.Time("TimerSample"))
{
	Thread.SpinWait(1000);
}
```

Output
 
> **Debug**  
> TimerSample took 00:00:00.0008142 

Now putting this all together we can see the full lifetime of a sequence logged.

```csharp
ILogger log = new Logger();
var source = new Subject<int>();

source.Log(log, "Sample")
	  .Subscribe();

source.OnNext(1);
//source.OnError(new InvalidOperationException("Some failure"));
source.OnCompleted();
```

Output:

> **Trace**  
> Sample.Subscribe() 

> **Trace**  
> Sample.OnNext(1) 

> **Trace**  
> Sample.OnCompleted() 

> **Trace**  
> Sample.Dispose() 

> **Debug**  
> Sample took 00:00:00.0038663 

The full [LinqPad](http://www.linqpad.net) sample in available as [Logging.linq](Logging.linq)

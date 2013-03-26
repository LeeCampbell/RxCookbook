#Logging

Logging of observable sequences is made easy with the `Do` operator. 
However we can further generalise it create a fully featured extension method for logging.

Simple usage of the `Do` operator allows us to intercept `OnNext`, `OnError` and `OnCompleted` actions.

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

Output

> **Debug**  
> OnNext(1) 

> **Debug**  
> OnCompleted() 

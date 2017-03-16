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
</Query>

void Main()
{
	var testScheduler = new TestScheduler();
	
	//A series of bytes/chars to be treated as buffer read from a stream (10 at a time).
	//	a \n\n represents a record delimiter.
	var source = testScheduler.CreateColdObservable<char[]>(
		ReactiveTest.OnNext(0100, new char[] { 'a', 'b', 'c', 'd', 'e', 'f', 'g', '\n','\n','h' }),
		ReactiveTest.OnNext(0200, new char[] { 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', '\n','\n' }),
		ReactiveTest.OnNext(0300, new char[] { 'q', 'r', 's', '\n','\n','t', 'u', 'v', '\n','\n' }),
		ReactiveTest.OnNext(0400, new char[] { 'w', 'x', '\n','y', 'z' }),
		ReactiveTest.OnCompleted<char[]>(500)
	);

	var delimiter = '\n';
	var observer = testScheduler.CreateObserver<string>();
	var shared = source.SelectMany(buffer=>buffer).Publish().RefCount();
	var subscription = shared
		//Signal a buffer is complete when we see consecutive delimiters
		.Buffer(() => shared.Scan(Tuple.Create(' ',' '), (acc, cur)=>Tuple.Create(acc.Item2, cur)).Where(t => t.Item1 == delimiter && t.Item2==delimiter))
		//Alternative implementation of the above line
		//	.Buffer(() => shared.Buffer(2,1).Where(pair => pair[0] == delimiter && pair[1]==delimiter))
		
		//Remove trailing delimiters
		.Select(chunk =>
			{
				var idx = chunk.Count-1;
				while (chunk[idx]==delimiter)
				{
					idx--;
				}
				return chunk.Take(idx+1);
			})
		//Alternative implementation to above. This mutates intermediate state instead.
		//.Select(chunk =>
		//	{
		//		var len = chunk.Count;
		//		while(chunk[chunk.Count-1]==delimiter)
		//		{
		//			chunk.RemoveAt(chunk.Count-1);
		//		}
		//		return chunk;
		//	})

		//Only yield results that have content. i.e. consecutive delimters are ignored e.g. \n\n\n and \n\n\n\n
		.Where(chunk=>chunk.Any())
		//Transform the buffered value to the desired record type, in our case a string is fine.
		.Select(chunk=>new string(chunk.ToArray()))
		.Subscribe(observer);

	testScheduler.Start();
	observer.Messages.Take(5).AssertEqual(
		ReactiveTest.OnNext(0100, "abcdefg"),
		ReactiveTest.OnNext(0200, "hijklmnop"),
		ReactiveTest.OnNext(0300, "qrs"),
		ReactiveTest.OnNext(0300, "tuv"),
		ReactiveTest.OnNext(0500, "wx\nyz")
    );
	
	
}

// Define other methods and classes here

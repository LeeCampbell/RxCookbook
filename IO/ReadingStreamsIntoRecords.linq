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
		ReactiveTest.OnNext(0100, new char[] { 'a', 'b', 'c', 'd', 'e', 'f', 'g', '\n', '\n', 'h' }),
		ReactiveTest.OnNext(0200, new char[] { 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', '\n', '\n' }),
		ReactiveTest.OnNext(0300, new char[] { 'q', 'r', 's', '\n', '\n', 't', 'u', 'v', '\n', '\n' }),
		ReactiveTest.OnNext(0400, new char[] { 'w', 'x', '\n', 'y', 'z', '\n', '\n' })
    );

	var delimiter = '\n';
	var observer = testScheduler.CreateObserver<string>();
	var shared = source.SelectMany(buffer=>buffer).Publish().RefCount();
	var subscription = shared
		.Buffer(() => shared.Scan(Tuple.Create(' ',' '), (acc, cur)=>Tuple.Create(acc.Item2, cur)).Where(t => t.Item1 == delimiter && t.Item2==delimiter))
		.Select(chunk =>
			{
				var len = chunk.Count;
				while(chunk[chunk.Count-1]==delimiter)
				{
					chunk.RemoveAt(chunk.Count-1);
				}
				return chunk;
			})
		.Where(chunk=>chunk.Any())
		.Select(chunk=>new string(chunk.ToArray()))
		.Subscribe(observer);

	testScheduler.Start();
	observer.Messages.AssertEqual(
		ReactiveTest.OnNext(0100, "abcdefg"),
		ReactiveTest.OnNext(0200, "hijklmnop"),
		ReactiveTest.OnNext(0300, "qrs"),
		ReactiveTest.OnNext(0300, "tuv"),
		ReactiveTest.OnNext(0400, "wx\nyz")
    );
	
	
}

// Define other methods and classes here

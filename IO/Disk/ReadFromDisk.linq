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
		.Dump("This file contents");
}

// Define other methods and classes here
public class FileReader
{
   public IObservable<string> StreamLines(string filePath)
   {
       return Observable.Create<string>(
           async (o, cts) =>
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
                         if (!cts.IsCancellationRequested && !reader.EndOfStream)
                         {
                             o.OnCompleted();
                         }
                     }
                 });
   }
}
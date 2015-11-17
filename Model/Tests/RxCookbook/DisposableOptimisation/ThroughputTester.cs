using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;

namespace RxCookbook.DisposableOptimisation
{
    public static class ThroughputTester
    {
        private static readonly Dictionary<string, IRunnable> _testCandidates = new Dictionary<string, IRunnable>()
        {
            {"SerialDisposable", new SerialThroughputTest<SerialDisposable>(()=>new SerialDisposable(), (sut,other)=>{sut.Disposable = other;})},
            {"SerialDisposableLockFree1", new SerialThroughputTest<SerialDisposableLockFree1>(()=>new SerialDisposableLockFree1(), (sut,other)=>{sut.Disposable = other;})},
            {"SerialDisposableLockFree2", new SerialThroughputTest<SerialDisposableLockFree2>(()=>new SerialDisposableLockFree2(), (sut,other)=>{sut.Disposable = other;})},
            {"SerialDisposableUnsafe", new SerialThroughputTest<SerialDisposableUnsafe>(()=>new SerialDisposableUnsafe(), (sut,other)=>{sut.Disposable = other;})},
            {"SerialDisposableVolatile", new SerialThroughputTest<SerialDisposableVolatile>(()=>new SerialDisposableVolatile(), (sut,other)=>{sut.Disposable = other;})},
        };

        public static void Run()
        {
            Console.WriteLine("Priming...");
            var normalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            foreach (var testCandidate in _testCandidates)
            {
                var t = testCandidate.Value.Run();
                t.ToArray();
                Console.WriteLine("Prime run {0}.", testCandidate.Key);
            }

            Console.WriteLine("Priming complete.");
            Console.WriteLine();
            Console.ForegroundColor = normalColor;
            Console.WriteLine();

            var results = new Dictionary<string, IEnumerable<ThroughputTestResult>>();

            foreach (var testCandidate in _testCandidates)
            {
                var result = testCandidate.Value.Run();
                results[testCandidate.Key] = result;
            }

            var colHeaders = results.First().Value.Select(tr => tr.Concurrency.ToString()).ToArray();
            var rowHeaders = results.OrderByDescending(r => r.Value.Max(x => x.Elapsed)).Select(r => r.Key).ToArray();

            var output = ResultsToFixedWdith(
                "Concurrency", colHeaders,
                "Type", rowHeaders,
                (col, row) =>
                {
                    var key = rowHeaders[row];
                    var vertex = results[key].OrderBy(tr => tr.Concurrency).Skip(col).First();
                    var opsPerSec = vertex.Messages / vertex.Elapsed.TotalSeconds;
                    return opsPerSec.ToString("N0");
                });

            Console.WriteLine(output);
            Console.WriteLine();
            Console.WriteLine("Test run complete. Press any key to exit.");
        }

        private static string ResultsToFixedWdith(string columnLabel, string[] columnHeaders, string rowLabel, string[] rowHeaders, Func<int, int, string> valueSelector)
        {
            var maxValueLength = columnHeaders.Max(h => h.Length);
            var values = new string[columnHeaders.Length, rowHeaders.Length];
            for (int y = 0; y < rowHeaders.Length; y++)
            {
                for (int x = 0; x < columnHeaders.Length; x++)
                {
                    var value = valueSelector(x, y);
                    values[x, y] = value;
                    if (value.Length > maxValueLength) maxValueLength = value.Length;
                }
            }

            var colWidth = maxValueLength + 1;
            var labelWidth = rowHeaders.Concat(new[] { rowLabel }).Max(h => h.Length) + 1;

            var sb = new StringBuilder();
            sb.Append("".PadRight(labelWidth));
            sb.Append(columnLabel);
            sb.AppendLine();
            sb.Append(rowLabel.PadLeft(labelWidth));
            foreach (string header in columnHeaders)
            {
                sb.Append(header.PadLeft(colWidth));
            }
            sb.AppendLine();
            for (int y = 0; y < rowHeaders.Length; y++)
            {
                sb.Append(rowHeaders[y].PadLeft(labelWidth));
                for (int x = 0; x < columnHeaders.Length; x++)
                {

                    sb.Append(valueSelector(x, y).PadLeft(colWidth));
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
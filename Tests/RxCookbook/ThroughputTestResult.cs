using System;
using System.Diagnostics;

namespace RxCookbook
{
    internal sealed class ThroughputTestResult : IDisposable
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly int _preRunGen0AllocationCount;

        public ThroughputTestResult(int concurrency, int messages)
        {
            Concurrency = concurrency;
            Messages = messages;
            _preRunGen0AllocationCount = GC.CollectionCount(0);
        }

        public int Concurrency { get; private set; }
        public int Messages { get; private set; }
        public int Gen0Collections { get; private set; }
        public TimeSpan Elapsed { get; private set; }
        public void Dispose()
        {
            _stopwatch.Stop();
            Gen0Collections = GC.CollectionCount(0) - _preRunGen0AllocationCount;
            Elapsed = _stopwatch.Elapsed;
        }

        public static ThroughputTestResult InvalidResult(int concurrency, int messages)
        {
            var result = new ThroughputTestResult(concurrency, messages);
            result.Dispose();
            result.Elapsed = TimeSpan.MaxValue;
            return result;
        }
    }
}
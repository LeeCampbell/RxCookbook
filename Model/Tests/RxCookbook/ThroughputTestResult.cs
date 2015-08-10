using System;
using System.Diagnostics;

namespace RxCookbook
{
    internal sealed class ThroughputTestResult : IDisposable
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly int _preRunGen0AllocationCount;

        public ThroughputTestResult(int subscriptions, int messages)
        {
            Subscriptions = subscriptions;
            Messages = messages;
            _preRunGen0AllocationCount = GC.CollectionCount(0);
        }

        public int Subscriptions { get; private set; }
        public int Messages { get; private set; }
        public int Gen0Collections { get; private set; }
        public TimeSpan Elapsed { get; private set; }
        public void Dispose()
        {
            _stopwatch.Stop();
            Gen0Collections = GC.CollectionCount(0) - _preRunGen0AllocationCount;
            Elapsed = _stopwatch.Elapsed;
        }
    }
}
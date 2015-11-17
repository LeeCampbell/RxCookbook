using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using HdrHistogram;

namespace RxCookbook.LoadShedding
{
    class ObserveLatestOnPerfTests
    {
        const int EventsPerSecond = 5000;
        const int ProductionWindowMilliseconds = 50;
        const int WindowsPerSecond = 1000 / ProductionWindowMilliseconds;
        const int ItemsPerWindow = EventsPerSecond / WindowsPerSecond;
        const int MinutesToRun = 1;
        private const int ValuesToProduce = EventsPerSecond * (60 * MinutesToRun);
        private static readonly Dictionary<string, Func<IObservable<Payload>, IScheduler, IObservable<Payload>>> _testCandidates = new Dictionary<string, Func<IObservable<Payload>, IScheduler, IObservable<Payload>>>()
        {
            //{"ObserveOn", Observable.ObserveOn},
            {"ObserveLatestOn", ObservableExtensions.ObserveLatestOn},
            {"ObserveLatestOnOptimisedSerialDisposable", ObservableExtensions.ObserveLatestOnOptimisedSerialDisposable},
            //{"TakeMostRecent", ObservableExtensions.TakeMostRecent}
        };


        public static void Run()
        {
            string output;
            Console.WriteLine("Priming...");
            //for (int i = 0; i < 2; i++)
            //{
                foreach (var testCandidate in _testCandidates)
                {
                    output = RunTest(testCandidate.Value, null);
                    //Console.WriteLine("Prime run {0}. {1} OutputLength:{2}", i, testCandidate.Key, output.Length);
                    Console.WriteLine("Prime run {0}. {1} OutputLength:{2}", 0, testCandidate.Key, output.Length);
                }
            //}
            Console.WriteLine("Priming complete.");
            Console.WriteLine();
            Console.WriteLine();

            foreach (var testCandidate in _testCandidates)
            {
                output = RunTest(testCandidate.Value, testCandidate.Key);
                Console.WriteLine(output);
                Console.WriteLine();
            }
            
            Console.WriteLine();
            Console.WriteLine("Test run complete. Press any key to exit.");
        }

        private static string RunTest(Func<IObservable<Payload>, IScheduler, IObservable<Payload>> loadShedder, string subscriptionMode)
        {
            Program.Clean();

            var sb = new StringBuilder();

            sb.AppendFormat("Events/s {0}", EventsPerSecond);
            sb.AppendLine();
            sb.AppendFormat("Windows/s {0}", WindowsPerSecond);
            sb.AppendLine();
            sb.AppendFormat("Items/Window {0}", ItemsPerWindow);
            sb.AppendLine();
            sb.AppendFormat("Minutes to run {0}", MinutesToRun);
            sb.AppendLine();
            sb.AppendFormat("Values to produce {0}", ValuesToProduce);
            sb.AppendLine();


            var itemsProduced = 0;
            var producerEls = new EventLoopScheduler(start => new Thread(start) { Name = "Producer", IsBackground = true });
            var consumerEls = new EventLoopScheduler(start => new Thread(start) { Name = "Consumer", IsBackground = true });
            var source = Observable.Interval(TimeSpan.FromMilliseconds(ProductionWindowMilliseconds), producerEls)
                .SelectMany(i => new int[ItemsPerWindow])
                .Select((_, idx) => new Payload(idx))                
                .Take(ValuesToProduce)
                .Do(i => itemsProduced++, ()=>Console.WriteLine(" Producer complete"));

            Console.WriteLine("Producer started {0}", subscriptionMode);

            var latencyRecorder = new LongHistogram(1000000000L * 60L * 30L, 3); // 1 ns to 30 minutes

            var mre = new AutoResetEvent(false);
            int receiveCount = 0;
            var startTimeStamp = Stopwatch.GetTimestamp();
            var subscription =  loadShedder(source, consumerEls)
                //Only allow the test run to blow out to 5x slower than production
                .TakeUntil(Observable.Timer(TimeSpan.FromMinutes(MinutesToRun * 5)))
                .Finally(() =>
                         {
                             var endTimeStamp = Stopwatch.GetTimestamp();
                             sb.AppendFormat("Elapsed time     {0}", TimeSpan.FromTicks(endTimeStamp - startTimeStamp));
                             mre.Set();
                         })
                .Subscribe(
                    payload =>
                    {
                        payload.Received();
                        receiveCount++; //Should be thread-safe here.
                        latencyRecorder.RecordValue(payload.ReceiveLatency());

                        //10k -> 0.036
                        //20k -> 0.09s
                        //30k -> 0.166s
                        //40k -> 0.250s

                        var primeIndex = ((payload.Id%4) + 1)*10000;

                        FindPrimeNumber(primeIndex);
                        payload.Processed();

                        if (payload.Id%1000 == 0)
                        {
                            Console.WriteLine(" Processed {0} events in {1}", payload.Id, TimeSpan.FromTicks(Stopwatch.GetTimestamp()-startTimeStamp));
                        }
                    },
                    Console.WriteLine,
                    () =>
                    {
                        if(subscriptionMode==null)
                            return;
                        
                        sb.AppendLine();
                        sb.AppendFormat("Processing complete - {0}", subscriptionMode);
                        sb.AppendLine();
                        sb.AppendFormat("Expected to produced {0} events.", ValuesToProduce);
                        sb.AppendLine();
                        sb.AppendFormat("Produced {0} events.", itemsProduced);
                        sb.AppendLine();
                        sb.AppendFormat("Received {0} events.", receiveCount);
                        sb.AppendLine();

                        var writer = new StringWriter(sb);
                        latencyRecorder.OutputPercentileDistribution(writer, 10);
                        sb.AppendLine();

                        var sw = new StringWriter();
                        var dateTime = DateTime.Now.ToString("yyyy-MM-ddThh.mm.ss");
                        var fileName = string.Format(@".\output-{0}-{1}.hgrm", subscriptionMode, dateTime);
                        latencyRecorder.OutputPercentileDistribution(sw);
                        File.WriteAllText(fileName, sw.ToString());
                        sb.AppendFormat("Results saved to {0}", fileName);

                    });
            Console.WriteLine("Waiting...");
            mre.WaitOne();
            Console.WriteLine("Disposing...");
            mre.Dispose();
            subscription.Dispose();
            //Enqueue the disposal of the event loop as it's last scheduled item.
            producerEls.Schedule(() => producerEls.Dispose());
            consumerEls.Schedule(() => consumerEls.Dispose());
            
            return sb.ToString();
        }

        private static long FindPrimeNumber(int n)
        {
            int count = 0;
            long a = 2;
            while (count < n)
            {
                long b = 2;
                int prime = 1;// to check if found a prime
                while (b * b <= a)
                {
                    if (a % b == 0)
                    {
                        prime = 0;
                        break;
                    }
                    b++;
                }
                if (prime > 0)
                    count++;
                a++;
            }
            return (--a);
        }
    }

    internal class Payload
    {
        private readonly int _id;
        private readonly long _createdTimestamp;
        private long _recievedTimestamp;
        private long _processedTimestamp;

        private static readonly long TicksPerSecond = Stopwatch.Frequency;
        private static readonly long TicksPerMillisecond = TicksPerSecond / 1000;

        static Payload()
        {
            double ticksPerMs = ((double)Stopwatch.Frequency) / 1000.0;

            if (TicksPerMillisecond != (long)ticksPerMs) throw new InvalidOperationException("Assumptions about ticks are wrong. -LC");
        }

        public Payload(int id)
        {
            _id = id;
            _createdTimestamp = Stopwatch.GetTimestamp();
        }

        public int Id { get { return _id; } }

        public void Received()
        {
            _recievedTimestamp = Stopwatch.GetTimestamp();
        }
        public void Processed()
        {
            _processedTimestamp = Stopwatch.GetTimestamp();
        }

        public long ReceiveLatency()
        {
            return _recievedTimestamp - _createdTimestamp;
        }

        private static long GetElapsedMs(long start, long end)
        {
            var elapsedTicks = (end - start);
            var elapsedMs = elapsedTicks * TicksPerMillisecond;
            return elapsedMs;
        }
    }
}

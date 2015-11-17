using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;

namespace RxCookbook.DisposableOptimisation
{
    internal class SerialThroughputTest<T> : IRunnable
         where T : ICancelable
    {
        private const int RunSize = 10 * 1000 * 1000;
        private readonly Func<T> _serialDisposableFactory;
        private readonly Action<T, IDisposable> _assign;

        public SerialThroughputTest(Func<T> serialDisposableFactory, Action<T, IDisposable> assign)
        {
            _serialDisposableFactory = serialDisposableFactory;
            _assign = assign;
        }

        public IEnumerable<ThroughputTestResult> Run()
        {
            yield return RunSynchronously();
            int maxParallelism = 2;
            do
            {
                yield return RunConcurrently(maxParallelism);
                maxParallelism *= 2;
            } while (maxParallelism <= Environment.ProcessorCount);
        }

        private ThroughputTestResult RunSynchronously()
        {
            var messages = CreateMessages();
            var sut = _serialDisposableFactory();

            Program.Clean();

            var result = new ThroughputTestResult(1, RunSize);
            foreach (var item in messages)
            {
                _assign(sut, item);
            }
            sut.Dispose();
            result.Dispose();
            Console.WriteLine($"Elapsed {result.Elapsed.TotalSeconds}sec");
            if (messages.Any(b => !b.IsDisposed))
            {
                Console.WriteLine($"{sut.GetType().Name} operated incorrectly. There are still {messages.Count(b => !b.IsDisposed)} objects not disposed.");
                return ThroughputTestResult.InvalidResult(1, RunSize);
            }
            return result;
        }

        private ThroughputTestResult RunConcurrently(int threads)
        {
            var messages = CreateMessages();
            var sut = _serialDisposableFactory();

            Program.Clean();

            var result = new ThroughputTestResult(threads, RunSize);
            Parallel.ForEach(
                messages,
                new ParallelOptions { MaxDegreeOfParallelism = threads },
                (item, state, idx) => _assign(sut, item));

            sut.Dispose();
            result.Dispose();

            if (messages.Any(b => !b.IsDisposed))
            {
                Console.WriteLine($"{sut.GetType().Name} operated incorrectly. There are still {messages.Count(b => !b.IsDisposed)} objects not disposed.");
                return ThroughputTestResult.InvalidResult(threads, RunSize);
            }

            return result;
        }

        private static BooleanDisposable[] CreateMessages()
        {
            var messages = new BooleanDisposable[RunSize];
            for (int i = 0; i < RunSize; i++)
            {
                messages[i] = new BooleanDisposable();
            }
            return messages;
        }
    }
}
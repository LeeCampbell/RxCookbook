using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive;
using System.Reactive.Linq;

namespace RxCookbook.INPC
{
    internal static class InpcPerfTests
    {
        private const int MessageCount = 1000 * 1000 * 100;

        private static readonly Dictionary<string, Func<ThroughputTestResult>> TestCandidates = new Dictionary
            <string, Func<ThroughputTestResult>>
        {
            //Listed in roughly the order of performance. Fastest first.
            { "Optimised Events. No Rx", RunOptInpcEvt},
            { "Standard Events. No Rx", RunInpcEvt},
            { "Optimised Events, Rx with no property name filter", RunUnitInpcObs},
            { "Optimized Events, Optimised Rx, no Expressions", RunExtremeInpcObs},
            { "Optimized Events, Optimised Rx, Dynamic compiled expressions", RunExtremeCompiledInpcObs},
            { "Optimized Events, Optimised Rx", RunNoAllocInpcObs},
            { "Optimized Rx", RunOptInpcObs},
            { "Standard Rx INPC", RunStdInpcObs},
            { "Standard Rx INPC with C#6", RunStdInpcObsWithCSharp6},
        };

        public static void Run()
        {
            //Avoid any warm up cost and also attempt to get the code JIT compiled -LC
            var longestTestName = 0;
            var counter = 0;
            Console.Write("Priming for JIT..");
            foreach (var testCandidate in TestCandidates)
            {
                Console.Write(".");
                longestTestName = Math.Max(longestTestName, testCandidate.Key.Length);
                for (int i = 0; i < 1; i++)
                {
                    counter += testCandidate.Value().Gen0Collections;
                }
            }
            Console.WriteLine();
            Console.WriteLine("Cleaning env");
            Program.Clean();
            Console.WriteLine("Running tests...");
            foreach (var testCandidate in TestCandidates)
            {
                var result = testCandidate.Value();
                var stringformat = @"{0," + (longestTestName + 1) + "}- msg:{1}  GCs: {2,4}  Elapsed: {3}";
                Console.WriteLine(stringformat, testCandidate.Key, result.Messages, result.Gen0Collections, result.Elapsed);
                Program.Clean();
            }
            Program.Clean();
            Console.WriteLine();
            Console.WriteLine("Complete.");
        }

        private static ThroughputTestResult RunStdInpcObs()
        {
            var count = 0;
            var person = new Person();
            using (person.OnPropertyChanges(p => p.Age)
                         .Subscribe(newAge => count = newAge))
            {
                var result = new ThroughputTestResult(1, MessageCount);
                for (int i = 0; i < MessageCount; i++)
                {
                    person.Age = i;
                }
                result.Dispose();
                return result;
            }
        }

        private static ThroughputTestResult RunStdInpcObsWithCSharp6()
        {
            var count = 0;
            var person = new Person_cSharp6();
            using (person.OnPropertyChanges(p => p.Age)
                         .Subscribe(newAge => count = newAge))
            {
                var result = new ThroughputTestResult(1, MessageCount);
                for (int i = 0; i < MessageCount; i++)
                {
                    person.Age = i;
                }
                result.Dispose();
                return result;
            }
        }

        private static ThroughputTestResult RunOptInpcObs()
        {
            var count = 0;
            var person = new Person();
            using (person.OnPropertyChangesOpt(p => p.Age)
                         .Subscribe(newAge => count = newAge))
            {
                var result = new ThroughputTestResult(1, MessageCount);
                for (int i = 0; i < MessageCount; i++)
                {
                    person.Age = i;
                }
                result.Dispose();
                return result;
            }
        }

        private static ThroughputTestResult RunNoAllocInpcObs()
        {
            var count = 0;
            var person = new PersonOpt();
            using (person.OnPropertyChangesOpt(p => p.Age)
                         .Subscribe(newAge => count = newAge))
            {
                var result = new ThroughputTestResult(1, MessageCount);
                for (int i = 0; i < MessageCount; i++)
                {
                    person.Age = i;
                }
                result.Dispose();
                return result;
            }
        }

        private static ThroughputTestResult RunExtremeInpcObs()
        {
            var count = 0;
            var person = new PersonOpt();
            using (person.OnPropertyChangesOpt(nameof(person.Age), p => p.Age)
                         .Subscribe(newAge => count = newAge))
            {
                var result = new ThroughputTestResult(1, MessageCount);
                for (int i = 0; i < MessageCount; i++)
                {
                    person.Age = i;
                }
                result.Dispose();
                return result;
            }
        }

        private static ThroughputTestResult RunExtremeCompiledInpcObs()
        {
            var count = 0;
            var person = new PersonOpt();
            using (person.OnPropertyChangesCompiled(p => p.Age)
                         .Subscribe(newAge => count = newAge))
            {
                var result = new ThroughputTestResult(1, MessageCount);
                for (int i = 0; i < MessageCount; i++)
                {
                    person.Age = i;
                }
                result.Dispose();
                return result;
            }
        }

        private static ThroughputTestResult RunUnitInpcObs()
        {
            var count = 0;
            var person = new PersonOpt();
            using (person.ToObservable()
                         .Select(_ => Unit.Default)
                         .Subscribe(newAge => count++))
            {
                var result = new ThroughputTestResult(1, MessageCount);
                for (int i = 0; i < MessageCount; i++)
                {
                    person.Age = i;
                }
                result.Dispose();
                return result;
            }
        }

        private static ThroughputTestResult RunInpcEvt()
        {
            var count = 0;
            var person = new Person();

            PropertyChangedEventHandler handler = (s, e) =>
            {
                if (e.PropertyName == "Age")
                {
                    count = person.Age;
                }
            };
            person.PropertyChanged += handler;
            var result = new ThroughputTestResult(1, MessageCount);
            for (int i = 0; i < MessageCount; i++)
            {
                person.Age = i;
            }
            result.Dispose();
            person.PropertyChanged -= handler;
            return result;

        }

        private static ThroughputTestResult RunOptInpcEvt()
        {
            var count = 0;
            var person = new PersonOpt();

            PropertyChangedEventHandler handler = (s, e) =>
            {
                if (e.PropertyName == "Age")
                {
                    count = person.Age;
                }
            };
            person.PropertyChanged += handler;
            var result = new ThroughputTestResult(1, MessageCount);
            for (int i = 0; i < MessageCount; i++)
            {
                person.Age = i;
            }
            result.Dispose();
            person.PropertyChanged -= handler;
            return result;
        }
    }
}

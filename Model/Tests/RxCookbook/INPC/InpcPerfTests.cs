using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxCookbook.INPC
{
    class InpcPerfTests
    {
        private const int MessageCount = 1000 * 1000 * 100;

        public static void Run()
        {
            for (int i = 0; i < 3; i++)
            {
                RunStdInpcObs();
            }
            for (int i = 0; i < 3; i++)
            {
                RunOptInpcObs();
            }
            for (int i = 0; i < 3; i++)
            {
                RunExtremeInpcObs();
            }
            for (int i = 0; i < 3; i++)
            {
                RunInpcEvt();
            }
            for (int i = 0; i < 3; i++)
            {
                RunOptInpcEvt();
            }
            Program.Clean();
            var stdResult = RunStdInpcObs();
            Program.Clean();
            var optResult = RunOptInpcObs();
            Program.Clean();
            var noAllocObsResult = RunNoAllocInpcObs();
            Program.Clean();
            var extremeResult = RunExtremeInpcObs();
            Program.Clean();
            var dynamCompileResult = RunExtremeCompiledInpcObs();
            Program.Clean();
            var evtResult = RunInpcEvt();
            Program.Clean();
            var optEvtResult = RunOptInpcEvt();
            Program.Clean();

            var obsNullResult = RunNullInpcObs();
            Program.Clean();
            var obsUnitResult = RunUnitInpcObs();
            Program.Clean();

            Console.WriteLine("Standard  - msg:{0}  GCs: {1}  Elapsed: {2} ", stdResult.Messages, stdResult.Gen0Collections, stdResult.Elapsed);
            Console.WriteLine("Optimized - msg:{0}  GCs: {1}  Elapsed: {2} ", optResult.Messages, optResult.Gen0Collections, optResult.Elapsed);
            Console.WriteLine("noAlloc   - msg:{0}  GCs: {1}  Elapsed: {2} ", noAllocObsResult.Messages, noAllocObsResult.Gen0Collections, noAllocObsResult.Elapsed);
            Console.WriteLine("extreme   - msg:{0}  GCs: {1}  Elapsed: {2} ", extremeResult.Messages, extremeResult.Gen0Collections, extremeResult.Elapsed);
            Console.WriteLine("dynamComp - msg:{0}  GCs: {1}  Elapsed: {2} ", dynamCompileResult.Messages, dynamCompileResult.Gen0Collections, dynamCompileResult.Elapsed);
            Console.WriteLine("obsNull   - msg:{0}  GCs: {1}  Elapsed: {2} ", obsNullResult.Messages, obsNullResult.Gen0Collections, obsNullResult.Elapsed);
            Console.WriteLine("obsUnit   - msg:{0}  GCs: {1}  Elapsed: {2} ", obsUnitResult.Messages, obsUnitResult.Gen0Collections, obsUnitResult.Elapsed);
            Console.WriteLine("Evt       - msg:{0}  GCs: {1}  Elapsed: {2} ", evtResult.Messages, evtResult.Gen0Collections, evtResult.Elapsed);
            Console.WriteLine("Opt Evt   - msg:{0}  GCs: {1}  Elapsed: {2} ", optEvtResult.Messages, optEvtResult.Gen0Collections, optEvtResult.Elapsed);

            Console.ReadLine();
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
            using (person.OnPropertyChangesOpt("Age", p => p.Age)
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

        private static ThroughputTestResult RunNullInpcObs()
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

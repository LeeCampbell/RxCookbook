using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace RxCookbook
{
    class Program
    {
        private const int MessageCount = 1000 * 1000 * 100;

        static void Main(string[] args)
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
            Clean();
            var stdResult = RunStdInpcObs();
            Clean();
            var optResult = RunOptInpcObs();
            Clean();
            var noAllocObsResult = RunNoAllocInpcObs();
            Clean();
            var extremeResult = RunExtremeInpcObs();
            Clean();
            var dynamCompileResult = RunExtremeCompiledInpcObs();
            Clean();
            var evtResult = RunInpcEvt();
            Clean();
            var optEvtResult = RunOptInpcEvt();
            Clean();

            var obsNullResult = RunNullInpcObs();
            Clean();
            var obsUnitResult = RunUnitInpcObs();
            Clean();

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

        private static void Clean()
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
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
                         .Select(_=>Unit.Default)
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
                         .Select(_=>Unit.Default)
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
    public static class PropertyExtensions
    {
        /// <summary>
        /// Gets property information for the specified <paramref name="property"/> expression.
        /// </summary>
        /// <typeparam name="TSource">Type of the parameter in the <paramref name="property"/> expression.</typeparam>
        /// <typeparam name="TValue">Type of the property's value.</typeparam>
        /// <param name="property">The expression from which to retrieve the property information.</param>
        /// <returns>Property information for the specified expression.</returns>
        /// <exception cref="ArgumentException">The expression is not understood.</exception>
        public static PropertyInfo GetPropertyInfo<TSource, TValue>(this Expression<Func<TSource, TValue>> property)
        {
            if (property == null)
                throw new ArgumentNullException("property");

            var body = property.Body as MemberExpression;
            if (body == null)
                throw new ArgumentException("Expression is not a property", "property");

            var propertyInfo = body.Member as PropertyInfo;
            if (propertyInfo == null)
                throw new ArgumentException("Expression is not a property", "property");

            return propertyInfo;
        }
    }
    public static class InpcObsEx
    {
        public static IObservable<TProperty> OnPropertyChanges<T, TProperty>(this T source, Expression<Func<T, TProperty>> property)
            where T : INotifyPropertyChanged
        {
            return Observable.Create<TProperty>(o =>
            {
                var propertyName = property.GetPropertyInfo().Name;
                var propertySelector = property.Compile();

                return Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                                handler => handler.Invoke,
                                h => source.PropertyChanged += h,
                                h => source.PropertyChanged -= h)
                            .Where(e => e.EventArgs.PropertyName == propertyName)
                            .Select(e => propertySelector(source))
                            .Subscribe(o);
            });
        }

        public static IObservable<TProperty> OnPropertyChangesCompiled<T, TProperty>(this T source, Expression<Func<T, TProperty>> property)
            where T : INotifyPropertyChanged
        {
            return Observable.Create<TProperty>(o =>
            {
                var propertyName = property.GetPropertyInfo().Name;
                var propertySelector = property.CompileDynamically();

                return Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                                handler => handler.Invoke,
                                h => source.PropertyChanged += h,
                                h => source.PropertyChanged -= h)
                            .Where(e => e.EventArgs.PropertyName == propertyName)
                            .Select(e => propertySelector(source))
                            .Subscribe(o);
            });
        }

        public static Func<T1, T2> CompileDynamically<T1, T2>(this Expression<Func<T1, T2>> source)
        {
            var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(
                new AssemblyName("MyAssembly_" + Guid.NewGuid().ToString("N")), 
                AssemblyBuilderAccess.Run);

            var moduleBuilder = assemblyBuilder.DefineDynamicModule("Module");

            var typeBuilder = moduleBuilder.DefineType("MyType_" + Guid.NewGuid().ToString("N"), TypeAttributes.Public);

            var methodBuilder = typeBuilder.DefineMethod("MyMethod", MethodAttributes.Public | MethodAttributes.Static);

            source.CompileToMethod(methodBuilder);

            var resultingType = typeBuilder.CreateType();

            var function = Delegate.CreateDelegate(source.Type, resultingType.GetMethod("MyMethod"));
            return (Func<T1, T2>) function;
        }

        public static IObservable<TProperty> OnPropertyChangesOpt<T, TProperty>(this T source, string propertyName, Func<T, TProperty> propertySelector)
            where T : INotifyPropertyChanged
        {
            return source.ToObservable()
                        .Where(e => e.PropertyName == propertyName)
                        .Select(e => propertySelector(source));
        }

        public static IObservable<TProperty> OnPropertyChangesOpt<T, TProperty>(this T source, Expression<Func<T, TProperty>> property)
            where T : INotifyPropertyChanged
        {
            return Observable.Create<TProperty>(o =>
            {
                var propertyName = property.GetPropertyInfo().Name;
                var propertySelector = property.Compile();

                return source.ToObservable()
                            .Where(e => e.PropertyName == propertyName)
                            .Select(e => propertySelector(source))
                            .Subscribe(o);
            });
        }

        public static IObservable<PropertyChangedEventArgs> ToObservable<T>(this T source)
        where T : INotifyPropertyChanged
    {
        return Observable.Create<PropertyChangedEventArgs>(observer =>
            {
                PropertyChangedEventHandler handler = (s, e) => observer.OnNext(e);
                source.PropertyChanged += handler;
                return Disposable.Create(() => source.PropertyChanged -= handler);
            });
    }
    }
}

using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace RxCookbook.INPC
{
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
            return (Func<T1, T2>)function;
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

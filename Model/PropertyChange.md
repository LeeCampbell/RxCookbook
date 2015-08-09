#Observing Property changes

The .NET framework offers several ways to propagate property changes from an Object Model.
The most common are using the `INotifyPropertyChanged` interface, _DependencyProperties_ in WPF, or, using the convention of having a property with a matching property changed event.
Often property change events are used in GUI applications to let the UI know when to automatically redraw.
We can however leverage property change patterns outside of GUI software for what ever reasons we see fit.

##INotifyPropertyChanged
Probably the most common way to expose property changes is by implementing the `System.ComponentModel.INotifyPropertyChanged` interface.
This interface simply exposes a `PropertyChanged` event that will provide access to a string representing the name of the property that changed.
As this interface can be found in the `System.dll` it is available for all .NET code from .NET 2.0 up.

First we can start with an example that implements `INotifyPropertyChanged`.

```csharp
public class Person : INotifyPropertyChanged
{
    string _name;
    int _age;

    public string Name
    {
        get { return _name; }
        set
        {
            if (_name !=value)
            {
                _name = value;
                OnPropertyChanged("Name");
            }
        }
    }

    public int Age
    {
        get { return _age; }
        set
        {
            if (_age !=value)
            {
                _age = value;
                OnPropertyChanged("Age");
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged(string propertyName)
    {
        var handler = PropertyChanged;
        if (handler != null)
        {
            handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
```

To get property change events we simply register an event handler.

```csharp
var Dave = new Person();
Dave.PropertyChanged += (s, e) => { e.PropertyName.Dump(); };
Dave.Name = "Dave";
Dave.Age = 21;
```

Ouput:

    Name
    Age

This is only somewhat useful as I imagine that you probably want to get the new value of the property.
This first extension method allows you to get the entire object when it raise the `PropertyChanged` event.

```csharp
public static class NotificationExtensions
{
    /// <summary>
    /// Returns an observable sequence of the source any time the <c>PropertyChanged</c> event is raised.
    /// </summary>
    /// <typeparam name="T">The type of the source object. Type must implement <seealso cref="INotifyPropertyChanged"/>.</typeparam>
    /// <param name="source">The object to observe property changes on.</param>
    /// <returns>Returns an observable sequence of the value of the source when ever the <c>PropertyChanged</c> event is raised.</returns>
    public static IObservable<T> OnAnyPropertyChanges<T>(this T source)
        where T : INotifyPropertyChanged
    {
        return Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                           handler => handler.Invoke,
                           h => source.PropertyChanged += h,
                           h => source.PropertyChanged -= h)
                       .Select(_=>source);
    }
}
```

This now allows us to use the `OnAnyPropertyChanges` to get access to the person object when ever a property changes.

```csharp
var Dave = new Person();
//Dave.PropertyChanged += (s, e) => { e.PropertyName.Dump(); };
Dave.OnAnyPropertyChanges().Dump("OnAnyPropertyChanges");
Dave.Name = "Dave";
Dave.Age = 21;
```

Output:

    OnAnyPropertyChanges →Person
        UserQuery+Person
        Name Dave
        Age 0
    OnAnyPropertyChanges →Person
        UserQuery+Person
        Name Dave
        Age 21

What could be more useful is to get the value of a specific property as it changes.
To do this we will first introduce an extension method to get the `PropertyInfo` of the property from a strongly type expression

```csharp
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
        {
            throw new ArgumentNullException("property");
        }

        var body = property.Body as MemberExpression;
        if (body == null)
        {
            throw new ArgumentException("Expression is not a property", "property");
        }

        var propertyInfo = body.Member as PropertyInfo;
        if (propertyInfo == null)
        {
            throw new ArgumentException("Expression is not a property", "property");
        }

        return propertyInfo;
    }
}
```

Now we can extend the `NotificationExtensions` class to have a method to get a sequence of property values as it the property changes.

```csharp
/// <summary>
/// Returns an observable sequence of the value of a property when <paramref name="source"/> raises <seealso cref="INotifyPropertyChanged.PropertyChanged"/> for the given property.
/// </summary>
/// <typeparam name="T">The type of the source object. Type must implement <seealso cref="INotifyPropertyChanged"/>.</typeparam>
/// <typeparam name="TProperty">The type of the property that is being observed.</typeparam>
/// <param name="source">The object to observe property changes on.</param>
/// <param name="property">An expression that describes which property to observe.</param>
/// <returns>Returns an observable sequence of the property values as they change.</returns>
public static IObservable<TProperty> OnPropertyChanges<T, TProperty>(this T source, Expression<Func<T, TProperty>> property)
    where T : INotifyPropertyChanged
{
    return  Observable.Create<TProperty>(o=>
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
```

The new method can be used to get just the value of the `Name` property when it changes.

```csharp
var Dave = new Person();
//Dave.PropertyChanged += (s, e) => { e.PropertyName.Dump("PropertyChanged"); };
//Dave.OnAnyPropertyChanges().Dump("OnAnyPropertyChanges");
Dave.OnPropertyChanges(d => d.Name).Dump("OnPropertyChanges");
Dave.Name = "Dave";
Dave.Age = 21;
```

Output:

    OnPropertyChanges →Dave

The full [LinqPad](http://www.linqpad.net) sample in available as [INotifyPropertyChangedSample.linq](INotifyPropertyChangedSample.linq)

##DependencyProperties
In WPF (and derivative frameworks like Silverlight, Windows Phone, Windows Store Apps...), `DependencyObjects` and their `DependencyProperty` members make up a key part of the standardized framework for bring together features for data-binding, animation, styles, property value inheritance and property change notification.
Here we will focus on how to get a property changed sequence from a `DependencyProperty`.

A conventional way to get access to change notification for any `DependencyProperty` is to get access to a `DependencyPropertyDescriptor` for the `DependencyProperty` and then use the `AddValueChanged` method to attach a callback delegate.

In this sample we create a dummy class that exposes a `Title` `DepenedencyProperty`.

```csharp
public class MyControl : DependencyObject
{
    public string Title
    {
    get { return (string)GetValue(TitleProperty); }
    set { SetValue(TitleProperty, value); }
    }

    public static readonly DependencyProperty TitleProperty =
    DependencyProperty.Register("Title", typeof(string), typeof(MyControl),
                            new PropertyMetadata());
}
```

Here we use a `DependencyPropertyDescriptor` to attach a callback when the `Title` property changes.

```csharp
var myControl = new MyControl();

var dpd = DependencyPropertyDescriptor.FromProperty(MyControl.TitleProperty, typeof(MyControl));
EventHandler handler = delegate { myControl.Title.Dump("Title"); };
dpd.AddValueChanged(myControl, handler);

myControl.Title = "New Title";
```

Output:

> **Title**
> New Title

We can use this as the basis to create an observable sequence.
In our example we want to provide clean up by removing the callback.
We use `Observable.Create` to get a `DependencyPropertyDescriptor` when the consumer subscribes to the sequence.
We then push the the new values each time the callback is called.

```csharp
public static class DependencyObjectExtensions
{
    public static IObservable<T> OnPropertyChanges<T>(this DependencyObject source, DependencyProperty property)
    {
        return Observable.Create<T>(o => {
            var dpd = DependencyPropertyDescriptor.FromProperty(property, property.OwnerType);
            if (dpd == null)
            {
                o.OnError(new InvalidOperationException("Can not register change handler for this dependency property."));
            }

            EventHandler handler = delegate { o.OnNext((T)source.GetValue(property)); };
            dpd.AddValueChanged(source, handler);

            return Disposable.Create(() => dpd.RemoveValueChanged(source, handler));
        });
    }
}
```

This can be used by specifying the `DependencyProperty` and its type as the generic type parameter.

```csharp
var myControl = new MyControl();

myControl.OnPropertyChanges<string>(MyControl.TitleProperty).Dump("OnPropertyChanges");

myControl.Title = "New Title";
```

Output:

    OnPropertyChanges →New Title


Perhaps an improvement to this would be to get automatic type resolution by using the expression we had in the _INotifyPropertyChanged_ sample.
We can add this by using the `FromName` instead of the `FromProperty` static method.

```csharp
public static IObservable<TProperty> OnPropertyChanges<T, TProperty>(this T source, Expression<Func<T, TProperty>> property)
    where T : DependencyObject
{
    return Observable.Create<TProperty>(o =>
    {
        var propertyName = property.GetPropertyInfo().Name;
        var dpd = DependencyPropertyDescriptor.FromName(propertyName, typeof(T), typeof(T));
        if (dpd == null)
        {
            o.OnError(new InvalidOperationException("Can not register change handler for this dependency property."));
        }
        var propertySelector = property.Compile();

        EventHandler handler = delegate { o.OnNext(propertySelector(source)); };
        dpd.AddValueChanged(source, handler);

        return Disposable.Create(() => dpd.RemoveValueChanged(source, handler));
    });
}
```

We can now get access to the observable sequence of property changes without requiring us to specify the type of the property.

```csharp
var myControl = new MyControl();

//myControl.OnPropertyChanges<string>(MyControl.TitleProperty).Dump("OnPropertyChanges");
myControl.OnPropertyChanges(c => c.Title).Dump("OnPropertyChanges");

myControl.Title = "New Title";
```

Output:

    OnPropertyChanges →New Title



The full [LinqPad](http://www.linqpad.net) sample in available as [DependencyPropertyChangedSample.linq](DependencyPropertyChangedSample.linq)

##PropertyChanged Convention
An alternative way to notify consumers of a property change is to the use convention of have a (PropertyName)Changed event.
In this example we have a `Name` property with a matching `NameChanged` event.

```csharp
public class Person
{
    string _name;
    public string Name
    {
        get { return _name; }
        set
        {
            _name = value;
            NameChanged(this, EventArgs.Empty);
        }
    }

    public event EventHandler NameChanged;
    private void OnNameChanged()
    {
        var handler = NameChanged;
        if (handler != null)
        {
            handler(this, EventArgs.Empty);
        }
    }
}
```

The simple way to receive change notifications is to register an event handler.

```csharp
var dave = new Person();
dave.NameChanged += (s, e) => { dave.Name.Dump("NameChanged"); };
dave.Name = "Dave";
```

Output:

> **NameChanged**
> Dave

However we can leverage the `TypeDescriptor` type to have a more generic approach to this.
In a similar way to how we accessed the change notification from a `DependencyProperty`, we can get a `PropertyDescriptor` and register a callback with the `AddValueChanged` method.

```csharp
var dave = new Person();
//dave.NameChanged += (s, e) => { dave.Name.Dump("NameChanged"); };

var nameProp = TypeDescriptor.GetProperties(dave)
                    .Cast<PropertyDescriptor>()
                    .Where(pd => pd.Name=="Name")
                    .SingleOrDefault();

EventHandler handler = delegate { dave.Name.Dump("Name"); };
nameProp.AddValueChanged(dave, handler);

dave.Name = "Dave";
```

Output:

> **Name**
> Dave

Just like our approach to `DependencyProperties`, we can turn this into an observable sequence with Rx.

```csharp
public static class ObjectExtensions
{
    public static IObservable<TProperty> OnPropertyChanges<T, TProperty>(this T source, Expression<Func<T, TProperty>> property)
    {
        return Observable.Create<TProperty>(o =>
        {
            var propertyName = property.GetPropertyInfo().Name;
            var propDesc =TypeDescriptor.GetProperties(source)
                        .Cast<PropertyDescriptor>()
                        .Where (pd => string.Equals(pd.Name, propertyName, StringComparison.Ordinal))
                        .SingleOrDefault();
            if (propDesc == null)
            {
                o.OnError(new InvalidOperationException("Can not register change handler for this property."));
            }
            var propertySelector = property.Compile();

            EventHandler handler = delegate { o.OnNext(propertySelector(source)); };
            propDesc.AddValueChanged(source, handler);

            return Disposable.Create(() => propDesc.RemoveValueChanged(source, handler));
        });
    }
}
```

Our usage is very similar to before.

```csharp
var dave = new Person();
dave.OnPropertyChanges(d => d.Name).Dump("OnPropertyChanges");
dave.Name = "Dave";
```

Output:

    OnPropertyChanges →Dave

The full [LinqPad](http://www.linqpad.net) sample in available as [PropertyChangedConvention.linq](PropertyChangedConvention.linq)

## Excess garbage
The above methods are functionally complete, however they can perform unnecessary memory allocations.
If memory pressure or Garbage collection is a concern then you may want to optimise them.

A specific concern is the `Observable.FromEventPattern` factory.
For each event that is converts to an `OnNext` callback, it will wrap the sender and event payload in a `EventPattern<T>` object e.g. code like

```csharp
onNext(new EventPattern<TEventArgs>(sender, eventArgs));
```

occurs several times in the [source code] (https://github.com/Reactive-Extensions/Rx.NET/blob/v2.2.5/Rx.NET/Source/System.Reactive.Linq/Reactive/Linq/Observable/FromEventPattern.cs).

If this is a concern for you then you may want to create your own manual wrapper around these events.
This is done quite simply with `Observable.Create`.
For example

```csharp
var btn = new Button();
var clicks = Observable.Create<RoutedEventArgs>(o =>
{
    RoutedEventHandler handler = (sender, args) => o.OnNext(args);

    btn.Click += handler;
    return Disposable.Create(() => btn.Click -= handler);
});
```

So we could create a generic implementation as such:

```csharp
private static IObservable<PropertyChangedEventArgs> OnPropertyChanges<T>(this T source)
    where T : INotifyPropertyChanged
{
    return Observable.Create<PropertyChangedEventArgs>(observer =>
    {
        PropertyChangedEventHandler handler = (s, e) => observer.OnNext(e);
        source.PropertyChanged += handler;
        return Disposable.Create(() => source.PropertyChanged -= handler);
    });
}
```

As a warning to the reader that is pursuing performance, the optimisation above will reduce your allocations, and hence and GC pressure.
You could further reduce memory allocation by optimising the implementation of the `INotifyPropertyChanged` event flow, removing the allocation of the `PropertyChangedEventArgs` by caching it.
However, if the next thing you do is use an `Expression` to filter the events and extract the property value, you will incur a large computational cost.
This computational cost is likely to dwarf the cost of the garbage collection, unless the .NET compiler and runtime make some large optimisations in version 5 or later.
My current measurements show for 100Million events, the standard method caused 890 Gen0 collections and took 7.9seconds.
The optimised `OnPropertyChanges` above completed with the Expression to filter and project the value, reduced the Gen0 collections to 381 and reduced elapsed time to 6.5seconds.
Further optimising the code to remove `PropertyChangedEventArgs` allocations reducing Gen0 allocations to 0 gain only 160ms to reduce elapsed time to 6.3seconds.
Replacing the Expression with basic `string` and `Func<TSource,TProperty>` carves off half the time to drop the elapsed time to 3.6seconds.

    Standard implementation
        msg:100000000  GCs: 890  Elapsed: 00:00:07.9463508
    Optimized implementation
         msg:100000000  GCs: 381  Elapsed: 00:00:06.5006809
    Optimized implementation, no allocation in INPC
        msg:100000000  GCs:   0  Elapsed: 00:00:06.3432379
    Optimized implementation, no allocation in INPC, Func instead of Expression
        msg:100000000  GCs:   0  Elapsed: 00:00:03.6200376


Micro-benchmarks should be taking with a grain of salt, and you should back up any assumptions about the performance of your code with higher level benchmarks.

##Library implementations
###Rxx

The wise contributors to the [Rxx](http://Rxx.codeplex.com) project have noticed that all of the sample above can actually be rolled into a single extension method.
You see the last example will not only cleverly figure out the property changed convention, but will also identify _DependencyProperties_ and `INotifyPropertyChange` implementations too.
There have been further improvements to cater for quirks in the `TypeDescriptor` type's implementation, so if you are using Rxx, then favour their robust implementation.

[FromPropertyChangedPattern.cs in Rxx](http://rxx.codeplex.com/SourceControl/changeset/view/71357#1142225)

###ReactiveUI
ReactiveUI is a popular GUI framework with Rx at its heart.
It can be useful for building cross platform products with Xamarin (WPF, iOS, Android) as you adopt its change notification and command system, and they will apply platform specific binding.

##More links
Allan Lindqvist's post [Observable from any property in a INotifyPropertyChanged class](http://social.msdn.microsoft.com/Forums/en-US/rx/thread/36bf6ecb-70ea-4be3-aa35-b9a9cbc9a078) on the Rx MSDN Forums.

Richard Szalay's post [GetPropertyValues: a strongly typed reactive wrapper around INotifyPropertyChanged](http://social.msdn.microsoft.com/Forums/en-US/rx/thread/2fc8ab3c-28ed-45a9-a96f-59133a3d103c) on the Rx MSDN Forums.


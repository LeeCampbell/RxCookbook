#Collection changes

The .NET framework provides a standard interface for publishing changes to a collection.
As in the [Observing Property changes sample](PropertyChange.md), the `INotifyCollectionChanged` interface can be found in the _System.dll_, so is available to all .NET software not just GUI applications.

```csharp
namespace System.Collections.Specialized
{
    public interface INotifyCollectionChanged
    {
        event NotifyCollectionChangedEventHandler CollectionChanged;
    }

    public delegate void NotifyCollectionChangedEventHandler(object sender, NotifyCollectionChangedEventArgs e);

    public class NotifyCollectionChangedEventArgs : EventArgs
    {
        // Implementations removed...
        public NotifyCollectionChangedAction Action { get; }
        public IList NewItems { get; }
        public IList OldItems { get; }
        public int NewStartingIndex { get; }
        public int OldStartingIndex { get; }
    }

    public enum NotifyCollectionChangedAction
    {
        Add,
        Remove,
        Replace,
        Move,
        Reset,
    }
}
```

The two common implementations of this interface are the `ObservableCollection<T>` and `ReadOnlyObservableCollection<T>` which can be found in _WindowsBase.dll_ in .NET 3.0, 3.5 & 4.0 and in _System.ObjectModel.dll_ for the newer Portable libraries (both in `System.Collections.ObjectModel`).
Here is an example usage of the `ObservableCollection<T>`.

```csharp
var source = new ObservableCollection<int>();
source.CollectionChanged += (s, e) => { e.Action.Dump("CollectionChanged"); };

source.Add(1);
source.Add(2);
source.Add(3);

source.Remove(2);
source.Clear();
```

Output

> **CollectionChanged**
> Add

> **CollectionChanged**
> Add

> **CollectionChanged**
> Add

> **CollectionChanged**
> Remove

> **CollectionChanged**
> Reset


Here you can see the `Action` for each event as the collection was changed.
You can also get information on the items that were added and at which index they were added from.
The same is the case for removals.

```csharp
var source = new ObservableCollection<int>();
source.CollectionChanged += (s, e) =>
{
    e.NewStartingIndex.Dump("CollectionChanged-NewStartingIndex");
    e.NewItems[0].Dump("CollectionChanged-NewItems[0]");
};

source.Add(1);
source.Add(2);
```

Output

> **CollectionChanged-NewStartingIndex**
> 0

> **CollectionChanged-NewItems[0]**
> 1

> **CollectionChanged-NewStartingIndex**
> 1

> **CollectionChanged-NewItems[0]**
> 2

A thing to note about the implementation of `NotifyCollectionChangedEventArgs` is that when the `Action` value suggests that the `NewItems` or `OldItems` properties are not appropriate, they return null values, not empty lists.
This requires you to either check the `Action` property before accessing the values or checking that the values are not null.

A simple implementation of getting the collection changed data would just be to just use the `Observable.FromEventPattern` factory method.

```csharp
Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
    h=>source.CollectionChanged += h,
    h=>source.CollectionChanged -= h);
```

Output

    CollectionChanges → Action Add
                        NewItems IList (1 item) {1}
                        OldItems null
                        NewStartingIndex 0
                        OldStartingIndex -1

    CollectionChanges → Action Add
                        NewItems IList (1 item) {2}
                        OldItems null
                        NewStartingIndex 1
                        OldStartingIndex -1

    CollectionChanges → Action Add
                        NewItems IList (1 item) {3}
                        OldItems null
                        NewStartingIndex 2
                        OldStartingIndex -1

    CollectionChanges → Action Remove
                        NewItems null
                        OldItems IList (1 item) {2}
                        NewStartingIndex -1
                        OldStartingIndex 1

    CollectionChanges → Action Reset
                        NewItems null
                        OldItems null
                        NewStartingIndex -1
                        OldStartingIndex -1

However this would require us to check the value of the `NewItems` and `OldItems` for null each time, or risk incurring a `NullReferenceException`.
I prefer to project the `NotifyCollectionChangedEventArgs` type into a custom type that removes the `NullReferenceException` risk for me.

```csharp
public sealed class CollectionChangedData<T>
{
    private readonly NotifyCollectionChangedAction _action;
    private readonly ReadOnlyCollection<T> _newItems;
    private readonly ReadOnlyCollection<T> _oldItems;

    public CollectionChangedData(NotifyCollectionChangedEventArgs collectionChangedEventArgs):
    this(collectionChangedEventArgs.OldItems, collectionChangedEventArgs.NewItems)
    {
        _action = collectionChangedEventArgs.Action;
    }

    public CollectionChangedData(T changedItem)
    {
        _action = NotifyCollectionChangedAction.Replace;
        _newItems = new ReadOnlyCollection<T>(new T[] { changedItem });
        _oldItems = new ReadOnlyCollection<T>(new T[] { });
    }

    public CollectionChangedData(IEnumerable oldItems, IEnumerable newItems)
    {
        _newItems = newItems == null
           ? new ReadOnlyCollection<T>(new T[] { })
           : new ReadOnlyCollection<T>(newItems.Cast<T>().ToList());

        _oldItems = oldItems == null
           ? new ReadOnlyCollection<T>(new T[] { })
           : new ReadOnlyCollection<T>(oldItems.Cast<T>().ToList());

        _action = _newItems.Count == 0
           ? NotifyCollectionChangedAction.Reset
           : NotifyCollectionChangedAction.Replace;
    }

    public NotifyCollectionChangedAction Action
    {
        get { return _action; }
    }

    public ReadOnlyCollection<T> NewItems
    {
        get { return _newItems; }
    }

    public ReadOnlyCollection<T> OldItems
    {
        get { return _oldItems; }
    }
}
```

Now using this class we can project the sequence into a value without null values.

```csharp
Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
    h => source.CollectionChanged += h,
    h => source.CollectionChanged -= h)
    .Select(e => new CollectionChangedData<int>(e.EventArgs))
    .Dump("CollectionChanges");
```

Output:

    CollectionChanges → Action Add
                        NewItems ReadOnlyCollection<Int32> (1 item) {1}
                        OldItems ReadOnlyCollection<Int32> (0 item) {}

    CollectionChanges → Action Add
                        NewItems ReadOnlyCollection<Int32> (1 item) {2}
                        OldItems ReadOnlyCollection<Int32> (0 item) {}

    CollectionChanges → Action Add
                        NewItems ReadOnlyCollection<Int32> (1 item) {3}
                        OldItems ReadOnlyCollection<Int32> (0 item) {}

    CollectionChanges → Action Remove
                        NewItems ReadOnlyCollection<Int32> (0 item) {}
                        OldItems ReadOnlyCollection<Int32> (1 item) {2}

    CollectionChanges → Action Reset
                        NewItems ReadOnlyCollection<Int32> (0 item) {}
                        OldItems ReadOnlyCollection<Int32> (0 item) {}

I haven't kept the `NewStartingIndex` and `OldStartingIndex` properties on the new class as I have yet to have a need for them.
Feel free to add them if your requirements dictate obviously.

Here are samples of the implementation as an extension method for both `ObservableCollection<T>` and `ReadOnlyObservableCollection<T>`:

```csharp
public static IObservable<CollectionChangedData<TItem>> CollectionChanges<TItem>(this ObservableCollection<TItem> collection)
{
    return CollectionChangesImp<ObservableCollection<TItem>, TItem>(collection);
}

public static IObservable<CollectionChangedData<TItem>> CollectionChanges<TItem>(
 this ReadOnlyObservableCollection<TItem> collection)
{
    return CollectionChangesImp<ReadOnlyObservableCollection<TItem>, TItem>(collection);
}

private static IObservable<CollectionChangedData<TItem>> CollectionChangesImp<TCollection, TItem>(
    TCollection collection)
    where TCollection : IList<TItem>, INotifyCollectionChanged
{
    return Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
            h => collection.CollectionChanged += h,
            h => collection.CollectionChanged -= h)
        .Select(e => new CollectionChangedData<TItem>(e.EventArgs));
}
```


##Collection's Items change notification
An interesting progression from just getting notifications when a collection changes, is to get notified when a property on an item in that collection changes.
For example if we have a collection of `Person` objects, we may want to be notified if one of those objects had their `Name` property changed.
For this we can leverage the patterns we had in the [Property Changed sample](PropertyChanged.md).

```csharp
public static IObservable<CollectionChangedData<TItem>> CollectionItemsChange<TItem, TProperty>(
    this ObservableCollection<TItem> collection,
    Expression<Func<TItem, TProperty>> property)
    where TItem : INotifyPropertyChanged
{
    var propertyName = property.GetPropertyInfo().Name;
    return CollectionItemsChange<ObservableCollection<TItem>, TItem>(collection, propName => propName == propertyName);
}

private static IObservable<CollectionChangedData<TItem>> CollectionItemsChange<TCollection, TItem>(
    TCollection collection,
    Predicate<string> isPropertyNameRelevant)
    where TCollection : IList<TItem>, INotifyCollectionChanged
{
    return Observable.Create<CollectionChangedData<TItem>>(
        o =>
        {
            var trackedItems = new List<INotifyPropertyChanged>();
            PropertyChangedEventHandler onItemChanged =
                (sender, e) =>
                {
                    if (isPropertyNameRelevant(e.PropertyName))
                    {
                        var payload = new CollectionChangedData<TItem>((TItem)sender);
                        o.OnNext(payload);
                    }
                };

            Action<IEnumerable<TItem>> registerItemChangeHandlers =
                items =>
                {
                    foreach (var notifier in items.OfType<INotifyPropertyChanged>())
                    {
                        trackedItems.Add(notifier);
                        notifier.PropertyChanged += onItemChanged;
                    }
                };

            Action<IEnumerable<TItem>> unRegisterItemChangeHandlers =
                items =>
                {
                    foreach (var notifier in items.OfType<INotifyPropertyChanged>())
                    {
                        notifier.PropertyChanged -= onItemChanged;
                        trackedItems.Remove(notifier);
                    }
                };

            registerItemChangeHandlers(collection);

            var collChanged = Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                h => collection.CollectionChanged += h,
                h => collection.CollectionChanged -= h);

            return collChanged
                .Finally(() => unRegisterItemChangeHandlers(collection))
                .Select(e => e.EventArgs)
                .Subscribe(
                    e => {
                        if (e.Action == NotifyCollectionChangedAction.Reset)
                        {
                            foreach (var notifier in trackedItems)
                            {
                                notifier.PropertyChanged -= onItemChanged;
                            }

                            var payload = new CollectionChangedData<TItem>(trackedItems, collection);
                            trackedItems.Clear();
                            registerItemChangeHandlers(collection);
                            o.OnNext(payload);
                        }
                        else
                        {
                            var payload = new CollectionChangedData<TItem>(e);
                            unRegisterItemChangeHandlers(payload.OldItems);
                            registerItemChangeHandlers(payload.NewItems);
                            o.OnNext(payload);
                        }
                });
        });
}
```

Now we can add, remove and modify items in a collection and get notified about it.

```csharp
var people = new ObservableCollection<Person>();
people.CollectionItemsChange(p=>p.Name)
      .SelectMany(changes=>changes.NewItems)
      .Select(person=>person.Name)
      .Dump("CollectionItemsChange");
people.Add(new Person(){Name="John"});
people.Add(new Person(){Name="Jack"});
people.Add(new Person(){Name="Jack"});

people[0].Name = "Jon";
```

Output:

    CollectionItemsChange →John
    CollectionItemsChange →Jack
    CollectionItemsChange →Jack
    CollectionItemsChange →Jon

The full [LinqPad](http://www.linqpad.net) sample in available as [ObservableCollectionSample.linq](ObservableCollectionSample.linq)
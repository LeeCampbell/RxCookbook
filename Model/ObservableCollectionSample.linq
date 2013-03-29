<Query Kind="Program">
  <Reference>&lt;RuntimeDirectory&gt;\System.Runtime.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.Threading.Tasks.dll</Reference>
  <NuGetReference>Rx-Main</NuGetReference>
  <Namespace>System</Namespace>
  <Namespace>System.Collections.Generic</Namespace>
  <Namespace>System.Collections.ObjectModel</Namespace>
  <Namespace>System.Collections.Specialized</Namespace>
  <Namespace>System.ComponentModel</Namespace>
  <Namespace>System.Linq</Namespace>
  <Namespace>System.Linq.Expressions</Namespace>
  <Namespace>System.Reactive.Disposables</Namespace>
  <Namespace>System.Reactive.Linq</Namespace>
  <Namespace>System.Reflection</Namespace>
</Query>

void Main()
{
	var source = new ObservableCollection<int>();
//	source.CollectionChanged+=(s,e)=> { e.Action.Dump("CollectionChanged");	};

//	source.CollectionChanged+=(s,e)=>
//	{
//		if(e.Action == NotifyCollectionChangedAction.Add)
//		{
//			e.NewStartingIndex.Dump("CollectionChanged-NewStartingIndex");
//			e.NewItems[0].Dump("CollectionChanged-NewItems[0]");
//		}
//	};
	
	var subscription = source.WhenCollectionChanges().Dump("WhenCollectionChanges");
	
	source.Add(1);
	source.Add(2);
	source.Add(3);
	
	source.Remove(2);
	source.Clear();
}

// Define other methods and classes here
//TODO: Allow the ability to provide multiple properties to WhenPropertyChanges
//TODO: Allow the ability to filter which properties notify on change for WhenCollectionItemsChange (ie as above todo).
public static class NotificationExtensions
{
   /// <summary>
   /// Executes an action when <paramref name="source"/> raises <seealso cref="INotifyPropertyChanged.PropertyChanged"/>.
   /// </summary>
   /// <typeparam name="T">The type of the source object. Type must implement <seealso cref="INotifyPropertyChanged"/>.</typeparam>
   /// <param name="source">The object to observe property changes on.</param>
   /// <param name="onPropertyChange">The action to perform when a property change event is raised.</param>
   /// <returns>Returns a token to allow consumer to stop observing.</returns>
   public static IDisposable WhenAnyPropertyChanges<T>(this T source, Action<T> onPropertyChange)
       where T : INotifyPropertyChanged
   {
       PropertyChangedEventHandler handler = (sender, e) => onPropertyChange((T)sender);
       source.PropertyChanged += handler;
       return Disposable.Create(() => source.PropertyChanged -= handler);
   }

   /// <summary>
   /// Executes an action when <paramref name="source"/> raises <seealso cref="INotifyPropertyChanged.PropertyChanged"/> for the given property.
   /// </summary>
   /// <typeparam name="T">The type of the source object. Type must implement <seealso cref="INotifyPropertyChanged"/>.</typeparam>
   /// <typeparam name="TProperty">The type of the property that is being observed.</typeparam>
   /// <param name="source">The object to observe property changes on.</param>
   /// <param name="property">An expression that describes which property to observe.</param>
   /// <param name="onPropertyChange">The action to execute when the property changes. The new value of the property is provided as the parameter.</param>
   /// <returns>Returns a token to allow consumer to stop observing.</returns>
   public static IDisposable WhenPropertyChanges<T, TProperty>(this T source, Expression<Func<T, TProperty>> property, Action<TProperty> onPropertyChange)
       where T : INotifyPropertyChanged
   {
       var propertyName = property.GetPropertyInfo().Name;
       var propertySelector = property.Compile();

       PropertyChangedEventHandler handler =
           (sender, e) =>
           {
               if (e.PropertyName == propertyName)
               {
                   var newValue = propertySelector(source);
                   onPropertyChange(newValue);
               }
           };

       source.PropertyChanged += handler;
       return Disposable.Create(() => source.PropertyChanged -= handler);
   }

   public static IObservable<CollectionChangedData<TItem>> WhenCollectionChanges<TItem>(this ObservableCollection<TItem> collection)
   {
       return WhenCollectionChangesImp<ObservableCollection<TItem>,TItem>(collection);
   }

    public static IObservable<CollectionChangedData<TItem>> WhenCollectionChanges<TItem>(
      this ReadOnlyObservableCollection<TItem> collection)
    {
        return WhenCollectionChangesImp<ReadOnlyObservableCollection<TItem>, TItem>(collection);
    }

    private static IObservable<CollectionChangedData<TItem>> WhenCollectionChangesImp<TCollection, TItem>(
       TCollection collection)
          where TCollection : IList<TItem>, INotifyCollectionChanged
    {
//        NotifyCollectionChangedEventHandler onCollectionChanged = (sender, e) =>
//        {
//            var payload = new CollectionChangedData<TItem>(e);
//            onPropertyChange(payload);
//        };
//        collection.CollectionChanged += onCollectionChanged;
//        return Disposable.Create(() => { collection.CollectionChanged -= onCollectionChanged; });

		return Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
				h=>collection.CollectionChanged+=h,
				h=>collection.CollectionChanged-=h)
			.Select(e=>new CollectionChangedData<TItem>(e.EventArgs));
			

    }


   //TODO: Allow the ability to push which property changed on underlying Item. (string.Empty for entire object)
   //TODO: Make Rx. Should allow filter by PropName (which would still push on string.Empty?)
   /// <summary>
   /// Executes an action when a collection or its items raise a change notification event.
   /// </summary>
   /// <typeparam name="TItem">The type of the collection items</typeparam>
   /// <param name="collection">The collection to observe.</param>
   /// <param name="onPropertyChange">The action to perform when the collection or its items change.</param>
   /// <returns>Returns a token to allow consumer to stop observing.</returns>
   public static IObservable<CollectionChangedData<TItem>> WhenCollectionItemsChange<TItem>(
       this ObservableCollection<TItem> collection)
   {
       return WhenItemsPropertyChange<ObservableCollection<TItem>, TItem>(collection, _ => true);
   }

   public static IObservable<CollectionChangedData<TItem>> WhenItemsPropertyChange<TItem, TProperty>(
      this ObservableCollection<TItem> collection,
      Expression<Func<TItem, TProperty>> property)
       where TItem : INotifyPropertyChanged
   {
       var propertyName = property.GetPropertyInfo().Name;
       return WhenItemsPropertyChange<ObservableCollection<TItem>, TItem>(collection, propName => propName == propertyName);
   }

   public static IObservable<CollectionChangedData<TItem>> WhenItemsPropertyChange<TItem, TProperty>(
      this ReadOnlyObservableCollection<TItem> collection,
      Expression<Func<TItem, TProperty>> property)
       where TItem : INotifyPropertyChanged
   {
       var propertyName = property.GetPropertyInfo().Name;
       return WhenItemsPropertyChange<ReadOnlyObservableCollection<TItem>, TItem>(collection, propName => propName == propertyName);
   }

   private static IObservable<CollectionChangedData<TItem>> WhenItemsPropertyChange<TCollection, TItem>(
      TCollection collection,
      Predicate<string> isPropertyNameRelevant)
         where TCollection : IList<TItem>, INotifyCollectionChanged
   {
   	return Observable.Create<CollectionChangedData<TItem>>(
		o=>{
   
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
			NotifyCollectionChangedEventHandler onCollectionChanged =
				(sender, e) =>
				{
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
				};
		
			registerItemChangeHandlers(collection);
			collection.CollectionChanged += onCollectionChanged;
		
			return Disposable.Create(
				() =>
				{
					collection.CollectionChanged -= onCollectionChanged;
					unRegisterItemChangeHandlers(collection);
				});
		});
   }
}

public sealed class NotifyEventComparer : IEqualityComparer<PropertyChangedEventArgs>
{
   public static readonly NotifyEventComparer Instance = new NotifyEventComparer();

   bool IEqualityComparer<PropertyChangedEventArgs>.Equals(PropertyChangedEventArgs x, PropertyChangedEventArgs y)
   {
       return x.PropertyName == y.PropertyName;
   }

   int IEqualityComparer<PropertyChangedEventArgs>.GetHashCode(PropertyChangedEventArgs obj)
   {
       return obj.PropertyName.GetHashCode();
   }
}

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

       _action = _newItems.Count==0 
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

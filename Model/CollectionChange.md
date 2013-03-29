#Collection changes

The .NET framework provides a standard interface for publishing changes to a collection. 
As in the [Observing Property changes sample](PropertyChange.md), the `INotifyCollectionChanged` interface can be found in the _System.dll_, so is available to all .NET software not just GUI applications.


	namespace System.Collections.Specialized
	{
	  public interface INotifyCollectionChanged
	  {
	    event NotifyCollectionChangedEventHandler CollectionChanged;
	  }
	
	  public delegate void NotifyCollectionChangedEventHandler(object sender, NotifyCollectionChangedEventArgs e);	
	
	  public class NotifyCollectionChangedEventArgs : EventArgs
	  {
	  	//Implementations removed...
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

The two common implementations of this interface are the `ObservableCollection<T>` and `ReadOnlyObservableCollection<T>` which can be found in _WindowsBase.dll_ in .NET 3.0, 3.5 & 4.0 and in _System.ObjectModel.dll_ for the newer Portable libraries (both in `System.Collections.ObjectModel`).
Here is an example usage of the `ObservableCollection<T>`.

	var source = new ObservableCollection<int>();
	source.CollectionChanged+=(s,e)=>{e.Action.Dump("CollectionChanged");};
	
	source.Add(1);
	source.Add(2);
	source.Add(3);
	
	source.Remove(2);
	source.Clear();

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

	var source = new ObservableCollection<int>();
	source.CollectionChanged+=(s,e)=>
	{
		e.NewStartingIndex.Dump("CollectionChanged-NewStartingIndex");
		e.NewItems[0].Dump("CollectionChanged-NewItems[0]");
	};
	
	source.Add(1);
	source.Add(2);

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

	Observable.FromEventPattern<NotifyCollectionChangedEventHandler,NotifyCollectionChangedEventArgs>(
			h=>source.CollectionChanged+=h,
			h=>source.CollectionChanged-=h)

However this would require us to check the value of the `NewItems` and `OldItems` for null each time, or risk incurring a `NullReferenceException`.
I prefer to project the `NotifyCollectionChangedEventArgs` type into a custom type that removes the `NullReferenceException` risk for me.

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



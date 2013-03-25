#Observing Property changes

.NET offers serveral ways to propagate property changes from an Object model. 
The most common are using the `INotifyPropertyChanged` interface, DependencyProperties in WPF, or, using the convention of having a property with a matching property changed event.
Often property change events are used in GUI applications to let the UI know when to automatically redraw.
We can however leverage property change patterns outside of GUI software for what ever reasons we see fit.

##INotifyPropertyChanged
Propbably the most common way to expose property changes is by implementing the `System.ComponentModel.INotifyPropertyChanged` interface.
This interface simply exposes a `PropertyChanged` event that will propvide access to a string representing the name of the property that changed.
As this interface can be found in the `System.dll` it is available for all .NET code from .NET 2.0 up.

First we can start with an exmple that implements `INotifyPropertyChanged`.


    public class Person : INotifyPropertyChanged
    {
        string _name;
        int _age;
        
        public string Name
        {
            get { return _name; }
            set 
            { 
                if(_name !=value)
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
                if(_age !=value)
                {
                    _age = value; 
                    OnPropertyChanged("Age");
                }
            }
        }
        
        #region INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }

To get property change events we simply register an event handler.

    var Dave = new Person();
    Dave.PropertyChanged+=(s,e)=>{e.PropertyName.Dump();};
    Dave.Name = "Dave";
    Dave.Age = 21;

Ouput:

    Name
    Age

This is only somewhat useful as I imagine that you probably want to get the new value of the property.
This first extension method allows you to get the entire object when it raise the `PropertyChanged` event.

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

This now allows us to use the `OnAnyPropertyChanges` to get access to the person object when ever a property changes.

    var Dave = new Person();
    //Dave.PropertyChanged+=(s,e)=>{e.PropertyName.Dump();};
    Dave.OnAnyPropertyChanges().Dump("OnAnyPropertyChanges");
    Dave.Name = "Dave";
    Dave.Age = 21;

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

Now we can extend the `NotificationExtensions` class to have a method to get a sequence of property values as it the property changes.

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
                                .Where(e=>e.EventArgs.PropertyName==propertyName)
                                .Select(e=>propertySelector(source))
                                .Subscribe(o);
                });
        }
    }
    
The new method can be used to get just the value of the `Name` property when it changes.

    var Dave = new Person();
    //Dave.PropertyChanged+=(s,e)=>{e.PropertyName.Dump("PropertyChanged");};
    //Dave.OnAnyPropertyChanges().Dump("OnAnyPropertyChanges");
    Dave.OnPropertyChanges(d=>d.Name).Dump("OnPropertyChanges");
    Dave.Name = "Dave";
    Dave.Age = 21;

Output:

    OnPropertyChanges →Dave

The full [LinqPad](http://www.linqpad.net) sample in available as [INotifyPropertyChangedSample.linq](INotifyPropertyChangedSample.linq)

##DependencyProperties
In WPF (and derivative frameworks like Silverlight, Windows Phone, Windows Store Apps...), `DependencyObjects` and their `DependencyProperty` members make up a key part of the standarized framework for bring together features for data-binding, animation, styles, property value inheritence and property change notification.
Here we will focus on how to get a property changed sequence from a `DependencyProperty`.

A conventional way to get access to change notification for any `DependencyProperty` is to get access to a `DependencyPropertyDescriptor` for the `DependencyProperty` and then use the `AddValueChanged` method to attach a callback delegate.

In this sample we create a dummy class that exposes a `Title` `DepenedencyProperty`.

	public class MyControl : DependencyObject
	{
		#region Title DependencyProperty
		
		public string Title
		{
		get { return (string)GetValue(TitleProperty); }
		set { SetValue(TitleProperty, value); }
		}
		
		public static readonly DependencyProperty TitleProperty =
		DependencyProperty.Register("Title", typeof(string), typeof(MyControl),
								new PropertyMetadata());
		
		#endregion
	}

Here we use a `DependencyPropertyDescriptor` to attach a callback when the `Title` property changes.

	var myControl = new MyControl();
	
	var dpd = DependencyPropertyDescriptor.FromProperty(MyControl.TitleProperty, typeof(MyControl));
	EventHandler handler = delegate { myControl.Title.Dump("Title"); };
	dpd.AddValueChanged(myControl, handler);
	
	myControl.Title = "New Title";

Output:

> **Title**  
> New Title 

We can use this as the basis to create an observable sequence.
In our example we want to provide clean up by removing the callback.
We use `Observable.Create` to get a `DependencyPropertyDescriptor` when the consumer subscribes to the sequence.
We then push the the new values each time the callback is called.


	public static class DependencyObjectExtensions
	{
		public static IObservable<T> OnPropertyChanges<T>(this DependencyObject source, DependencyProperty property)
		{
			return Observable.Create<T>(o=>{
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

This can be used by specifying the `DependencyProperty` and its type as the generic type parameter. 

	var myControl = new MyControl();
		
	myControl.OnPropertyChanges<string>(MyControl.TitleProperty).Dump("OnPropertyChanges");
	
	myControl.Title = "New Title";

Perhaps an improvement to this would be to get automatic type resolution by using the expression we had in the INotifyPropertyChanged sample.
We can add this by using the `FromName` instead of the `FromProperty` static method.

	public static IObservable<TProperty> OnPropertyChanges<T, TProperty>(this T source, Expression<Func<T, TProperty>> property)
		where T : DependencyObject 
	{
		return Observable.Create<TProperty>(o=>{
			var propertyName = property.GetPropertyInfo().Name;
		    var dpd =DependencyPropertyDescriptor.FromName(propertyName, typeof(T), typeof(T));
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

We can now get access to the observable sequence of property changes without requiring us to specify the type of the property.

	var myControl = new MyControl();
	
	//myControl.OnPropertyChanges<string>(MyControl.TitleProperty).Dump("OnPropertyChanges");
	myControl.OnPropertyChanges(c=>c.Title).Dump("OnPropertyChanges");	
	
	myControl.Title = "New Title";

Output:

	OnPropertyChanges →New Title



##PropertyChanged Convention

##Library implementations
###Rxx
###ReactiveUI

##More links
Allan Lindqvist's post [Observable from any property in a INotifyPropertyChanged class](http://social.msdn.microsoft.com/Forums/en-US/rx/thread/36bf6ecb-70ea-4be3-aa35-b9a9cbc9a078) on the Rx MSDN Forums.
Richard Szalay's post [GetPropertyValues: a strongly typed reactive wrapper around INotifyPropertyChanged](http://social.msdn.microsoft.com/Forums/en-US/rx/thread/2fc8ab3c-28ed-45a9-a96f-59133a3d103c) on the Rx MSDN Forums.


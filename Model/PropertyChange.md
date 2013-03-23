#Observing Property changes

.NET offers serveral ways to propagate property changes from an Object model. 
The most common are using the `INotifyPropertyChanged` interface, DependencyProperties in WPF, or, using the convention of having a property with a matching property changed event.

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
    Dave.OnAnyPropertyChanges().Dump("WhenAnyPropertyChanges");
    Dave.Name = "Dave";
    Dave.Age = 21;

Output:

    WhenAnyPropertyChanges →Person
        UserQuery+Person 
        Name Dave 
        Age 0 
    WhenAnyPropertyChanges →Person
        UserQuery+Person 
        Name Dave 
        Age 21 



##DependencyProperties

##PropertyChanged Convention

##Library implementations
###Rxx
###ReactiveUI

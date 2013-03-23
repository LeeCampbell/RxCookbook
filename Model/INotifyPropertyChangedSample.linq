<Query Kind="Program">
  <Reference>&lt;RuntimeDirectory&gt;\System.Runtime.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.Threading.Tasks.dll</Reference>
  <NuGetReference>Rx-Main</NuGetReference>
  <Namespace>System.ComponentModel</Namespace>
  <Namespace>System.Reactive.Disposables</Namespace>
  <Namespace>System.Reactive.Linq</Namespace>
</Query>

void Main()
{
    var Dave = new Person();
    Dave.PropertyChanged+=(s,e)=>{e.PropertyName.Dump("PropertyChanged");};
    Dave.OnAnyPropertyChanges().Dump("OnAnyPropertyChanges");
	Dave.OnPropertyChanges(d=>d.Name).Dump("OnPropertyChanges");
    Dave.Name = "Dave";
    Dave.Age = 21;
}

// Define other methods and classes here

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
<Query Kind="Program">
  <Reference>&lt;RuntimeDirectory&gt;\System.Runtime.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.Threading.Tasks.dll</Reference>
  <NuGetReference>Rx-Main</NuGetReference>
  <Namespace>System.ComponentModel</Namespace>
  <Namespace>System.Reactive.Linq</Namespace>
  <Namespace>System.Reactive.Disposables</Namespace>
</Query>

void Main()
{
	var dave = new Person();
	//dave.NameChanged+=(s,e)=>{dave.Name.Dump("NameChanged");};
	
//	var nameProp =TypeDescriptor.GetProperties(dave)
//						.Cast<PropertyDescriptor>()
//						.Where (pd => pd.Name=="Name")
//						.SingleOrDefault();
//
//	EventHandler handler = delegate { dave.Name.Dump("Name"); };
//	nameProp.AddValueChanged(dave, handler);
	
	dave.OnPropertyChanges(d=>d.Name).Dump("OnPropertyChanges");
	dave.Name = "Dave";
	
}

// Define other methods and classes here
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
		if(handler!=null)handler(this, EventArgs.Empty);
	}
}


public static class ObjectExtensions
{
	public static IObservable<TProperty> OnPropertyChanges<T, TProperty>(this T source, Expression<Func<T, TProperty>> property)
	{
		return Observable.Create<TProperty>(o=>{
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
<Query Kind="Program">
  <Reference>&lt;RuntimeDirectory&gt;\Accessibility.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.Runtime.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.Security.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.Threading.Tasks.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.Xaml.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\WPF\WindowsBase.dll</Reference>
  <NuGetReference>Rx-Main</NuGetReference>
  <Namespace>System.ComponentModel</Namespace>
  <Namespace>System.Reactive.Disposables</Namespace>
  <Namespace>System.Reactive.Linq</Namespace>
  <Namespace>System.Windows</Namespace>
</Query>

void Main()
{
	var myControl = new MyControl();
	
//	var dpd = DependencyPropertyDescriptor.FromProperty(MyControl.TitleProperty, typeof(MyControl));
//	EventHandler handler = delegate { myControl.Title.Dump("Title"); };
//	dpd.AddValueChanged(myControl, handler);
	
	//myControl.OnPropertyChanges<string>(MyControl.TitleProperty).Dump("OnPropertyChanges");
	myControl.OnPropertyChanges(c=>c.Title).Dump("OnPropertyChanges");	
	
	myControl.Title = "New Title";
}

// Define other methods and classes here

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
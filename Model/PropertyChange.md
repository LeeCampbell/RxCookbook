#Observing Property changes

.NET offers serveral ways to propagate property changes from an Object model. 
The most common are using the `INotifyPropertyChanged` interface, DependencyProperties in WPF, or, using the convention of having a property with a matching property changed event.

##INotifyPropertyChanged
Propbably the most common way to expose property changes is by implementing the `System.ComponentModel.INotifyPropertyChanged` interface.
This interface simply exposes a `PropertyChanged` event that will propvide access to a string representing the name of the property that changed.
As this interface can be found in the `System.dll` it is available for all .NET code from .NET 2.0 up.


##DependencyProperties

##PropertyChanged Convention

##Library implementations
###Rxx
###ReactiveUI

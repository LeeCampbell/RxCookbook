RxCookbook
==========

Collection of recipes and snippets helping you create useful Rx code

## Rx in your Model
See how you can use Rx in your regular domain.

[Property Change notifications](Model/PropertyChange.md)  
[Collection Change notifications](Model/CollectionChange.md)


## Rx for Instrumentation
Use Rx to help instrument your code, or analyse instrumented data from other systems.

[Logging](Instrumentation/Logging.md)

## Rx in your repositories
There are common patterns that I see occurring in the repository "layer" in reactive applications.

[Polling with Rx](Repository/Polling.md)  
[Lazy Connect](Repository/LazyConnect.md)  

## Rx with IO
Here we look at different ways to use Rx with various forms in Input/Output.

### Disk
Rx can help stream data to and from the disk.
I have found that it can greatly reduce complexity while also delivering impressive performance benefits.

[Rx Disk IO](IO/Disk/ReadMe.md)

### Communications
Rx can provide a useful abstraction over numerous communications layers.
Here we look at various implementations for different technologies.


[TIBCO Rendevous](IO/Comms/TibRv.md)

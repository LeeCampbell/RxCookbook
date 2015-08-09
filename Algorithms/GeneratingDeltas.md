#Generating Delta value

If you are looking to enrich your data from an observable sequence with delta values then this can be done quite easily with Rx.

This kind of algorithm is used for getting changes in data eg.

 * Changes in price
 * Changes in temperature
 * Changes in location
 
To generate a delta value we just need to be able to access the previous value and compare it to the current value. 

    delta = currentValue - previousValue

There are several ways we can do this. Here we will cover using overlapping windows with `Buffer`, a running accumulator with `Scan` or merging the current sequence with a copy of sequence delayed by 1 using `Publish`, `Zip` and `Skip`. 

##Buffer
A very simple option is to use the overload of the `Buffer` that allows you to specify the buffer size _and_ the size of the stride to take.
Normally you would pass just an integer value or a timespan to the `Buffer` operator.
In these overload, the values in each produced buffer do not overlap i.e. for `Buffer(5)` you would get the first values values produced as single `IList<T>` and then once the tenth value was produced from the source, the buffer would give the next 5 values in another `IList<T>`.
However if you use the overload where you provide the stride, you effectively specify how large the buffer should be and when to create the next buffer.
The example above could be recreated by specifying the same value for the buffer and the stride e.g. `Buffer(5,5)`.
For our requirements, we can create overlapping windows e.g. a buffer of the first two values, then the second buffer would contain the second value again and then the third value.
To create this we can specify a buffer size of 2 and a stride length of 1 i.e. `Buffer(2,1)`

    var source = GetObservableSequence();
    var deltas = source.Buffer(2,1)
		.Select(buffer=>buffer[1]-buffer[0]);

Note that this will not produce a value until at least two values have been produced.
This can be remedied by using the `StartWith` to provide a seed value.

##Scan

An alternate solution is to use the `Scan` operator.
The `Scan` operator is often used for producing running aggregate values e.g. running totals.
In this case we will use the accumulator (`acc`) to just store the last value.

    var source = GetObservableSequence();
    var deltas = source.Scan(new{Prev=0, Delta=0},(acc, curr)=>new{Prev=curr, Delta=curr-acc.Prev})
        .Select(x=>x.Delta);
 
This is arguably more complex solution than the `Buffer` option.   
This however does not suffer the same limitation as the `Buffer` algorithm, and can yield values as soon as the first value from the source is produced (without the need for `StartWith`).


##ZipSkip

The _ZipSkip_ algorithm is popular however it is more difficult to learn, and requires more effort to share subscriptions.
Effectively the _ZipSkip_ algorithm will `Zip` the source sequence with the source sequence again but skipping the first value.
This will mean that the `resultSelector` of the `Zip` operator will be provided a value and the previous value.
It also means that we can only get a delta once we have 2 values produced.

    var previousValues = GetObservableSequence();
    var currentValues = previousValues.Skip(1);
    var deltas = previousValues.Zip(currentvalues, (prev, curr)=>curr-prev);
    
The issue with the above solution is that we are assuming that there is no cost or side effect to making a subscription to our source.
To put it another way, we are assuming the sequence is *Hot*.
If it is not safe to make this assumption then we also need to share the subscription cost, probably via the `Publish()` operator.

    var source = GetObservableSequence().Publish();
    var previousValues = source;
    var currentValues = previousValues.Skip(1);
    var deltas = previousValues.Zip(currentvalues, (prev, curr)=>curr-prev)
    
Now we have the extra responsibility of having to connect the published sequence, and then dispose that connection appropriately.
We could use `RefCount()` however that could introduce a race condition, that would be mitigated by manually connecting after subscription.

If we want to ensure that this algorithm does yield a value when the source sequence yields its first value, then we can add a `StartsWith` operator.

    var source = GetObservableSequence()
        .StartsWith(0)
        .Publish();
    var previousValues = source;
    var currentValues = previousValues.Skip(1);
    var deltas = previousValues.Zip(currentvalues, (prev, curr)=>curr-prev)










    

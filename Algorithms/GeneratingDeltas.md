#Generating Delta value

If you are looking to enrich your data from an observable sequence with delta values then this can be done quite easily with Rx.

This kind of algorithm is used for getting changes in data eg.

 * Changes in price
 * Changes in temerature
 * Changes in location
 
To generate a delta value we just need to be able to access the previous value and compare it to the current value. 

    delta = currentValue - previousValue

There are two ways we can do this; _ZipSkip_ or _Scan_

##ZipSkip

The _ZipSkip_ method is popular however it is more difficult to learn, and requires more effort to share subscriptions.
Effectively the _ZipSkip_ algorithm will `Zip` the source sequence with the source sequence again but skipping the first value.
This will mean that the `resultSelector` of the `Zip` operator will be provided a value and the previous value.
It also means that we can only get a delta once we have 2 values produced.

    var previousValues = GetObservableSequence();
    var currentValues = previousValues.Skip(1);
    var deltas = previousValues.Zip(currentvalues, (prev, curr)=>curr-prev);
    
The issue with the value above solution is that we are assuming that there is no cost or side effect to making a subscription to our source.
To put it another way, we are assuming the sequence is *Hot*.
If it is not safe to make this assumption then we also need to share the subscription cost, probably via the `Publish()` operator.

    var source = GetObservableSequence().Publish();
    var previousValues = source;
    var currentValues = previousValues.Skip(1);
    var deltas = previousValues.Zip(currentvalues, (prev, curr)=>curr-prev)
    
Now we have the extra responsibilty of having to connect the published sequence, and the duty to dispose of that connection too.
We could use `RefCount()` however that could introduce a race condition, that would be manually mitigated by manually connecting after subscription.

If we want to ensure that this algorithm does yeild a value when the source sequence yileds its first value, then we can add a `StartsWith` operator.


    var source = GetObservableSequence().StartsWith(0).Publish();
    var previousValues = source;
    var currentValues = previousValues.Skip(1);
    var deltas = previousValues.Zip(currentvalues, (prev, curr)=>curr-prev)


##Scan

An alternate solution, which is arguably more simple, is to use the Scan operator.
The Scan operator is often used for producing running aggregate values e.g. Running Totals.
In this case we will instead use the accumulator value to just store the last value.


    var source = GetObservableSequence();
    var deltas = source.Scan(new{Prev=0, Delta=0},(acc, curr)=>new{Prev=curr, Delta=curr-acc.Prev})
        .Select(x=>x.Delta);
    
Here you can see that this is a much simpler implementation that the _ZipSkip_ algorithm.
This however does not suffer the same limitation as the _ZipSkip_ algorithm, and can yeild values as soon as the first value from the source is produced.



















    

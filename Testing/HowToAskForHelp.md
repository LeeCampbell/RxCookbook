# Getting help from the community

At time of writing [IntroToRx.com](IntroToRx.com) the best place to get quality timely help on your Rx problems was to go to the [Microsoft Rx Forums](https://social.msdn.microsoft.com/Forums/en-US/home?forum=rx).
Since then, [Stackoverflow.com](stackoverflow.com/questions/tagged/system.Reactive) has eclipsed it as the resource of choice.
Other places of help have also popped up including the [Gitter chat room](gitter.im/Reactive-Extensions/Rx.NET) for the Rx.NET repository.

## Better questions get better answers
Regardless of where you go to for help, you will find a higher quality if you can provide a high quality question.
Stackoverflow.com provide good guidance on how to create a [Minimal Complete Verfiable Example](http://stackoverflow.com/help/mcve).
Some people however will struggle to do this when learning a new tool e.g. a new language, IDE, library or framework.

## Clarity of thought
It appears that most people struggling with asking a question fall into one of two categories:

 1. Unclear requirements
 2. XY problem see:  [http://xyproblem.info](xyproblem.info) and [http://meta.stackexchange.com/questions/66377/what-is-the-xy-problem](http://meta.stackexchange.com/questions/66377/what-is-the-xy-problem)

Unclear requirements can either be a function of the questioner not having fully formed thoughts about the problem space, or the questioner failing to communicate these thoughts completely or accurately.

The XY problem is also quite common when someone attempts to transfer their skills from one domain to another domain.
I see this quote often when a questioner tries to apply the way they have worked with `IEnumerable<T>` to `IObservable<T>`.
A questioner may be used to imperatively working with the `List<T>` type, and then attempts to transfer this style to their use of Rx.
For example they may be used to calling `Add(T)` and then returning the `List<T>` instance from a method.
It is then tempting to believe that a substitution of `Subject<T>.OnNext(T)` for `List<T>.Add(T)` would be appropriate.
The uncomfortable cognitive dissonance that ensues can be a common place of friction for new users of Rx, especially coming from a non-functional background.

## Visualizing the problem
Time and again I have found that the most effective way to understand your Rx problem is to visualize your problem.
The visualization technique most common with Rx practitioner is to create a Marble Diagram.

When dealing with sequences of data in space (i.e. an array or a `List<T>`) we can simply express the data as it is in ordered


```
[1,2,3]
```

With an Observable Sequence (i.e. data in time) time is a critical concept.
We need to be able to express the values in the sequence in their relative timings.
An Observable Sequence can also have the concept of completion and error.
A Marble diagram is a simple way of communicating these sequence of values over time.

In ASCII art, the sequence is represented as notifications produced over time from left to right.
For example a sequence of value spaced 3 seconds apart could be presented as

```
---1---2---3---
```

As completion and errors are also features of an Observable Sequence, then we introduce symbols to represent these too.
Most commonly `x` is used to denote and `OnError` and `|` for `OnCompleted`.

```
---1---2---3---x
```

or

```
---1---2---3---|
```

### Composition of Marble diagrams
Marble diagrams are most useful when use to show how composition of sequences or operators behave.
This simple example shows how the `Concat` operator might be presented:

```
s1   --1--2--3--|
s2              --A--B--C--|

r    --1--2--3----A--B--C--|
```
Where `s1` and `s2` are the source sequences and `r` represents the result of the composition.

This example shows the effect of a where filter that removes odd numbers.

```
s1   --1--2--3--4--5--6--7--|

r    -----2-----4-----6-----|
```


The [http://rxmarbles.com/](http://rxmarbles.com/) site show numerous interactive models of various Rx operators.

## Creating an MCVE

## References
Examples of Questions and answers using Marble diagrams:
 * [http://stackoverflow.com/questions/40235683/how-to-have-events-separated-at-least-by-a-given-time-span/40252708#40252708](http://stackoverflow.com/questions/40235683/how-to-have-events-separated-at-least-by-a-given-time-span/40252708#40252708)
 * [http://stackoverflow.com/questions/35529284/reactive-approach-to-simple-imperative-task/35545218](http://stackoverflow.com/questions/35529284/reactive-approach-to-simple-imperative-task/35545218)

Examples of XY Problem
 * [http://stackoverflow.com/questions/39356211/use-reactive-extensions-to-filter-on-significant-changes-in-observable-stream/39359821#39359821](http://stackoverflow.com/questions/39356211/use-reactive-extensions-to-filter-on-significant-changes-in-observable-stream/39359821#39359821)

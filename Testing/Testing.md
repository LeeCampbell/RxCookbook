TODO: Complete this WIP (Work In Progress)
TODO: Update this to lead the reader into using the ReactiveTest.OnNext/OnError/OnCompleted methods.


##Instead of using `.Single()`/`.First()` in your unit tests.

When writing unit test that pass, `.Single()`/`.First()` can seem of little consequence. 
The problem comes when the tests fail. 
As Test-First practiioners would know, using these kind of operators can block and just leave your tests hanging.

Lets look at a simple example where we use a blocking operator in our tests.


    [Test]
    public void SillyTest()
    {
        var expected = 42;
        var source = new ReplaySubject<int>();
        
        source.OnNext(expected);
        source.OnCompleted();
        
        Assert.AreEqual(expected, source.Single());
    }

Here a simple change of removing the termination of the source will leave this test hanging!

    [Test]
    public void SillyTest()
    {
        var expected = 42;
        var source = new ReplaySubject<int>();
        
        source.OnNext(expected);
        //source.OnCompleted();
        
        Assert.AreEqual(expected, source.Single()); //This will hang on the .Single!
    }


Instead of using these blocking operators we can leverage the `TestableObserver` class.

Here is and example of a set of Exentions methods that I can use to avoid hanging tests;


    public static class TestableObservableEx
    {
        public static bool HasSubscriptions<T>(this ITestableObservable<T> source)
        {
            return source.Subscriptions.Any(s => s.Unsubscribe == long.MaxValue);
        }

        public static void AssertSingleValueOf<T>(this IObservable<T> source, T expected)
        {
            var observer = new TestScheduler().CreateObserver<T>();
            using (source.Subscribe(observer))
            {
                Assert.AreEqual(2, observer.Messages.Count, "Source is not a single value sequence.");
                Assert.AreEqual(NotificationKind.OnCompleted, observer.Messages[1].Value.Kind, "Only two message were expected : OnNext(expected) and OnCompleted.");
                Assert.AreEqual(expected, observer.Messages[0].Value.Value, "Value from sequence does not match expected.");
            }
        }
    }
    
These can be used like this


    [Test]
    public void SillyTest()
    {
        var expected = 42;
        var source = new ReplaySubject<int>();
        
        source.OnNext(expected);
        //source.OnCompleted();
        
        //Assert.AreEqual(expected, source.Single());
        source.AssertSingleValueOf(expected);
    }
    
Note, here the test would fail quickly and without having to set a Global timeout on our testing framework or build server.


If you still want to be able to safely get access to the single/first value from a sequence in a unit test, we could also create an extension method that safely yeilds that value when appropriate.


    public static T SafeSingle<T>(this IObservable<T> source)
    {
        Assert.IsNotNull(source);

        var observer = new TestScheduler().CreateObserver<T>();
        using (source.Subscribe(observer))
        {
            Assert.AreEqual(2, observer.Messages.Count, "Source is not a single value sequence.");
            Assert.AreEqual(NotificationKind.OnCompleted, observer.Messages[1].Value.Kind, "Only two message were expected : OnNext(expected) and OnCompleted.");
            return observer.Messages[0].Value.Value;
        }
    }

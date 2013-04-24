<Query Kind="Program">
  <Reference>D:\Externals\Microsoft\Reactive Extensions\System.Reactive.dll</Reference>
  <Reference>D:\Externals\Microsoft\Reactive Extensions\System.Reactive.Providers.dll</Reference>
  <Reference>D:\Externals\TibcoRv\TIBCO.Rendezvous.dll</Reference>
  <Reference>D:\Externals\TibcoRv\TIBCO.Rendezvous.netmodule</Reference>
  <Namespace>System.Reactive</Namespace>
  <Namespace>System.Reactive.Disposables</Namespace>
  <Namespace>System.Reactive.Linq</Namespace>
  <Namespace>System.Reactive.Subjects</Namespace>
  <Namespace>TIBCO.Rendezvous</Namespace>
</Query>

void Main()
{
  /*
  Add reference to System.Reactive.dll, System.Reactive.Providers.dll, TIBCO.Rendezvous.dll & TIBCO.Rendezvous.netmodule
  You may have to manually set the reference to the netmodule by editing the .linq file in notepad as LinqPad does not 
    see this as a valid assembly to reference (fair enough too!)
  e.g.
    <Reference>D:\Externals\TibcoRv\TIBCO.Rendezvous.dll</Reference>
    <Reference>D:\Externals\TibcoRv\TIBCO.Rendezvous.netmodule</Reference>
  Built against v8.2.2 (1.0.3688.17766 of TIBCO.Rendezvous.dll & 8,2,2,0 of tibrv.dll)
  */
  var service = "7500";
	var network = ";239.255.0.1";
	var daemon = "tcp:7500";
	
	var env = new TibcoEnvironment();
	var subjectListenerFactory = new SubjectListenerFactory(env, service, network, daemon);
	
	var myTestSubjectListener = subjectListenerFactory.CreateSubjectListener("TEST.Message");						 
	
	myTestSubjectListener.Dump("Test messages");

  Console.WriteLine("Stop the query when ready");
}

public interface ISubjectListenerFactory
{
	ISubjectListener CreateSubjectListener(string subject);
}
public interface ISubjectListener
{
	IObservable<TIBCO.Rendezvous.Message> ReceivedMessages();
}
public interface ITibcoEnvironment
{
	TIBCO.Rendezvous.Queue DispatcherQueue { get; }
	IDisposable Open();
}


public class SubjectListenerFactory : ISubjectListenerFactory
{
  private readonly ITibcoEnvironment _tibcoEnvironment;
  private readonly string _service;
  private readonly string _network;
  private readonly string _daemon;

  public SubjectListenerFactory(ITibcoEnvironment tibcoEnvironment, string service, string network, string daemon)
  {
      _tibcoEnvironment = tibcoEnvironment;
      _service = service;
      _network = network;
      _daemon = daemon;
  }

  public ISubjectListener CreateSubjectListener(string subject)
  {
      return new SubjectListener(_tibcoEnvironment, _service, _network, _daemon, subject);
  }
}

public sealed class SubjectListener : ISubjectListener
{
  private readonly IObservable<Message> _receivedMessages;

  public SubjectListener(ITibcoEnvironment tibcoEnvironment, string service, string network, string daemon, string subject)
  {
      _receivedMessages = Observable.Create<Message>(o =>
          {
              var resources = new CompositeDisposable();

              try
              {
                  var connection = tibcoEnvironment.Open();
                  resources.Add(connection);

                  //TODO: The tibEnv could return wrapped NetTransport. It would handle the refcount sharing of the Transport (if required) & opening of Env.
                  var transport = new NetTransport(service, network, daemon);
                  resources.Add(Disposable.Create(transport.Destroy));

                  var listener = new Listener(tibcoEnvironment.DispatcherQueue, transport, subject, null);
                  resources.Add(Disposable.Create(listener.Destroy));

                  var subscription = Observable.FromEventPattern<MessageReceivedEventHandler, MessageReceivedEventArgs>(
                      h => listener.MessageReceived += h,
                      h => listener.MessageReceived -= h)
                                               .Select(e => e.EventArgs.Message)
                                               .Subscribe(o);
                  resources.Add(subscription);
              }
              catch (Exception e)
              {
                  o.OnError(e);
              }

              return resources;
          })
          .Publish()
          .RefCount();

  }

  public IObservable<Message> ReceivedMessages()
  {
      return _receivedMessages;
  }
}

public sealed class TibcoEnvironment : ITibcoEnvironment
{
  private readonly object _lock = new object();
  private int _openCount;
  private Dispatcher _dispatcher;

  public TibcoEnvironment()
  {}

  public TIBCO.Rendezvous.Queue DispatcherQueue { get; private set; }

  public IDisposable Open()
  {
      lock (_lock)
      {
          if (_openCount == 0)
          {
              TIBCO.Rendezvous.Environment.Open();

              //We don't use the Queue.Default instance as you can not Destroy it (as it throws), fair enough as it is shared.
              // We can't construct this until the Environment is Open, thus it can not be readonly, thus we create it here.
              DispatcherQueue = new TIBCO.Rendezvous.Queue();
              _dispatcher = new Dispatcher(DispatcherQueue);
          }

          _openCount++;
      }
      return Disposable.Create(Close);
  }

  private void Close()
  {
      lock (_lock)
      {
          if (_openCount <= 0)
              throw new InvalidOperationException("Close was called with no open");

          _openCount--;

          if (_openCount > 0)
              return;


          //The order we do the next parts are important!
          //You cannot 'Destroy' the Dispatcher until you first destroy the queue and the close the Environment.
          // You would think that the order would be Queue, Dispatcher, Env to have symmetry with the creation steps
          // but you will get a ThreadAbortException if you try to do it out of order. Below are the result of various
          // orders of performing clean up (Q = _queue.Destroy(); E = Environment.Close(); D = _dispatcher.Destroy();)
          // QED (works)
          // QDE  ThreadAbortException
          // EQD  (works)
          // EDQ  ThreadAbortException
          // DQE  ThreadAbortException
          // DEQ  ThreadAbortException

          DispatcherQueue.Destroy();
          DispatcherQueue = null;
          TIBCO.Rendezvous.Environment.Close();
          _dispatcher.Destroy();
          _dispatcher = null;
      }
  }
}

## Events that indicate exceptional situations

> [A link to the discussion](https://github.com/sidristij/dotnetbook/issues/51)

In general, we don’t always know about those exceptions that can happen in our programs because we almost always use somebody else’s code, something that is in other subsystems or libraries. Not only are different situations possible in your code or in the code of other libraries, but there are a lot of problems with running code in isolated domains. In this case, it would be useful to get information about the running isolated code  because the situation when third-party code catches all exceptions by addressing them with a `fault` block can be quite real:

```csharp
    try {
        // ...
    } catch {
        // just to make code call safer
    }
```

In this case, the execution of code may seem safer as it actually is, but we won’t get any messages about problems. The second case is when an application suppresses an exception, even a legal one. As a result, the next exception in a random place will cause the application to break down because of a seemingly accidental error. In this case, we would prefer to know what happened prior to this error. What chain of events caused such an outcome? To make this knowledge possible you may choose to employ additional events that belong to exceptional situations: `AppDomain.FirstChanceException` and `AppDomain.UnhandledException`.

In fact, when you *"throw an exception"*, you call an ordinary method of the `throw` subsystem. This method performs several internal operations:

  - calls `AppDomain.FirstChanceException`;
  - looks through the chain of handlers to find one with relevant filters;
  - goes back to a necessary frame in stack and calls the handler;
  - if the handler was not found, calls `AppDomain.UnhandledException` and terminates the thread where the exception was thrown.

You may wonder if you can cancel the exception in uncontrolled code which runs in an isolated domain without terminating the thread where this exception was thrown. The answer is NO.

If an exception was not caught throughout the whole range of called methods it can’t be handled at all. Otherwise, there is a strange situation: if we use `AppDomain.FirstChanceException` to process (with some synthetic `catch`) an exception, which frame should the stack go back to? How should we set it up within the rules of `.NET` CLR? In no way. It’s impossible. The only thing we can do is to record this data for future research.

Another thing we should mention at "the very beginning” is why `thread` and not `AppDomain` has these events. It is because exceptions logically appear in an execution thread. That means `thread` in fact. So, why does a domain have problems? The answer is simple: what are the situations `AppDomain.FirstChanceException` and `AppDomain.UnhandledException` used for? Above all, they are used to create sandboxes for plug-ins, i.e. situations where there is some `AppDomain` which is set for PartialTrust. All kinds of things can happen in this `AppDomain`: new threads can be created at any moment or those already existing in `ThreadPool` can be used. Thus, we, being outside this process (as we didn’t write this code), can’t subscribe to events of these internal threads — we just don’t know what threads were created there. But we definitely have the `AppDomain` which we have a link to and which creates a sandbox for us.

So, in fact, we have two boundary states: something unexpected happened (`FirstChanceException`) and "things are bad", the exception was unexpected and nobody handled it. That’s why further thread execution makes no sense and the `thread` will be unloaded.

What can we get if we have these events and why is it bad if developers ignore them.

### AppDomain.FirstChanceException

This event is purely informational and can’t be handled. Its task is to notify us about an exception that happened within this domain and will be processed by the application code after processing the event. Its execution has several features we should be aware of when designing a handler.

However, let’s first look at a simple synthetic example of exception handling:

```csharp
void Main()
{
    var counter = 0;

    AppDomain.CurrentDomain.FirstChanceException += (_, args) => {

        Console.WriteLine(args.Exception.Message);

        if(++counter == 1) {
            throw new ArgumentOutOfRangeException();
        }
    };

    throw new Exception("Hello!");
}

```

What is in this code?

Firstly, no matter where an exception is generated, it will be logged in a console first. I mean even if you forget or can’t implement the handling of some type of exception it will anyway appear in a log that you arrange.

Secontly, we see a somewhat strange condition for throwing an internal exception. It is because you can’t just throw another exception inside a `FirstChanceException` handler. Or, it’s better to say you *don’t have an opportunity* to throw an exception inside `FirstChanceException`. If you do this, it may have two possible outcomes.

First, if we didn’t have the `if(++counter == 1)` condition, we would get infinite `FirstChanceException` for all new `ArgumentOutOfRangeException`. What does it mean? It means that at some point we would get `StackOverflowException`: `throw new Exception("Hello!")` calls the `Throw` CLR method that calls `FirstChanceException` which calls `Throw` for `ArgumentOutOfRangeException` already and so on based on recursion. The second variant involves protecting ourselves in terms of recursion depth using `counter` condition. In this case, we throw an exception only once. The result is unexpected as we get an exceptional situation which in fact occurs inside a `throw` instruction.

What does fit best for such an error type? ECMA-335 says that if an instruction came to an unexpected state, `ExecutionEngineException` should be thrown. This is an exception that we can’t handle. It leads to application termination. What variants of safe handling do we have?

The first that comes to mind is to secure the whole `FirstChanceException` handler code by a `try-catch` block:

```csharp
void Main()
{
    var fceStarted = false;
    var sync = new object();

    EventHandler<FirstChanceExceptionEventArgs> handler;
    handler = new EventHandler<FirstChanceExceptionEventArgs>((_, args) =>
    {
        lock (sync)
        {
            if (fceStarted)
            {
                // This code is in fact a stub to notify that an exception comes not from the main code of an application 
                // but from the `try` section below.
                Console.WriteLine($"FirstChanceException inside FirstChanceException ({args.Exception.GetType().FullName})");
                return;
            }
            fceStarted = true;

            try
            {
                // unsafe logging to some place,  e. g. to a database
                Console.WriteLine(args.Exception.Message);
                throw new ArgumentOutOfRangeException();
            }
            catch (Exception exception)
            {
                // this logging must be absolutely safe
                Console.WriteLine("Success");
            }
            finally
            {
                fceStarted = false;
            }
        }
    });

    AppDomain.CurrentDomain.FirstChanceException += handler;

    try
    {
        throw new Exception("Hello!");
    } finally {
        AppDomain.CurrentDomain.FirstChanceException -= handler;
    }
}

OUTPUT:

Hello!
Specified argument was out of the range of valid values.
FirstChanceException inside FirstChanceException (System.ArgumentOutOfRangeException)
Success

!Exception: Hello!
```

Thus, on the one hand, we have a code to handle a `FirstChanceException` event and on the other hand, we have an additional code to handle exceptions in `FirstChanceException` itself.

However, approaches to log both scenarios should be different. If event processing can be logged in any possible way, the handling of `FirstChanceException` logic error should be done without any exceptions. The second thing, which you probably noticed, is the synchronization among threads. You may ask why it should be here if any exception appears in some thread which means that `FirstChanceException` is thread-safe in theory.

Don’t be so optimistic. `FirstChanceException` happens in `AppDomain`.  It means it impacts any thread started in a particular domain. Thus, if we have a domain with several running threads, then `FirstChanceException` can run in parallel. It means that we should protect our thread by synchronization, e.g. using `lock`.

Another way is to transfer exception handling to an adjacent thread, belonging to another application domain. However, in this case we should build a domain, dedicated for this task so that other worker threads wouldn’t bring this domain down.

```csharp
static void  Main()
{
    using (ApplicationLogger.Go(AppDomain.CurrentDomain))
    {
        throw new Exception("Hello!");
    }
}

public class ApplicationLogger : MarshalByRefObject
{
    ConcurrentQueue<Exception> queue = new ConcurrentQueue<Exception>();
    CancellationTokenSource cancellation;
    ManualResetEvent @event;

    public void LogFCE(Exception message)
    {
        queue.Enqueue(message);
    }

    private void StartThread()
    {
        cancellation = new CancellationTokenSource();
        @event = new ManualResetEvent(false);
        var thread = new Thread(() =>
        {
            while (!cancellation.IsCancellationRequested)
            {
                if (queue.TryDequeue(out var exception))
                {
                    Console.WriteLine(exception.Message);
                }
                Thread.Yield();
            }
            @event.Set();
        });
        thread.Start();
    }

    private void StopAndWait()
    {
        cancellation.Cancel();
        @event.WaitOne();
    }

    public static IDisposable Go(AppDomain observable)
    {
        var dom = AppDomain.CreateDomain("ApplicationLogger", null, new AppDomainSetup
        {
            ApplicationBase = AppDomain.CurrentDomain.BaseDirectory,
        });

        var proxy = (ApplicationLogger)dom.CreateInstanceAndUnwrap(typeof(ApplicationLogger).Assembly.FullName, typeof(ApplicationLogger).FullName);

        proxy.StartThread();

        var subscription = new EventHandler<FirstChanceExceptionEventArgs>((_, args) =>
        {
            proxy.LogFCE(args.Exception);
        });
        observable.FirstChanceException += subscription;

        return new Subscription(() => {
            observable.FirstChanceException -= subscription;
            proxy.StopAndWait();
        });
    }

    private class Subscription : IDisposable
    {
        Action act;
        public Subscription (Action act) {
            this.act = act;
        }

        public void Dispose()
        {
            act();
        }
    }
}
```

In this case, the handling of `FirstChanceException` is done safely in an adjacent thread that belongs to a neighboring domain. Thus, the errors of message handling can’t bring worker threads down. Additionally, we can listen to `UnhandledException` in message logging domain: fatal errors during logging won’t bring the whole application down.

### AppDomain.UnhandledException

The second message which we can catch and which refers to exception handling is `AppDomain.UnhandledException`. This message is bad news for us as it means that nobody was able to handle an error in some thread. It also means, that the only thing we can do is to clean up the consequences of such an error,  i.e. the resources assigned to this thread if they were created. However, it’s better to handle exceptions within threads, without bringing a thread down,  that means to use `try-catch`. Let’s check the efficiency of this approach.

Assume that we have a library that should create threads and implement some logic in them. We, as users of this library, are interested only in guaranteed API calls and getting error messages.

If the library brings down threads quietly, it won’t be of much use for us. Moreover, bringing down a thread will lead to an `AppDomain.UnhandledException` message which doesn’t indicates the affected thread. In case of our own code, this thread won’t be helpful either. Anyway, I haven’t seen situations where it could be necessary. Our task is to handle errors properly, log them, and terminate a thread correctly. In fact it means that we should wrap a method that starts a thread in `try-catch`:

```csharp
    ThreadPool.QueueUserWorkitem(_ => {
        using(Disposables aggregator = ...){
            try {
                // do work here, plus:
                aggregator.Add(subscriptions);
                aggregator.Add(dependantResources);
            } catch (Exception ex)
            {
                logger.Error(ex, "Unhandled exception");
            }
        }
    });
```

With this scheme we will meet our goal: on the one hand, we don’t bring a thread down, on the other hand, we clean up local resources correctly if they were created. Also, we log those errors that we get.

"*Hold on*" - you might say - "*You left the topic of* `AppDomain.UnhandledException` *event too quickly. Is it really unnecessary?*"

Well, it is necessary. But we need it only to alert us if we forgot to wrap some threads in `try-catch` with all the necessary logic, including logging and resource cleanup. Otherwise, it would be completely wrong to address all exceptions as if they never occured.

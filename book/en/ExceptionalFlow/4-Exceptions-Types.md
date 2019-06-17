### CLR Exceptions

> [A link to the discussion](https://github.com/sidristij/dotnetbook/issues/51)

There are some exceptional situations that, let’s say, more exceptional than others. In terms of classification, as it was said at the beginning of this chapter, we can divide them into ones that belong to a .NET application and others that belong to the unsafe world. The latter group consists of two subcategories: the exceptions of the CLR core (which is essentially unsafe) and any unsafe code of external libraries.

[todo]

#### ThreadAbortException

Although it may seem not so obvious, there are four types of Thread Abort.

  - Rough `ThreadAbort` which gets unstoppable when activated and which doesn’t call exception handlers at all, including `finally` sections.
  - `Thread.Abort()` method, invoked on a current thread.
  - Asynchronous `ThreadAbortException` which is thrown from some other thread.
  - `ThreadAbort` invoked on the threads which are initiated when the `AppDomain` is being unloaded and which contains running methods compiled for this domain.

> It should be noted that `ThreadAbortException` is often used in _big_ .NET Framework, yet doesn’t exist for CoreCLR, .NET Core or for Windows 8 "Modern app profile". Let’s figure out why.

For the second, third or fourth variant of a thread abort, when we are still able to do something, a virtual machine starts going through all exception handlers and look up those that correspond to the type of a thrown exception or for a higher exception level. In our case these are: `ThreadAbortException`, `Exception` and `object` (don’t forget that Exception is essentially a data set and our search may end up with any type of exception, even with `int`). By handling *all* suitable `catch` blocks, the virtual machine successively forwards `ThreadAbortException` along the entire chain of exception handling and enters all `finally` blocks. In general, the situations described below are identical:

```csharp
var thread = new Thread(() =>
{
    try {
        // ...
    } catch (Exception ex)
    {
        // ...
    }
});
thread.Start();
//...
thread.Abort();

var thread = new Thread(() =>
{
    try {
        // ...
    } catch (Exception ex)
    {
        // ...
        if(ex is ThreadAbortException)
        {
            throw;
        }
    }
});
thread.Start();
//...
thread.Abort();
```

Sometimes, we definitely may expect `ThreadAbort` and  have an understandable desire to handle it. For these cases there is `Thread.ResetAbort()` method which does what we need: stops forwarding an exception through the chain of handlers, thus making this exception handled:

```csharp
void Main()
{
    var barrier = new Barrier(2);

    var thread = new Thread(() =>
    {
        try {
            barrier.SignalAndWait();  // Breakpoint #1
            Thread.Sleep(TimeSpan.FromSeconds(30));
        }
        catch (ThreadAbortException exception)
        {
            "Resetting abort".Dump();
            Thread.ResetAbort();
        }

        "Caught successfully".Dump();
        barrier.SignalAndWait();     // Breakpoint #2
    });

    thread.Start();
    barrier.SignalAndWait();         // Breakpoint #1

    thread.Abort();
    barrier.SignalAndWait();         // Breakpoint #2
}

Output:
Resetting abort
Catched successfully
```

But should we really use it? And should we be mad at CoreCLR developers because they got rid of this code? Assume you are a user of code and you think it is "stuck". You have a sharp desire to call `ThreadAbortException`. When you want to terminate a thread, all you really want is that this thread really stops running. Moreover, it rarely happens when an algorithm aborts the thread and leaves it. Normally, it waits for correct termination of operations. Alternatively, it decides that the thread is dead, decrements some internal counters and forgets that there is some multithread processing of some code. You’ll never know which one is worse. Moreover, after many years of being a programmer I still can’t offer a great way to call and handle it. Imagine—you throw `ThreadAbort` not _right away_ but in any case some time after having understood that the situation is hopeless. So sometimes you hit `ThreadAbortException` handler and sometimes you miss it: "stuck code" may be in fact not stuck but a too long executed one. And in that very moment you wanted to kill it, this code could have started working correctly again,  i.e. exited `try-catch(ThreadAbortException) { Thread.ResetAbort(); }` block. What will we get in this case? An aborted thread that happened to stuck through no fault of its own. For example, a janitor could have passed by, unplugged the cable and brought the network down. A method was waiting for a timeout and when the janitor has plugged the cable back, everything started working again, but your controlling code had already killed the thread. Is it OK? No. Can we safeguard from this? No. But let’s get back to our idea of legalizing `Thread.Abort()`: we have thrown a hammer at the thread and now expect the latter to be inevitably aborted, but it may never happen. Firstly, it is not clear how to abort it in this case. Things may be more complicated here: a stuck thread can have logic inside which catches `ThreadAbortException`, terminates it using `ResetAbort`, but remains stuck because of broken logic. What’s then? Should we use `thread.Interrupt()`? It seems like an attempt to bypass a program logic error using brute force methods. Also, I guarantee you will get resource leaks: `thread.Interrupt()` won’t call `catch` and `finally` which means you can’t clean up resources. Your thread will simply disappear and if you are in an adjacent thread you won’t know the references to all resources, allocated to the aborted thread. Also, note that if `ThreadAbortException` misses `catch(ThreadAbortException) { Thread.ResetAbort(); }` you will get resource leaks too.

I hope that after having read the information above you will feel some confusion and intention to reread the paragraph. And this would be a good idea that proves you mustn’t use `Thread.Abort()`  as well as `thread.Interrupt();`. Both methods make the behavior of your application uncontrolled. They violate the integrity principle, which is the main principle of the .NET Framework.

However, to understand why developers introduced this method you should have a look at the .NET Framework source code and find where `Thread.ResetAbort()` is invoked. Because this method makes `thread.Abort()` legitimate.

**ISAPIRuntime class** [ISAPIRuntime.cs](https://referencesource.microsoft.com/#System.Web/Hosting/ISAPIRuntime.cs,192)

```csharp
try {

    // ...

}
catch(Exception e) {
    try {
        WebBaseEvent.RaiseRuntimeError(e, this);
    } catch {}

    // Have we called HSE_REQ_DONE_WITH_SESSION?  If so, don't re-throw.
    if (wr != null && wr.Ecb == IntPtr.Zero) {
        if (pHttpCompletion != IntPtr.Zero) {
            UnsafeNativeMethods.SetDoneWithSessionCalled(pHttpCompletion);
        }
        // if this is a thread abort exception, cancel the abort
        if (e is ThreadAbortException) {
            Thread.ResetAbort();
        }
        // IMPORTANT: if this thread is being aborted because of an AppDomain.Unload,
        // the CLR will still throw an AppDomainUnloadedException. The native caller
        // must special case COR_E_APPDOMAINUNLOADED(0x80131014) and not
        // call HSE_REQ_DONE_WITH_SESSION more than once.
        return 0;
    }

    // re-throw if we have not called HSE_REQ_DONE_WITH_SESSION
    throw;
}
```

This example calls some external code and if it was terminated incorrectly with `ThreadAbortException`, we mark the thread under certain circumstances as non-abortable anymore. In fact, we handle `ThreadAbort`. Why do we abort `Thread.Abort` in this case? Because here, we deal with server code which must return correct error codes to a calling party regardless of our errors. A thread abort would prevent a server to return a necessary error code to user, which is absolutely inappropriate. Also, there is a comment about `Thread.Abort()` during `AppDomain.Unload()` which is an extreme situation for `Thread.Abort` as you can’t stop such a process even if you use `Thread.ResetAbort`. Though it will stop abort handling, it won’t stop the unloading of a thread inside a domain: a thread can’t execute code instructions from the domain being currently unloaded.

**HttpContext class** [HttpContext.cs](https://referencesource.microsoft.com/#System.Web/HttpContext.cs,1864)

```csharp
internal void InvokeCancellableCallback(WaitCallback callback, Object state) {
    // ...
 
    try {
        BeginCancellablePeriod();  // request can be cancelled from this point
        try {
            callback(state);
        }
        finally {
            EndCancellablePeriod();  // request can be cancelled until this point
        }
        WaitForExceptionIfCancelled();  // wait outside of finally
    }
    catch (ThreadAbortException e) {
        if (e.ExceptionState != null &&
            e.ExceptionState is HttpApplication.CancelModuleException &&
            ((HttpApplication.CancelModuleException)e.ExceptionState).Timeout) {

            Thread.ResetAbort();
            PerfCounters.IncrementCounter(AppPerfCounter.REQUESTS_TIMED_OUT);

            throw new HttpException(SR.GetString(SR.Request_timed_out),
                                null, WebEventCodes.RuntimeErrorRequestAbort);
        }
    }
}
```

This is a great example of transfer from unmanaged asynchronous `ThreadAbortException` to managed `HttpException` with recording the case to the Performance Counters Log.

**HttpApplication class** [HttpApplication.cs](https://referencesource.microsoft.com/#System.Web/HttpApplication.cs,2270)

```csharp
 internal Exception ExecuteStep(IExecutionStep step, ref bool completedSynchronously) 
 {
    Exception error = null;

    try {
        try {

        // ...

        }
        catch (Exception e) {
            error = e;

            // ...

            // This might force ThreadAbortException to be thrown
            // automatically, because we consumed an exception that was
            // hiding ThreadAbortException behind it

            if (e is ThreadAbortException &&
                ((Thread.CurrentThread.ThreadState & ThreadState.AbortRequested) == 0))  {
                // Response.End from a COM+ component that re-throws ThreadAbortException
                // It is not a real ThreadAbort
                // VSWhidbey 178556
                error = null;
                _stepManager.CompleteRequest();
            }
        }
        catch {
            // ignore non-Exception objects that could be thrown
        }
    }
    catch (ThreadAbortException e) {
        // ThreadAbortException could be masked as another one
        // the try-catch above consumes all exceptions, only
        // ThreadAbortException can filter up here because it gets
        // auto rethrown if no other exception is thrown on catch
        if (e.ExceptionState != null && e.ExceptionState is CancelModuleException) {
            // one of ours (Response.End or timeout) -- cancel abort

            // ...

            Thread.ResetAbort();
        }
    }
}
```

You can see an interesting scenario here—to expect a false `ThreadAbort` (I feel sympathy with CLR and .NET Framework team in  how unbelievably many unusual scenarios they have to handle). This time a scenario is handled in two stages: initially, using an internal handler, we catch `ThreadAbortException` and then check whether our thread is marked for abort.  If it is not, the `ThreadAbortException` is false. We should address these scenarios accordingly: catch an exception and handle it. If we get a true `ThreadAbort`, it will go to an external `catch` because `ThreadAbortException` should enter all suitable handlers. If it meets necessary conditions, it will also be handled by removing the `ThreadState.AbortRequested` flag using the `Thread.ResetAbort()`.

In terms of `Thread.Abort()` calls, all the examples of code in .NET Framework may be rewritten without using it. Just one example for clarity:

**QueuePathDialog class** [QueuePathDialog.cs](https://referencesource.microsoft.com/#System.Messaging/System/Messaging/Design/QueuePathDialog.cs,364)

```csharp
protected override void OnHandleCreated(EventArgs e)
{
    if (!populateThreadRan)
    {
        populateThreadRan = true;
        populateThread = new Thread(new ThreadStart(this.PopulateThread));
        populateThread.Start();
    }

    base.OnHandleCreated(e);
}

protected override void OnFormClosing(FormClosingEventArgs e)
{
    this.closed = true;

    if (populateThread != null)
    {
        populateThread.Abort();
    }

    base.OnFormClosing(e);
}

private void PopulateThread()
{
    try
    {
        IEnumerator messageQueues = MessageQueue.GetMessageQueueEnumerator();
        bool locate = true;
        while (locate)
        {
            // ...
            this.BeginInvoke(new FinishPopulateDelegate(this.OnPopulateTreeview), new object[] { queues });
        }
    }
    catch
    {
        if (!this.closed)
            this.BeginInvoke(new ShowErrorDelegate(this.OnShowError), null);
    }

    if (!this.closed)
        this.BeginInvoke(new SelectQueueDelegate(this.OnSelectQueue), new object[] { this.selectedQueue, 0 });
}
```

##### ThreadAbortException during AppDomain.Unload

Let’s unload AppDomain while executing code that is loaded into it. To do this we create an unnatural, yet interesting situation in terms of running code. Here we have two threads: one is `main` and the other one is created to get `ThreadAbortException`. In the `main` thread, we create a new domain and start a new thread in it. This thread must go to the `main` domain, so that child domain methods remain just in Stack Trace. Then, the `main` domain unloads the child one:

```csharp
class Program : MarshalByRefObject
{
    static void Main()
    {
        try
        {
            var domain = ApplicationLogger.Go(new Program());
            Thread.Sleep(300);
            AppDomain.Unload(domain);

        } catch (ThreadAbortException exception)
        {
            Console.WriteLine("Main AppDomain aborted too, {0}", exception.Message);
        }
    }

    public void InsideMainAppDomain()
    {
        try
        {
            Console.WriteLine($"InsideMainAppDomain() called inside {AppDomain.CurrentDomain.FriendlyName} domain");

            // AppDomain.Unload will be called while this Sleep
            Thread.Sleep(-1);
        }
        catch (ThreadAbortException exception)
        {
            Console.WriteLine("Subdomain aborted, {0}", exception.Message);

            // This sleep to allow user to see console contents
            Thread.Sleep(-1);
        }
    }

    public class ApplicationLogger : MarshalByRefObject
    {
        private void StartThread(Program pro)
        {
            var thread = new Thread(() =>
            {
                pro.InsideMainAppDomain();
            });
            thread.Start();
        }

        public static AppDomain Go(Program pro)
        {
            var dom = AppDomain.CreateDomain("ApplicationLogger", null, new AppDomainSetup
            {
                ApplicationBase = AppDomain.CurrentDomain.BaseDirectory,
            });

            var proxy = (ApplicationLogger)dom.CreateInstanceAndUnwrap(typeof(ApplicationLogger).Assembly.FullName, typeof(ApplicationLogger).FullName);
            proxy.StartThread(pro);

            return dom;
        }
    }

}
```

Exciting things happen here. The code that unloads a domain also looks up methods called in this domain that didn’t finish their work yet, including those in the depth of the method call stack, and invokes `ThreadAbortException` on these threads. This is important, yet not obvious. If a domain is unloaded, it’s impossible to return to a method from which the main domain method is called, but which exists in the unloaded domain. In other words, `AppDomain.Unload` may get rid of those threads that run code from other domains. In this case, it is not possible to abort `Thread.Abort`: you can’t execute the code of an unloaded domain which means `Thread.Abort` will finish its work even if you call `Thread.ResetAbort`.

##### Conclusions on ThreadAbortException

  - It’s an asynchronous exception, therefore it can appear anywhere in your code (though you should make efforts for it).
  - Normally, code handles only expected errors, such as a file access error, a string parsing error, etc. An asynchronous exception (which can appear anywhere) creates a scenario when `try-catch` may not be handled: you can’t be ready for ThreadAbort at every place in an application. It turns out this exception will cause resource leaks, anyway.
  - A thread may be aborted because of some domain unloading. If there are calls of unloaded domain methods in Stack Trace of a thread, this thread will get `ThreadAbortException` without `ResetAbort`.
  - In general, there shouldn’t be situations when you need to call `Thread.Abort()` as the result is almost always unpredictable.
  - CoreCLR doesn’t have a `Thread.Abort()` manual call option: it was eliminated from the class. But it doesn’t mean you can’t get it.

#### ExecutionEngineException

This exception is marked as `Obsolete` and has the following comment:

> This type previously indicated an unspecified fatal error in the runtime. The runtime no longer raises this exception so this type is obsolete

And this is not true. I guess the author of this comment would really want this to become true. However, let’s get back to the example of an exception in `FirstChanceException` and see it’s not true:

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

The execution of this code will cause `ExecutionEngineException`, though I would expect the `ArgumentOutOfRangeException` by Unhandled Exception from the `throw new Exception("Hello!")` instruction.  Maybe this seemed strange to the core developers, and they thought it would be more correct to throw `ExecutionEngineException`.

Another simple way to get `ExecutionEngineException` is to  customize marshaling to the unsafe world incorrectly. If you get wrong sizes of types or pass more than it is necessary, ruining, for example, the stack of a thread, you will get `ExecutionEngineException`. This is expectable, as here the CLR entered the state which it finds inconsistent. And it doesn’t know how to recover consistency. As a result, you get `ExecutionEngineException`.

Another thing that needs special attention is the diagnostics of `ExecutionEngineException`. Why is this exception thrown? If it suddenly appeared in your code, answer several questions.

  - Does your application use unsafe libraries? Is it you or third-party who use them? First, try to find where this error occurs in the application. If the code goes to the unsafe world and gets `ExecutionEngineException` there, carefully check the convergence of methods signatures both in your code and in imported one. Remember that if modules written in Delphi or other variants of Pascal are imported, arguments should be reversed (configured in `DllImport`: `CallingConvention.StdCall`).
  - Are you subscribed to `FirstChanceException`? Its code might cause an exception. In this case, just wrap a handler in `try-catch(Exception)` and write events to the error log.
  - Is it possible that your application is built partially for one platform and partially for another? Try to clear the nuget packages cache and rebuild the application from scratch with obj/bin folders emptied manually.
  - Is there a problem with the framework itself? This may happen in the early versions of .NET Framework 4.0.  In this case, test a separate piece of code that causes an error in a newer version of the framework.

In general, don’t be afraid of this exception: it’s so rare that you may even forget about it till you meet it next time.

#### NullReferenceException

> TODO

#### SecurityException

> TODO

#### OutOfMemoryException

> TODO

### Corrupted State Exceptions

Once the platform was established and gets popular, programmers started massively migrating from C/C++ and MFC (Microsoft Foundation Classes) to more easy-to-develop environments. Besides .NET Framework, those environments included Qt, Java and С++ Builder—the mainstream was a movement towards virtualized execution of application code. Eventually, the thoughtfully designed .NET Framework started filling up its niche. Matured over the years, the platform turned from a shy newcomer into a key player. If previously we mostly had to deal with far too many components written in COM/ATL/ActiveX (Do you remember dragging COM/ActiveX components onto icon forms in Borland C++ Builder?), now life became much easier. Today the corresponding technologies are _quite_ rare to worry about and there is a chance to make them slightly uncomfortable so that people get rid of them and use state-of-the-art .NET Framework. We perceive old technologies that still exist and do well as archaic, forgotten, "faulty”, and old-fashioned. That’s why it is possible to make another step towards a closed sandbox: make it more impenetrable, more managed.

One of these steps is to introduce the notion of `Corrupted State Exceptions` which declares some exceptional situations illegitimate. Let’s see which exceptions are these and trace the history once again based on one of them – `AccessViolationException`:

**Util.cpp file** [util.cpp](https://github.com/dotnet/coreclr/blob/479b1e654cd5a13bb1ce47288cf78776b858bced/src/utilcode/util.cpp#L3163-L3197)

```cpp
BOOL IsProcessCorruptedStateException(DWORD dwExceptionCode, BOOL fCheckForSO /*=TRUE*/)
{
    // ...

    // If we have been asked not to include SO in the CSE check
    // and the code represent SO, then exit now.
    if ((fCheckForSO == FALSE) && (dwExceptionCode == STATUS_STACK_OVERFLOW))
    {
        return fIsCorruptedStateException;
    }

    switch(dwExceptionCode)
    {
        case STATUS_ACCESS_VIOLATION:
        case STATUS_STACK_OVERFLOW:
        case EXCEPTION_ILLEGAL_INSTRUCTION:
        case EXCEPTION_IN_PAGE_ERROR:
        case EXCEPTION_INVALID_DISPOSITION:
        case EXCEPTION_NONCONTINUABLE_EXCEPTION:
        case EXCEPTION_PRIV_INSTRUCTION:
        case STATUS_UNWIND_CONSOLIDATE:
            fIsCorruptedStateException = TRUE;
            break;
    }

    return fIsCorruptedStateException;
}
```

Let’s look at the description of our exceptional situations:

| Error code                        | Description                                                                                   |
|------------------------------------|------------------------------------------------------------------------------------------------|
| STATUS_ACCESS_VIOLATION            | A quite frequent error when you try to work with a memory range without access rights. Though memory is linear in terms of a process, it’s not possible to work with its entire range. You can use only those "portions” allocated by an operating system or ranges that you have access rights to (there are ranges owned by an operating system exclusively or available for code execution, yet not for reading) |
| STATUS_STACK_OVERFLOW              | Everybody knows this error: there is not enough memory on the thread stack to call another method    |
| EXCEPTION_ILLEGAL_INSTRUCTION      | Another code, read by a processor from a method body, wasn’t recognized as an instruction            |
| EXCEPTION_IN_PAGE_ERROR            | A thread attempted to work with a non-existing memory page        |
| EXCEPTION_INVALID_DISPOSITION      | An exception handling mechanism returned a wrong handler. Such an exception should never appear in programs written in high-level languages (e.g. С++) |
| EXCEPTION_NONCONTINUABLE_EXCEPTION | A thread attempted to continue the execution of a program after an exception prohibited the execution. This is not about `catch/fault/finally` blocks. It's rather about something like exception filters that allowed to fix an error (which caused an exception) and then tried to execute that code again |
| EXCEPTION_PRIV_INSTRUCTION         | An attempt to execute a privileged instruction of a processor                                      |
| STATUS_UNWIND_CONSOLIDATE          | An exception connected with the stack unwind, which is beyond the scope of our discussion |

Note that only two of these exceptions are worth catching: `STATUS_ACCESS_VIOLATION` and `STATUS_STACK_OVERFLOW`. Other errors are exceptional even for exceptional situations. They are rather fatal errors and we can’t consider them. So, let’s discuss only these two errors in detail.

#### AccessViolationException

This exception is bad news which you don’t want to get. But if you get it, it’s not clear what to do with it. `AccessViolationException` indicates that you "missed" a memory range allocated for an application and is thrown when you try to read from or write to a protected region of memory. The word "protected” rather denotes an attempt to work with a range of memory which hasn’t been allocated yet or which has been already cleared. Here I am not talking about a garbage collector that allocates and clears memory. It just assigns the chunks of allocated memory for your and its own needs. Memory has a layered structure in a way. First, there is the layer of memory management by a garbage collector. Next is the layer used by CoreCLR libraries to manage the allocation of memory which is followed by the layer used by OS to manage memory allocation from the pool of available fragments of linear address space. Thus, this exception appears when an application misses its memory range and attempts to work with a region which isn’t yet allocated or intended for this application. In this situation you don’t have many variants for analysis.

  – If `StackTrace` goes deep into CLR, you are very unlucky, because it’s probably a core error. However, this almost never happens. You can bypass the error by updating the core version or act in another way.
  – If `StackTrace` goes into the unsafe code of some library, you either got marshaling wrong or there is a serious error in the unsafe library. Check method arguments carefully: it’s possible that native method arguments have another bit size, another order or just another size. Check that structures are passed by value or by reference where appropriate.

To catch this exception at the moment you should show a JIT-compiler it’s really necessary. Otherwise, it won’t be caught and you will get a broken application. However, you should catch this exception only if you can handle it properly: it may indicate a leak of memory if it was allocated by an unsafe method between calling this method and throwing `AccessViolationException`. At this point, an application may still function but its work may be incorrect because if you catch an error of a method call you will definitely try to call this method again. In this case, nobody knows what may go wrong: you can’t know how the state of an application was violated previously. However, if you still want to catch this exception, pay attention to the table of possible options for doing this in different versions of .NET Framework:

.NET Framework version | AccessViolationExeception
----------------------|-----------------------------------------------------------
1.0                   | NullReferenceException
2.0, 3.5              | AccessViolation can be caught
4.0+                  | AccessViolation can be caught, but an adjustment is needed
.NET Core             | AccessViolation *can’t* be caught

In other words, if you have a very old application, working under .NET Framework 1.0, ~~show it to me~~ you will get NRE which will be a sort of deception: you passed a pointer with a value greater than zero and got `NullReferenceException`. However, I think this behavior is justified: if you are in the world of managed code, you don’t want to learn the error types of unmanaged code, and NRE—which is essentially "a bad pointer to an object”  in the world of .NET—is suitable here. However, things are not that simple. Users needed this type of exception in real life and it was introduced in .NET Framework 2.0. For years this was an exception you can catch. Then, it lost this capability, but a special structure appeared that allowed activating the catch option. This sequence of the CLR team decisions at every stage looks justified. See it for yourself:

  - `1.0` Missing allocated memory ranges should be an exceptional situation because if an application works with an address it got it from some place. In the managed world this place is the `new` operator. In the unmanaged world, every piece of code can be the place for such an error. Though these two exceptions are opposed in terms of essence (NRE works with an uninitialized pointer and AVE works with an incorrectly initialized pointer), incorrectly initialized pointers don’t exist from the .NET ideology point of view. Both cases can be reduced to an incorrectly initialized pointer. So, let’s do it and throw `NullReferenceException` in both cases.
  - `2.0` During the early stages of .NET Framework existence it turned out that there is more code which is inherited via COM libraries than native code: there is a huge code base of commercial components for networking, UI, DB, and other subsystems. It means that the possibility of getting `AccessViolationException` exactly is real: the wrong diagnostics of a problem can make it more expensive to catch. Therefore, `AccessViolationException` was introduced in .NET Framework.
  - `4.0` .NET Framework took hold and squeezed low-level programming languages. The number of COM components decreased sharply: almost all major tasks are solved within the framework already and working with unsafe code is treated as something incorrect. In these conditions we can get back to the ideology, introduced in the framework from the very beginning: .NET is for .NET only. Unsafe code is not a norm but a least-evil state; therefore catching `AccessViolationException` contradicts the ideology of the “framework as a platform” notion, i.e. of a full-fledged simulated sandbox with its own rules. However, we still use this platform and have to catch this exception in many cases: we introduce a special catch mode only if a corresponding configuration is used.
  - `.NET Core` The dream of the CLR team has come true: working with unsafe code beyond the law in .NET anymore, and therefore the existence of `AccessViolationException` is not legitimate even at the configuration level. .NET is mature enough to make its own rules. Now the existence of this exception in an application will crash it. That’s why any unsafe code, i.e. CLR itself, must be safe in terms of this exception. If it appears in an unsafe library, nobody will use it. It means that developers writing third-party components in unsafe languages should be careful and handle this exception on their side.

This is how we can follow up how .NET has become a platform using the example of one exception: from playing by imposed rules to establishing its own rules.

With all things said I should only show how to activate the handling of this exception in a particular method in `4.0+`. You should:

  - add the following code to the `configuration/runtime` section:  `<legacyCorruptedStateExceptionsPolicy enabled="true|false"/>`
  - add two attributes for each method where it is *necessary* to handle `AccessViolationException`: `HandleProcessCorruptedStateExceptions` and `SecurityCritical`. These attributes allow activating the handling of Corrupted State Exceptions for _particular_ methods only. This procedure is particularly appropriate as you should be sure that you want to handle these exceptions and should know where: sometimes it is more appropriate to bring an application down.

Let’s look at the following code to see the activating of the `CSE` handler and the example of a trivial handling:

```csharp
[HandleProcessCorruptedStateExceptions, SecurityCritical]
public bool TryCallNativeApi()
{
    try
    {
        // Activating a method which can throw AccessViolationException
    }
    catch (Exception e)
    {
        // Logging, exit
        System.Console.WriteLine(e.Message);
        return false;
    }

  return true;
}
```

#### StackOverflowException

It is the last type of exception we should talk about. It appears when a memory array allocated for the stack runs out. We already discussed the structure of the stack in the corresponding chapter ([Thread stack](./ThreadStack.md)). So, here we will discuss only the error itself.

When there is not enough memory for the thread stack (or the next memory range is occupied and we can’t allocate the next page of virtual memory) or a thread used the allowed memory range, there is an attempt to access the address space which is called Guard page. This range is actually a trap and doesn’t take any physical memory. Instead of real writing or reading, a processor calls a special abort which should request a new memory range from an OS to accommodate the growth of the stack. If the absolute maximum value is reached the OS generates `STATUS_STACK_OVERFLOW` exception instead of allocating a new range. This exception, forwarded in .NET via the `Structured Exception Handling` mechanism, destroys the current thread as inconsistent.

Note that although this exception is `Corrupted State Exception`, you can’t catch it with `HandleProcessCorruptedStateExceptions`. I mean the following code won’t work:

```csharp
// Main.cs
[HandleProcessCorruptedStateExceptions, SecurityCritical]
static void Main()
{
    try
    {
        Recursive();
    } catch (Exception exception)
    {
        Console.WriteLine("Catched Stack Overflow!");
    }
}

static void Recursive()
{
    Recursive();
}

// app.config:
<configuration>
  <runtime>
    <legacyCorruptedStateExceptionsPolicy enabled="true"/>
  </runtime>
</configuration>
```

You can’t catch this exception because stack overflow can be caused by two reasons. The first is an intentional call of a recursive method which doesn’t control its own depth carefully. Here you may want to fix the situation by catching the exception. However, we therefore legalize this situation and allow it to happen again which means we are short-sighted, rather than careful. The second reason is accidental when `StackOverflowException` appears during a usual call. Just the depth of the stack at that moment was too critical. In this case, catching exceptions looks like something inappropriate at all: an application worked normally, everything was good and suddenly the legal call of a method with algorithms working correctly caused an exception, followed by unwinding stack up to the section of code that expects such behavior. Well... Once again: we expect that nothing will run in the next section as the stack will be out of memory. I think this is absurd.

![IDisposable cover](./imgs/Disposable-Cover.png)

# Disposable pattern (Disposable Design Principle)

> [A link to the discussion](https://github.com/sidristij/dotnetbook/issues/54)

I guess almost any programmer who uses .NET will now say this pattern is a piece of cake. That it is the best-known pattern used on the platform. However, even the simplest and well-known problem domain will have secret areas which you have never looked at. So, let’s describe the whole thing from the beginning for the first-timers and all the rest (so that each of you could remember the basics). Don’t skip these paragraphs — I am watching you!

## IDisposable

If I ask what is IDisposable, you will surely say that it is

```csharp
public interface IDisposable
{
    void Dispose();
}
```

What is the purpose of the interface? I mean, why do we need to clear up memory at all if we have a smart Garbage Collector that clears the memory instead of us, so we even don’t have to think about it. However, there are some small details.

There is a misconception that ```IDisposable``` serves to release unmanaged resources. This is only partially true and to understand it, you just need to remember the examples of unmanaged resources. Is ```File``` class an unmanaged resource? No. Maybe ```DbContext``` is an unmanaged resource? No, again. An unmanaged resource is something that doesn’t belong to .NET type system. Something the platform didn’t create, something that exists out of its scope. A simple example is an opened file handle in an operating system. A handle is a number that uniquely identifies a file opened – no, not by you – by an operating system. That is, all control structures (e.g. the position of a file in a file system, file fragments in case of fragmentation and other service information, the numbers of a cylinder, a head or a sector of an HDD) are inside an OS but not .NET platform. The only unmanaged resource that is passed to .NET platform is IntPtr number. This number is wrapped by FileSafeHandle, which is in its turn wrapped by the File class. It means the File class is not an unmanaged resource on its own, but uses an additional layer in the form of IntPtr to include an unmanaged resource — the handle of an opened file. How do you read that file? Using a set of methods in WinAPI or Linux OS.

Synchronization primitives in multithreaded or multiprocessor programs are the second example of unmanaged resources. Here belong data arrays that are passed through P/Invoke and also mutexes or semaphores.

> Note that OS doesn’t simply pass the handle of an unmanaged resource to an application. It also saves that handle in the table of handles opened by the process. Thus, OS can correctly close the resources after the application termination. This ensures the resources will be closed anyway after you exit the application. However, the running time of an application can be different which can cause long resource locking.

Ok. Now we covered unmanaged resources. Why do we need to use IDisposable in these cases? Because .NET Framework has no idea what’s going on outside its territory. If you open a file using OS API, .NET will know nothing about it. If you allocate a memory range for your own needs (for example using VirtualAlloc), .NET will also know nothing. If it doesn’t know, it will not release the memory occupied by a VirtualAlloc call. Or, it will not close a file opened directly via an OS API call. These can cause different and unexpected consequences. You can get OutOfMemory if you allocate too much memory without releasing it (e.g. just by setting a pointer to null). Or, if you open a file on a file share through OS without closing it, you will lock the file on that file share for a long time. The file share example is especially good as the lock will remain on the IIS side even after you close a connection with a server. You don’t have rights to release the lock and you will have to ask administrators to perform `iisreset` or to close resource manually using special software.
This problem on a remote server can become a complex task to solve.

All these cases need a universal and familiar _protocol for interaction_ between a type system and a programmer. It should clearly identify the types that require forced closing. The IDisposable interface serves exactly this purpose. It functions the following way: if a type contains the implementation of the IDisposable interface, you must call Dispose() after you finish work with an instance of that type.

So, there are two standard ways to call it. Usually you create an entity instance to use it quickly within one method or within the lifetime of the entity instance.

The first way is to wrap an instance into ```using(...){ ...  }```. It means you instruct to destroy an object after the using-related block is over, i.e. to call Dispose(). The second way is to destroy the object, when its lifetime is over, with a reference to the object we want to release. But .NET has nothing but a finalization method that implies automatic destruction of an object, right? However, finalization is not suitable at all as we don’t know when it will be called. Meanwhile, we need to release an object at a certain time, for example just after we finish work with an opened file. That is why we also need to implement IDisposable and call Dispose to release all resources we owned. Thus, we follow the _protocol_, and it is very important. Because if somebody follows it, all the participants should do the same to avoid problems.

## Different ways to implement IDisposable

Let’s look at the implementations of IDisposable from simple to complicated. The first and the simplest is to use IDisposable as it is:

```csharp
public class ResourceHolder : IDisposable
{
    DisposableResource _anotherResource = new DisposableResource();

    public void Dispose()
    {
        _anotherResource.Dispose();
    }
}
```

Here, we create an instance of a resource that is further released by Dispose(). The only thing that makes this implementation inconsistent is that you still can work with the instance after its destruction by ```Dispose()```:

```csharp
public class ResourceHolder : IDisposable
{
    private DisposableResource _anotherResource = new DisposableResource();
    private bool _disposed;

    public void Dispose()
    {
        if(_disposed) return;

        _anotherResource.Dispose();
        _disposed = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CheckDisposed()
    {
        if(_disposed) {
            throw new ObjectDisposedException();
        }
    }
}
```

CheckDisposed() must be called as a first expression in all public methods of a class. The obtained `ResourceHolder` class structure looks good to destroy an unmanaged resource, which is `DisposableResource`. However, this structure is not suitable for a wrapped-in unmanaged resource. Let’s look at the example with an unmanaged resource.

```csharp
public class FileWrapper : IDisposable
{
    IntPtr _handle;

    public FileWrapper(string name)
    {
        _handle = CreateFile(name, 0, 0, 0, 0, 0, IntPtr.Zero);
    }

    public void Dispose()
    {
        CloseHandle(_handle);
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFile", SetLastError = true)]
    private static extern IntPtr CreateFile(String lpFileName,
        UInt32 dwDesiredAccess, UInt32 dwShareMode,
        IntPtr lpSecurityAttributes, UInt32 dwCreationDisposition,
        UInt32 dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError=true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
```

What is the difference in the behavior of the last two examples? The first one describes the interaction of two managed resources. This means that if a program works correctly, the resource will be released anyway. Since ```DisposableResource``` is managed, .NET CLR knows about it and will release the memory from it if its behaviour is incorrect. Note that I consciously don’t assume what ```DisposableResource``` type encapsulates. There can be any kind of logic and structure. It can contain both managed and unmanaged resources. *This shouldn't concern us at all*. Nobody asks us to decompile third party’s libraries each time and see whether they use managed or unmanaged resources. And if *our type* uses an unmanaged resource, we cannot be unaware of this. We do this in ```FileWrapper``` class. So, what happens in this case? If we use unmanaged resources, we have two scenarios. The first one is when everything is OK and Dispose is called. The second one is when something goes wrong and Dispose failed.

Let’s say straight away why this may go wrong:

  - If we use ```using(obj) { ... }```, an exception may appear in an inner block of code. This exception is caught by ```finally``` block, which we cannot see (this is syntactic sugar of C#). This block calls Dispose implicitly. However, there are cases when this doesn’t happen. For example, neither ```catch``` nor ```finally``` catch ```StackOverflowException```. You should always remember this. Because if some thread becomes recursive and ```StackOverflowException``` occurs at some point, .NET will forget about the resources that it used but not released. It doesn’t know how to release unmanaged resources. They will stay in memory until OS releases them, i.e. when you exit a program, or even some time after the termination of an application.
  - If we call Dispose() from another Dispose(). Again, we may happen to fail to get to it. This is not the case of an absent-minded app developer, who forgot to call Dispose(). It is the question of exceptions. However, these are not only the exceptions that crash a thread of an application. Here we talk about all exceptions that will prevent an algorithm from calling an external Dispose() that will call our Dispose().

All these cases will create suspended unmanaged resources. That is because Garbage Collector doesn’t know it should collect them. All it can do upon next check is to discover that the last reference to an object graph with our ```FileWrapper``` type is lost. In this case, the memory will be reallocated for objects with references. How can we prevent it?

We must implement the finalizer of an object. The 'finalizer' is named this way on purpose. It is not a destructor as it may seem because of similar ways to call finalizers in C# and destructors in C++. The difference is that a finalizer will be called *anyway*, contrary to a destructor (as well as ```Dispose()```). A finalizer is called when Garbage Collection is initiated (now it is enough to know this, but things are a bit more complicated). It is used for a guaranteed release of resources if *something goes wrong*. We *must* implement a finalizer to release unmanaged resources. Again, because a finalizer is called when GC is initiated, we don’t know when this happens in general.

Let’s expand our code:

```csharp
public class FileWrapper : IDisposable
{
    IntPtr _handle;

    public FileWrapper(string name)
    {
        _handle = CreateFile(name, 0, 0, 0, 0, 0, IntPtr.Zero);
    }

    public void Dispose()
    {
        InternalDispose();
        GC.SuppressFinalize(this);
    }

    private void InternalDispose()
    {
        CloseHandle(_handle);
    }

    ~FileWrapper()
    {
        InternalDispose();
    }

    /// other methods
}
```

We enhanced the example with the knowledge about the finalization process and secured the application against losing resource information if Dispose() is not called. We also called GC.SuppressFinalize to disable the finalization of the instance of the type if Dispose() is successfully called. There is no need to release the same resource twice, right? Thus, we also reduce the finalization queue by letting go a random region of code that is likely to run with finalization in parallel, some time later. Now, let’s enhance the example even more.

```csharp
public class FileWrapper : IDisposable
{
    IntPtr _handle;
    bool _disposed;

    public FileWrapper(string name)
    {
        _handle = CreateFile(name, 0, 0, 0, 0, 0, IntPtr.Zero);
    }

    public void Dispose()
    {
        if(_disposed) return;
        _disposed = true;

        InternalDispose();
        GC.SuppressFinalize(this);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CheckDisposed()
    {
        if(_disposed) {
            throw new ObjectDisposedException();
        }
    }

    private void InternalDispose()
    {
        CloseHandle(_handle);
    }

    ~FileWrapper()
    {
        InternalDispose();
    }

    /// other methods
}
```

Now our example of a type that encapsulates an unmanaged resource looks complete. Unfortunately, the second ```Dispose()``` is in fact a standard of the platform and we allow to call it. Note that people often allow the second call of ```Dispose()``` to avoid problems with a calling code and this is wrong. However, a user of your library who looks at MS documentation may not think so and will allow multiple calls of Dispose(). Calling other public methods will destroy the integrity of an object anyway. If we destroyed the object, we cannot work with it anymore. This means we must call ```CheckDisposed``` at the beginning of each public method.

However, this code contains a severe problem that prevents it from working as we intended. If we remember how garbage collection works, we will notice one feature. When collecting garbage, GC *primarily* finalizes everything inherited directly from *Object*. Next it deals with objects that implement *CriticalFinalizerObject*. This becomes a problem as both classes that we designed inherit Object. We don’t know in which order they will come to the “last mile”. However, a higher-level object can use its finalizer to finalize an object with an unmanaged resource. Although, this doesn’t sound like a great idea. The order of finalization would be very helpful here. To set it, the lower-level type with an encapsulated unmanaged resource must be inherited from `CriticalFinalizerObject`.

The second reason is more profound. Imagine that you dared to write an application that doesn’t take much care of memory. It allocates memory in huge quantities, without cashing and other subtleties. One day this application will crash with OutOfMemoryException. When it occurs, code runs specifically. It cannot allocate anything, since it will lead to a repeated exception, even if the first one is caught. This doesn’t mean we shouldn’t create new instances of objects. Even a simple method call can throw this exception, e.g. that of finalization. I remind you that methods are compiled when you call them for the first time. This is usual behavior. How can we prevent this problem? Quite easily. If your object is inherited from *CriticalFinalizerObject*, then *all* methods of this type will be compiled straight away upon loading it in memory. Moreover, if you mark methods with *[PrePrepareMethod]* attribute, they will be also pre-compiled and will be secure to call in a low resource situation.

Why is that important? Why spend too much effort on those that pass away? Because unmanaged resources can be suspended in a system for long. Even after you restart a computer. If a user opens a file from a file share in your application, the former will be locked by a remote host and released on the timeout or when you release a resource by closing the file. If your application crashes when the file is opened, it won't be released even after reboot. You will have to wait long until the remote host releases it. Also, you shouldn’t allow exceptions in finalizers. This leads to an accelerated crash of the CLR and of an application as you cannot wrap the call of a finalizer in *try .. catch*. I mean, when you try to release a resource, you must be sure it can be released. The last but not less important fact: if the CLR unloads a domain abnormally, the finalizers of types, derived from *CriticalFinalizerObject* will be also called, unlike those inherited directly from *Object*.

## SafeHandle / CriticalHandle / SafeBuffer / derived types

I feel I’m going to open the Pandora’s box for you. Let’s talk about special types: SafeHandle, CriticalHandle and their derived types. 
This is the last thing about the pattern of a type that gives access to an unmanaged resource. But first, let’s list everything we _usually_ get from unmanaged world:

  - The first and obvious thing is handles. This may be an meaningless word for a .NET developer, but it is a very important component of the operating system world. A handle is a 32- or 64-bit number by nature. It designates an opened session of interaction with an operating system.   For example, when you open a file you get a handle from the WinApi function. Then you can work with it and do *Seek*, *Read* or *Write* operations. Or, you may open a socket for network access. Again an operating system will pass you a handle. In .NET handles are stored as *IntPtr* type;
  - The second thing is data arrays. You can work with unmanaged arrays either through unsafe code (unsafe is a key word here) or use SafeBuffer which will wrap a data buffer into a suitable .NET class. Note that the first way is faster (e.g. you can optimize loops greatly), but the second one is much safer, as it is based on SafeHandle;
  - Then go strings. Strings are simple as we need to determine the format and encoding of the string we capture. It is then copied for us (a string is an immutable class) and we don’t worry about it anymore.
  - The last thing is ValueTypes that are just copied so we don’t need to think about them at all.

SafeHandle is a special .NET CLR class that inherits CriticalFinalizerObject and should wrap the handles of an operating system in the safest and most comfortable way.

```csharp
[SecurityCritical, SecurityPermission(SecurityAction.InheritanceDemand, UnmanagedCode=true)]
public abstract class SafeHandle : CriticalFinalizerObject, IDisposable
{
    protected IntPtr handle;        // The handle from OS
    private int _state;             // State (validity, the reference counter)
    private bool _ownsHandle;       // The flag for the possibility to release the handle. 
                                    // It may happen that we wrap somebody else’s handle 
                                    // have no right to release.
    private bool _fullyInitialized; // The initialized instance

    [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
    protected SafeHandle(IntPtr invalidHandleValue, bool ownsHandle)
    {
    }

    // The finalizer calls Dispose(false) with a pattern
    [SecuritySafeCritical]
    ~SafeHandle()
    {
        Dispose(false);
    }

    // You can set a handle manually or automatically with p/invoke Marshal
    [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
    protected void SetHandle(IntPtr handle)
    {
        this.handle = handle;
    }

    // This method is necessary to work with IntPtr directly. It is used to  
    // determine if a handle was created by comparing it with one of the previously
    // determined known values. Pay attention that this method is dangerous because:
    //
    //   – if a handle is marked as invalid by SetHandleasInvalid, DangerousGetHandle
    //     it will anyway return the original value of the handle.
    //   – you can reuse the returned handle at any place. This can at least
    //     mean, that it will stop work without a feedback. In the worst case if
    //     IntPtr is passed directly to another place, it can go to an unsafe code and become
    //     a vector for application attack by resource substitution in one IntPtr
    [ResourceExposure(ResourceScope.None), ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
    public IntPtr DangerousGetHandle()
    {
        return handle;
    }

    // The resource is closed (no more available for work)
    public bool IsClosed {
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        get { return (_state & 1) == 1; }
    }

    // The resource is not available for work. You can override the property by changing the logic.
    public abstract bool IsInvalid {
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        get;
    }

    // Closing the resource through Close() pattern 
    [SecurityCritical, ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
    public void Close() {
        Dispose(true);
    }

    // Closing the resource through Dispose() pattern
    [SecuritySafeCritical, ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
    public void Dispose() {
        Dispose(true);
    }

    [SecurityCritical, ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
    protected virtual void Dispose(bool disposing)
    {
        // ...
    }

    // You should call this method every time when you understand that a handle is not operational anymore.
    // If you don’t do it, you can get a leak.
    [SecurityCritical, ResourceExposure(ResourceScope.None)]
    [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
    [MethodImplAttribute(MethodImplOptions.InternalCall)]
    public extern void SetHandleAsInvalid();

    // Override this method to point how to release
    // the resource. You should code carefully, as you cannot
    // call uncompiled methods, create new objects or produce exceptions from it.
    // A returned value shows if the resource was releases successfully.
    // If a returned value = false, SafeHandleCriticalFailure will occur
    // that will enter a breakpoint if SafeHandleCriticalFailure
    // Managed Debugger Assistant is activated.
    [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
    protected abstract bool ReleaseHandle();

    // Working with the reference counter. To be explained further.
    [SecurityCritical, ResourceExposure(ResourceScope.None)]
    [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
    [MethodImplAttribute(MethodImplOptions.InternalCall)]
    public extern void DangerousAddRef(ref bool success);
    public extern void DangerousRelease();
}
```

To understand the usefulness of the classes derived from SafeHandle you need to remember why .NET types are so great: GC can collect their instances automatically. As SafeHandle is managed, the unmanaged resource it wrapped inherits all characteristics of the managed world. It also contains an internal counter of external references which are unavailable to CLR. I mean references from unsafe code.  You don’t need to increment or decrement a counter manually at all. When you declare a type derived from SafeHandle as a parameter of an unsafe method, the counter increments when entering that method or decrements after exiting.  The reason is that when you go to an unsafe code by passing a handle there, you may get this SafeHandle collected by GC, by resetting the reference to this handle in another thread (if you deal with one handle from several threads). Things work even easier with a reference counter: SafeHandle will not be created until the counter is zeroed. That’s why you don’t need to change the counter manually. Or, you should do it very carefully by returning it when possible.

The second purpose of a reference counter is to set the order of finalization of ```CriticalFinalizerObject``` that reference each other. If one SafeHandle-based type references another, then you need to additionally increment a reference counter in the constructor of the referencing type and decrease the counter in the ReleaseHandle method. Thus, your object will exist until the object to which your object references is not destroyed. However, it's better to avoid such puzzlements. Let’s use the knowledge about SafeHandlers and write the final variant of our class:

```csharp
public class FileWrapper : IDisposable
{
    SafeFileHandle _handle;
    bool _disposed;

    public FileWrapper(string name)
    {
        _handle = CreateFile(name, 0, 0, 0, 0, 0, IntPtr.Zero);
    }

    public void Dispose()
    {
        if(_disposed) return;
        _disposed = true;
        _handle.Dispose();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CheckDisposed()
    {
        if(_disposed) {
            throw new ObjectDisposedException();
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFile", SetLastError = true)]
    private static extern SafeFileHandle CreateFile(String lpFileName,
        UInt32 dwDesiredAccess, UInt32 dwShareMode,
        IntPtr lpSecurityAttributes, UInt32 dwCreationDisposition,
        UInt32 dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    /// other methods
}
```

How is it different? If you set **any** SafeHandle-based type (including your own) as the return value in the DllImport method, then Marshal will correctly create and initialize this type and set a counter to 1. Knowing this we set the SafeFileHandle type as a return type for the CreateFile kernel function. When we get it, we will use it exactly to call ReadFile and WriteFile (as a counter value increments when calling and decrements when exiting it will ensure that the handle still exist throughout reading from and writing to a file). This is a correctly designed type and it will reliably close a file handle if a thread is aborted. This means we don’t need to implement our own finalizer and everything connected with it. The whole type is simplified.

### The execution of a finalizer when instance methods work

There is one optimization technique used during garbage collection that is designed to collect more objects in less time.  Let’s look at the following code:

```csharp
public void SampleMethod()
{
    var obj = new object();
    obj.ToString();

    // ...
    // If GC runs at this point, it may collect obj
    // as it is not used anymore
    // ...

    Console.ReadLine();
}
```

On the one hand, the code looks safe, and it’s not clear straightaway why should we care. However, if you remember that there are classes that wrap unmanaged resources, you will understand that an incorrectly designed class may cause an exception from the unmanaged world. This exception will report that a previously obtained handle is not active:

```csharp
// The example of an absolutely incorrect implementation
void Main()
{
    var inst = new SampleClass();
    inst.ReadData();
    // inst is not used further
}

public sealed class SampleClass : CriticalFinalizerObject, IDisposable
{
    private IntPtr _handle;

    public SampleClass()
    {
        _handle = CreateFile("test.txt", 0, 0, IntPtr.Zero, 0, 0, IntPtr.Zero);
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            CloseHandle(_handle);
            _handle = IntPtr.Zero;
        }
    }

    ~SampleClass()
    {
        Console.WriteLine("Finalizing instance.");
        Dispose();
    }

    public unsafe void ReadData()
    {
        Console.WriteLine("Calling GC.Collect...");

        // I redirected it to the local variable not to
        // use this after GC.Collect();
        var handle = _handle;

        // The imitation of full GC.Collect
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Console.WriteLine("Finished doing something.");
        var overlapped = new NativeOverlapped();

        // it is not important what we do
        ReadFileEx(handle, new byte[] { }, 0, ref overlapped, (a, b, c) => {;});
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto, BestFitMapping = false)]
    static extern IntPtr CreateFile(String lpFileName, int dwDesiredAccess, int dwShareMode,
    IntPtr securityAttrs, int dwCreationDisposition, int dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ReadFileEx(IntPtr hFile, [Out] byte[] lpBuffer, uint nNumberOfBytesToRead,
    [In] ref NativeOverlapped lpOverlapped, IOCompletionCallback lpCompletionRoutine);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr hObject);
}
```

Admit that this code looks decent more or less. Anyway, it doesn’t look like there is a problem. In fact, there is a serious problem. A class finalizer may attempt to close a file while reading it, which almost inevitably leads to an error. Because in this case the error is explicitly returned (`IntPtr == -1`) we will not see this. The `_handle` will be set to zero, the following `Dispose` will fail to close the file and the resource will leak. To solve this problem, you should use `SafeHandle`, `CriticalHandle`, `SafeBuffer` and their derived classes. Besides that these classes have counters of usage in unmanaged code, these counters also automatically increment when passing with methods' parameters to the unmanaged world and decrement when leaving it.

## Multithreading

Now let’s talk about thin ice. In the previous sections about IDisposable we touched one very important concept that underlies not only the design principles of Disposable types but any type in general. This is the object’s integrity concept. It means that at any given moment of time an object is in a strictly determined state and any action with this object turns its state into one of the variants that were pre-determined while designing a type of this object. In other words, no action with the object should turn it into an undefined state. This results in a problem with the types designed in the above examples. They are not thread-safe. There is a chance the public methods of these types will be called when the destruction of an object is in progress. Let’s solve this problem and decide whether we should solve it at all.

```csharp
public class FileWrapper : IDisposable
{
    IntPtr _handle;
    bool _disposed;
    object _disposingSync = new object();

    public FileWrapper(string name)
    {
        _handle = CreateFile(name, 0, 0, 0, 0, 0, IntPtr.Zero);
    }

    public void Seek(int position)
    {
        lock(_disposingSync)
        {
            CheckDisposed();
            // Seek API call
        }
    }

    public void Dispose()
    {
        lock(_disposingSync)
        {
            if(_disposed) return;
            _disposed = true;
        }
        InternalDispose();
        GC.SuppressFinalize(this);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CheckDisposed()
    {
        lock(_disposingSync)
        {
            if(_disposed) {
                throw new ObjectDisposedException();
            }
        }
    }

    private void InternalDispose()
    {
        CloseHandle(_handle);
    }

    ~FileWrapper()
    {
        InternalDispose();
    }

    /// other methods
}
```

The ```_disposed``` validation code in Dispose() should be initialized as a critical section. In fact, the whole code of public methods should be initialized as a critical section. This will solve the problem of concurrent access to a public method of an instance type and to a method of its destruction. However, it brings other problems that become a timebomb:

  - The intensive use of type instance methods as well as the creation and destruction of objects will lower the performance significantly. This is because taking a lock consumes time. This time is necessary to allocate SyncBlockIndex tables, check current thread and many other things (we will deal with them in the chapter about multithreading). That means we will have to sacrifice the object’s performance throughout its lifetime for the “last mile” of its life.
  - Additional memory traffic for synchronization objects.
  - Additional steps GC should take to go through an object graph.

Now, let’s name the second and, in my opinion, the most important thing. We allow the destruction of an object and at the same time expect to work with it again. What do we hope for in this situation? that it will fail? Because if Dispose runs first, then the following use of object methods will definitely result in ```ObjectDisposedException```. So, you should delegate the synchronization between Dispose() calls and other public methods of a type to the service side, i.e. to the code that created the instance of ```FileWrapper``` class. It is because only the creating side knows what it will do with an instance of a class and when to destroy it. On the other hand, a Dispose call should produce only critical errors, such as `OutOfMemoryException`, but not IOException for example. This is because of the requirements for the architecture of classes that implement IDisposable. It means that if Dispose is called from more than one thread at a time, the destruction of an entity may happen from two threads simultaneously (we skip the check of `if(_disposed) return;`). It depends on the situation: if a resource *can be* released several times, there is no need in additional checks. Otherwise, protection is necessary:

```csharp
// I don’t show the whole pattern on purpose as the example will be too long
// and will not show the essence
class Disposable : IDisposable
{
    private volatile int _disposed;

    public void Dispose()
    {
        if(Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
        {
            // dispose
        }
    }
}
```

## Two levels of Disposable Design Principle

What is the most popular pattern to implement ```IDisposable``` that you can meet in .NET books and the Internet? What pattern is expected from you during interviews for a potential new job? Most probably this one:

```csharp
public class Disposable : IDisposable
{
    bool _disposed;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if(disposing)
        {
            // here we release managed resources
        }
        // here we release unmanaged resources
    }

    protected void CheckDisposed()
    {
        if(_disposed)
        {
            throw new ObjectDisposedException();
        }
    }

    ~Disposable()
    {
        Dispose(false);
    }
}
```

What is wrong with this example and why we haven’t written like this before? In fact, this is a good pattern suitable for all situations. However, its ubiquitous use is not a good style in my opinion as we almost don’t deal with unmanaged resources in practice that makes half of the pattern serve no purpose. Moreover, since it simultaneously manages both managed and unmanaged resources, it violates the principle of responsibility division. I think this is wrong. Let’s look at a slightly different approach. *Disposable Design Principle*. In brief, it works as follows:

Disposing is divided into two levels of classes:

  - Level 0 types directly encapsulate unmanaged resources
    - They are either abstract or packed.
    - All methods should be marked:
      – PrePrepareMethod, so that a method could be compiled when loading a type
      - SecuritySafeCritical to protect against a call from the code, working under restrictions
      - ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success / MayFail)] to put CER for a method and all its child calls
  – They can reference Level 0 types, but should increment the counter of referencing objects to guarantee the right order of entering the “last mile”
  - Level 1 types encapsulate only managed resources
    - They are inherited only from Level 1 types or directly implement IDisposable 
    - They cannot inherit Level 0 types or CriticalFinalizerObject
    - They can encapsulate Level 1 and Level 0 managed types
    - They implement IDisposable.Dispose by destroying encapsulated objects starting from Level 0 types and going to Level 1
    - They don’t implement a finalizer as they don’t deal with unmanaged resources 
    - They should contain a protected property that gives access to Level 0 types.

That is why I used the division into two types from the beginning: the one that contains a managed resource and the one with unmanaged resource. They should function differently.

## Other ways to use Dispose

The idea behind the creation of IDisposable was to release unmanaged resources. But as with many other patterns it is very helpful for other tasks, e.g. to release references to managed resources. Though releasing managed resources doesn’t sound very helpful. I mean they are called managed on purpose so we would relax with a grin regarding C/C++ developers, right?  However, it is not so.  There always may be a situation where we lose a reference to an object but at the same time think that everything is OK: GC will collect garbage, including our object. However, it turns out that memory grows. We get into the memory analysis program and see that something else holds this object. The thing is that there can be a logic for implicit capture of a reference to your entity in both .NET platform and the architecture of external classes. As the capture is implicit, a programmer can miss the necessity of its release and then get a memory leak.

### Delegates, events

Let’s look at this synthetic example:

```csharp
class Secondary
{
    Action _action;

    void SaveForUseInFuture(Action action)
    {
        _action = action;
    }

    public void CallAction()
    {
        _action();
    }
}

class Primary
{
    Secondary _foo = new Secondary();

    public void PlanSayHello()
    {
        _foo.SaveForUseInFuture(Strategy);
    }

    public void SayHello()
    {
        _foo.CallAction();
    }

    void Strategy()
    {
        Console.WriteLine("Hello!");
    }
}
```

Which problem does this code show? Secondary class stores `Action` type delegate in `_action` field that is accepted in `SaveForUseInFuture` method. Next, `PlanSayHello` method inside `Primary` class passes pointer to `Strategy` method to `Secondary` class. It is curious but if, in this example, you pass somewhere a static method or an instance method, the passed `SaveForUseInFuture` will not be changed, but a `Primary` class instance will be referenced *implicitly* or not referenced at all. Outwardly it looks like you instructed which method to call. But in fact, a delegate is built not only using a method pointer but also using the pointer to an instance of a class. A calling party should understand for which instance of a class it has to call the `Strategy` method! That is the instance of `Secondary` class has implicitly accepted and holds the pointer to the instance of `Primary` class, though it is not indicated explicitly. For us it means only that if we pass `_foo` pointer somewhere else and lose the reference to `Primary`, then GC *will not collect* `Primary` object, as `Secondary` will hold it. How can we avoid such situations? We need a determined approach to release a reference to us.  A mechanism that perfectly fits this purpose is `IDisposable`

```csharp
// This is a simplified implementation
class Secondary : IDisposable
{
    Action _action;

    public event Action<Secondary> OnDisposed;

    public void SaveForUseInFuture(Action action)
    {
        _action = action;
    }

    public void CallAction()
    {
        _action?.Invoke();
    }

    void Dispose()
    {
        _action = null;
        OnDisposed?.Invoke(this);
    }
}
```

Now the example looks acceptable. If an instance of a class is passed to a third party and the reference to `_action` delegate will be lost during this process, we will set it to zero and the third party will be notified about the destruction of the instance and delete the reference to it.
The second danger of code that runs on delegates is the functioning principles of `event`.  Let’s look what they result in:

```csharp
 // a private field of a handler
private Action<Secondary> _event;

// add/remove methods are marked as [MethodImpl(MethodImplOptions.Synchronized)]
// that is similar to lock(this)
public event Action<Secondary> OnDisposed {
    add { lock(this) { _event += value; } }
    remove { lock(this) { _event -= value; } }
}
```

C# messaging hides the internals of events and holds all the objects that subscribed to update through `event`. If something goes wrong, a reference to a signed object remains in `OnDisposed` and will hold the object. It is a strange situation as in terms of architecture we get a concept of “events source” that shouldn’t hold anything logically.  But in fact, objects subscribed to update are held implicitly. In addition, we cannot change something inside this array of delegates though the entity belongs to us. The only thing we can do is to delete this list by assigning null to an events source.

The second way is to implement `add`/`remove` methods explicitly, so we could control a collection of delegates.

> Another implicit situation may appear here. It may seem that if you assign null to an events source, the following subscription to events will cause `NullReferenceException`. I think this would be more logical.

However, this is not true. If external code subscribes to events after an events source is cleared, FCL will create a new instance of Action class and store it in `OnDisposed`. This implicitness in C# can mislead a programmer: dealing with nulled fields should produce a sort of alertness rather than calmness. Here we also demonstrate an approach when the carelessness of a programmer can lead to memory leaks.

### Lambdas, closures

Using such syntactic sugar as lambdas is especially dangerous.

> I would like to touch upon syntactic sugar as a whole. I think you should use it rather carefully and only if you know the outcome exactly. Examples with lambda expressions are closures, closures in Expressions and many other miseries you can inflict upon yourself.

Of course, you may say you know that a lambda expression creates a closure and can result in a risk of resource leak. But it is so neat, so pleasant that it is hard to avoid using lambda instead of allocating the entire method, that will be described in a place different from where it will be used. In fact, you shouldn’t buy into this provocation, though not everybody can resist. Let’s look at the example:

```csharp
 button.Clicked += () => service.SendMessageAsync(MessageType.Deploy);
```

Agree, this line looks very safe. But it hides a big problem: now `button` variable implicitly references `service` and holds it. Even if we decide that we don’t need `service` anymore, `button` will still hold the reference while this variable is alive. One of the ways to solve this problem is to use a pattern for creating `IDisposable` from any `Action` (`System.Reactive.Disposables`):  

```csharp
// Here we create a delegate from a lambda
Action action = () => service.SendMessageAsync(MessageType.Deploy);

// Here we subscribe
button.Clicked += action;

// We unsubscribe
var subscription = Disposable.Create(() => button.Clicked -= action);

// where it is necessary
subscription.Dispose();
```

Admit, this looks a bit lengthy and we lose the whole purpose of using lambda expressions. It is much safer and simpler to use common private methods to capture variables implicitly.

### ThreadAbort protection

When you create a library for an third-party developer, you cannot predict its behavior in a third-party application. Sometimes you can only guess what a programmer did to your library that caused a particular outcome. One example is functioning in a multithreaded environment when the consistency of resources cleanup can become a critical issue. Note that when we write the `Dispose()` method, we can guarantee the absence of exceptions. However, we cannot ensure that while running the `Dispose()` method no `ThreadAbortException` will occur that disables our thread of execution. Here we should remember that when `ThreadAbortException` occurs, all catch/finally blocks are executed anyway (at the end of a catch/finally block ThreadAbort occurs further along). So, to ensure execution of a certain code by using Thread.Abort you need to wrap a critical section in `try { ... } finally { ... }`, see the example below:

```csharp
void Dispose()
{
    if(_disposed) return;

    _someInstance.Unsubscribe(this);
    _disposed = true;
}
```

One can abort this at any point using `Thread.Abort`. It partially destroys an object, though you can still work with it in the future. At the same time, the following code:

```csharp
void Dispose()
{
    if(_disposed) return;

    // ThreadAbortException protection
    try {}
    finally
    {
        _someInstance.Unsubscribe(this);
        _disposed = true;
    }
}
```

is protected from such an abort and will run smoothly and for sure, even if `Thread.Abort` appears between calling `Unsubscribe` method and executing its instructions.

## Results

### Advantages

Well, we learned a lot about this simplest pattern. Let’s determine its advantages:

  1. The main advantage of the pattern is the capability to release resources determinately i.e. when you need them.
  2. The second advantage is the introduction of a proven way to check if a specific instance requires to destroy its instances after using.
  3. If you implement the pattern correctly, a designed type will function safely in terms of use by third-party components as well as in terms of unloading and destroying resources when a process crashes (for example because of lack of memory). This is the last advantage.

### Disadvantages

In my opinion, this pattern has more disadvantages than advantages.

  1. On the one hand, any type that implements this pattern instructs other parts that if they use it they take a sort of public offer. This is so implicit that as in case of public offers a user of a type doesn’t always know that the type has this interface. Thus you have to follow IDE prompts (type a period, Dis.. and check if there is a method in the filtered member list of a class). If you see a Dispose pattern, you should implement it in your code. Sometimes it doesn’t happen straight away and in this case you should implement a pattern through a system of types that adds functionality. A good example is that ```IEnumerator<T>``` entails ```IDisposable```.
  2. Usually when you design an interface there is a need to insert IDisposable into the system of a type’s interfaces when one of the interfaces have to inherit IDisposable. In my opinion, this damages the interfaces we designed. I mean when you design an interface you create an interaction protocol first. This is a set of actions you can perform with *something* hidden behind the interface. `Dispose()` is a method for destroying an instance of a class. This contradicts the essence of an *interaction protocol*. In fact, these are the details of implementation that infiltrated into the interface.
  3. Despite being determined, Dispose() doesn’t mean direct destruction of an object. The object will still exist after its *destruction* but in another state. To make it true CheckDisposed() must be the first command of each public method. This looks like a temporary solution that somebody gave us saying: “Go forth and multiply”;
  4. There is also a small chance to get a type that implements ```IDisposable``` through *explicit* implementation. Or you can get a type that implements IDisposable without a chance to determine who must destroy it: you or the party that gave it to you. This resulted in an antipattern of multiple calls of Dispose() that allows to destroy a destroyed object;
  5. The complete implementation is difficult, and it is different for managed and unmanaged resources. Here the attempt to facilitate the work of developers through GC looks awkward. You can override `virtual void Dispose()` method and introduce some DisposableObject type that implements the whole pattern, but that doesn’t solve other problems connected with the pattern;
  6. As a rule Dispose() method is implemented at the end of a file while '.ctor' is declared at the beginning. If you modify a class or introduce new resources, it is easy to forget to add disposal for them.
  7. Finally, it is difficult to determine the order of *destruction* in a multithreaded environment when you use a pattern for object graphs where objects fully or partially implement that pattern. I mean situations when Dispose() can start at different ends of a graph. Here it is better to use other patterns, e.g. the Lifetime pattern.
  8. The wish of platform developers to automate memory control combined with realities: applications interact with unmanaged code very often + you need to control the release of references to objects so Garbage Collector could collect them. This adds great confusion in understanding such questions as: “How should we implement a pattern correctly”? “Is there a reliable pattern at all”? Maybe calling `delete obj; delete[] arr;` is simpler?

## Domain unloading and exit from an application

If you got to this part, you became more confident in the success of future job interviews. However, we didn’t discuss all the questions connected with this simple, as it may seem, pattern. The last question is whether the behavior of an application differs in case of simple garbage collection and when garbage is collected during domain unloading and while exiting the application. This question merely touches upon `Dispose()`... However `Dispose()` and finalization go hand in hand and we rarely meet an implementation of a class which has finalization but doesn't have `Dispose()` method. So, let’s describe finalization in a separate section. Here we just add a few important details.

During application domain unloading you unload both assemblies loaded into the application domain and all objects that were created as part of the domain to be unloaded. In fact, this means the cleanup (collection by GC) of these objects and calling finalizers for them. If the logic of a finalizer waits for finalization of other objects to be destroyed in the right order, you may pay attention to `Environment.HasShutdownStarted` property indicating that an application is unloaded from memory and to `AppDomain.CurrentDomain.IsFinalizingForUnload()` method indicating that this domain is unloaded which is the reason for finalization. If these events occur the order of resources finalization generally becomes unimportant. We cannot delay either the unloading of domain or an application as we should do everything as quickly as possible.

This is the way this task is solved as part of a class [LoaderAllocatorScout](http://referencesource.microsoft.com/#mscorlib/system/reflection/loaderallocator.cs,25551b0f6db5f579)

```csharp
// Assemblies and LoaderAllocators will be cleaned up during AppDomain shutdown in
// an unmanaged code
// So it is ok to skip reregistration and cleanup for finalization during appdomain shutdown.
// We also avoid early finalization of LoaderAllocatorScout due to AD unload when the object was inside DelayedFinalizationList.
if (!Environment.HasShutdownStarted &&
    !AppDomain.CurrentDomain.IsFinalizingForUnload())
{
    // Destroy returns false if the managed LoaderAllocator is still alive.
    if (!Destroy(m_nativeLoaderAllocator))
    {
        // Somebody might have been holding a reference on us via weak handle.
        // We will keep trying. It will be hopefully released eventually.
        GC.ReRegisterForFinalize(this);
    }
}
```

## Typical implementation faults

As I showed you there is no universal pattern to implement IDisposable. Moreover, some reliance on automatic memory control misleads people and they make confusing decisions when implementing a pattern. The whole .NET Framework is riddled with errors in its implementation. To prove my point, let’s look at these errors using the example of .NET Framework exactly. All implementations are available via: [IDisposable Usages](http://referencesource.microsoft.com/#mscorlib/system/idisposable.cs,1f55292c3174123d,references)

**FileEntry Class** [cmsinterop.cs](http://referencesource.microsoft.com/#mscorlib/system/deployment/cmsinterop.cs,eeedb7095d7d3053,references)

> This code is written in a hurry just to close the issue. Obviously, the author wanted to do something but changed their mind and kept a flawed solution

```csharp
internal class FileEntry : IDisposable
{
    // Other fields
    // ...
    [MarshalAs(UnmanagedType.SysInt)] public IntPtr HashValue;
    // ...

    ~FileEntry()
    {
        Dispose(false);
    }

    // The implementation is hidden and complicates calling the *right* version of a method.
    void IDisposable.Dispose() { this.Dispose(true); }

    // Choosing a public method is a serious mistake that allows for incorrect destruction of
    // an instance of a class. Moreover, you CANNOT call this method from the outside
    public void Dispose(bool fDisposing)
    {
        if (HashValue != IntPtr.Zero)
        {
            Marshal.FreeCoTaskMem(HashValue);
            HashValue = IntPtr.Zero;
        }

        if (fDisposing)
        {
            if( MuiMapping != null)
            {
                MuiMapping.Dispose(true);
                MuiMapping = null;
            }

            System.GC.SuppressFinalize(this);
        }
    }
}
```

**SemaphoreSlim Class** [System/Threading/SemaphoreSlim.cs](https://github.com/dotnet/coreclr/blob/cbcdbd25e74ff9d963eafa202dd63504ca537f7e/src/mscorlib/src/System/Threading/SemaphoreSlim.cs)

> This error is in the top of errors of .NET Framework regarding IDisposable: SuppressFinalize for classes where there is no finalizer. It is very common.

```csharp
public void Dispose()
{
    Dispose(true);

    // As the class doesn’t have a finalizer, there is no need in GC.SuppressFinalize
    GC.SuppressFinalize(this);
}

// The implementation of this pattern assumes the finalizer exists. But it doesn’t.
// It was possible to do with just public virtual void Dispose()
protected virtual void Dispose(bool disposing)
{
    if (disposing)
    {
        if (m_waitHandle != null)
        {
            m_waitHandle.Close();
            m_waitHandle = null;
        }
        m_lockObj = null;
        m_asyncHead = null;
        m_asyncTail = null;
    }
}
```

**Calling Close+Dispose** [Some NativeWatcher project code](https://github.com/alexguirre/NativeWatcher/blob/7208d463c41a709f29c60264bc518c6c0c5713cc/NativeWatcher/Forms/FormsManager.cs)

> Sometimes people call both Close and Dispose. This is wrong though it will not produce an error as the second Dispose doesn’t generate an exception.

In fact, Close is another pattern to make things clearer for people. However, it made everything more unclear.

```csharp
public void Dispose()
{
    if (MainForm != null)
    {
        MainForm.Close();
        MainForm.Dispose();
    }
    MainForm = null;
}
```

## General results

  1. IDisposable is a standard of the platform and the quality of its implementation influences the quality of the whole application. Moreover, in some situation it influences the safety of your application that can be attacked via unmanaged resources.
  2. The implementation of IDisposable must be maximally productive. This is especially true about the section of finalization, that works in parallel with the rest of code, loading Garbage Collector.
  3. When implementing IDisposable you shouldn't use Dispose() simultaneously with public methods of a class. The destruction cannot go along with usage. This should be considered when designing a type that will use IDisposable object.
  4. However, there should be a protection against calling ‘Dispose()’ from two threads simultaneously. This results from the statement that Dispose() shouldn’t produce errors.
  5. Types that contain unmanaged resources should be separated from other types. I mean if you wrap an unmanaged resource, you should allocate a separate type for it. This type should contain finalization and should be inherited from `SafeHandle / CriticalHandle / CriticalFinalizerObject`. This separation of responsibility will result in improved support of the type system and will simplify the implementation to destroy instances of types via Dispose(): the types with this implementation won't need to implement a finalizer.
  6. In general, this pattern is not comfortable in use as well as in code maintenance. Probably, we should use Inversion of Control approach when we destroy the state of objects via `Lifetime` pattern. However, we will talk about it in the next section.

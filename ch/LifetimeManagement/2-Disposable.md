![IDisposable cover](./imgs/Disposable-Cover.png)

# 一次性模式（一次性设计原则）

> [讨论链接](https://github.com/sidristij/dotnetbook/issues/54)

我想几乎所有使用.NET的程序员现在都会说这种模式是小菜一碟。这是平台上使用的最着名的模式。然而，即使是最简单和众所周知的问题域也会有你从未看过的秘密区域。所以，让我们从一开始就为第一次和其他所有人描述整个事情（这样你们每个人都能记住基础知识）。不要跳过这些段落 - 我在看着你！

## IDisposable

如果我问什么是IDisposable，你肯定会说它是

```csharp
public interface IDisposable
{
    void Dispose();
}
```

界面的目的是什么？我的意思是，如果我们有一个智能垃圾收集器清除内存而不是我们，为什么我们需要清理内存，所以我们甚至不必考虑它。但是，有一些小细节。

有一种误解，“`IDisposable```用于释放非托管资源。这只是部分正确，要理解它，您只需要记住非托管资源的示例。```File```类是非托管资源吗？不，也许````DbContext```是一个非托管资源？不，再说一次。非托管资源不属于.NET类型系统。平台没有创建的东西，存在于其范围之外的东西。一个简单的例子是操作系统中打开的文件句柄。句柄是一个数字，用于唯一标识操作系统打开的文件 - 不，不是您。也就是说，所有控制结构（例如文件系统中文件的位置，碎片和其他服务信息中的文件碎片，圆柱体的数量，硬盘的头部或扇区）在操作系统内，但不在.NET平台内。传递给.NET平台的唯一非托管资源是IntPtr编号。这个数字由FileSafeHandle包装，FileSafeHandle由File类包装。这意味着File类本身不是非托管资源，但使用IntPtr形式的附加层来包含非托管资源 - 打开文件的句柄。你怎么看那个文件？在WinAPI或Linux OS中使用一组方法。你怎么看那个文件？在WinAPI或Linux OS中使用一组方法。你怎么看那个文件？在WinAPI或Linux OS中使用一组方法。

多线程或多处理器程序中的同步原语是非托管资源的第二个示例。这里属于通过P/Invoke传递的数据数组以及互斥锁或信号量。

> 请注意，操作系统不会简单地将非托管资源的句柄传递给应用程序。它还将该句柄保存在由该进程打开的句柄表中。因此，OS可以在应用程序终止后正确关闭资源。这可确保在退出应用程序后无论如何都会关闭资源。但是，应用程序的运行时间可能不同，这可能导致长时间的资源锁定。

好。现在我们介绍了非托管资源。为什么我们需要在这些情况下使用IDisposable？因为.NET Framework不知道在其领土之外发生了什么。如果使用OS API打开文件，.NET将不知道它。如果为自己的需要分配内存范围（例如使用VirtualAlloc），.NET也一无所知。如果它不知道，它将不会释放VirtualAlloc调用占用的内存。或者，它不会关闭通过OS API调用直接打开的文件。这些可能会导致不同的意外后果。如果在不释放内存的情况下分配太多内存（例如，只需将指针设置为null），就可以获得OutOfMemory。或者，如果您通过操作系统在文件共享上打开文件而不关闭它，则会长时间锁定该文件共享上的文件。文件共享示例特别好，因为即使关闭与服务器的连接，锁也将保留在IIS端。您无权释放锁定，您必须要求管理员执行`iisreset`或使用特殊软件手动关闭资源。
远程服务器上的此问题可能成为要解决的复杂任务。

所有这些情况都需要在类型系统和程序员之间使用通用且熟悉的 _交互协议_。

因此，有两种标准方法可以调用它。通常，您可以创建实体实例，以便在一个方法内或在实体实例的生命周期内快速使用它。

第一种方法是将一个实例包装成```using（...）{...}```。这意味着您指示在使用相关块结束后销毁对象，即调用Dispose（）。第二种方法是在对象生命周期结束时通过引用我们想要释放的对象来销毁它。但.NET只有一个暗示自动销毁对象的终结方法，对吗？但是，最终确定并不合适，因为我们不知道什么时候会被调用。同时，我们需要在特定时间释放一个对象，例如在我们使用打开的文件完成工作之后。这就是为什么我们还需要实现IDisposable并调用Dispose来释放我们拥有的所有资源。因此，我们遵循_protocol_，这非常重要。因为如果有人遵循它，所有参与者都应该这样做以避免问题。

## 实现IDisposable的不同方法

让我们看一下IDisposable从简单到复杂的实现。第一个也是最简单的是使用IDisposable：

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

在这里，我们创建一个由Dispose（）进一步释放的资源实例。使这个实现不一致的唯一原因是你仍然可以通过```Dispose（）```来破坏实例：

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

必须将CheckDisposed（）作为类的所有公共方法中的第一个表达式调用。获得的`ResourceHolder`类结构看起来很好地破坏了非托管资源，即`DisposableResource`。但是，此结构不适用于包装的非托管资源。让我们看一下非托管资源的示例。

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

最后两个例子的行为有什么不同？第一个描述了两个托管资源的交互。这意味着如果程序正常工作，无论如何都会释放资源。由于```DisposableResource```被管理，.NET CLR知道它并且如果它的行为不正确将从它释放内存。请注意，我有意识地不假设```DisposableResource```类型封装。可以有任何逻辑和结构。它可以包含托管和非托管资源。*这根本不应该关注我们*。没有人要求我们每次都反编译第三方的库，看看他们是使用托管资源还是非托管资源。如果*我们的类型*使用非托管资源，我们不能不知道这一点。我们在```FileWrapper```类中执行此操作。所以，在这种情况下会发生什么？如果我们使用非托管资源，我们有两种情况。第一个是当一切正常并调用Dispose时。第二个是出现问题时Dispose失败。

让我们马上说出为什么会出错：

  - 如果我们使用```using（obj）{...}```，则内部代码块中可能会出现异常。这个异常是由```finally```块捕获的，我们看不到（这是C＃的语法糖）。该块隐式调用Dispose。但是，有些情况下不会发生这种情况。例如，既没有```catch```也没有```finally```捕获```StackOverflowException```。你应该永远记住这一点。因为如果某些线程变得递归并且某些时候发生了```StackOverflowException```，.NET将忘记它使用但未释放的资源。它不知道如何释放非托管资源。它们将保留在内存中，直到OS释放它们，即退出程序时，或者甚至在应用程序终止后的某个时间。
  - 如果我们从另一个Dispose（）调用Dispose（）。同样，我们可能碰巧没有达到目的。这不是一个心不在焉的应用程序开发人员，他忘了调用Dispose（）。这是例外的问题。但是，这些不仅是导致应用程序线程崩溃的异常。这里我们讨论所有会阻止算法调用将调用我们的Dispose（）的外部Dispose（）的异常。

所有这些情况都将创建暂停的非托管资源。那是因为垃圾收集器不知道它应该收集它们。它在下次检查时可以做的就是发现最后一个带有```FileWrapper```类型的对象图的引用丢失了。在这种情况下，将为具有引用的对象重新分配内存。我们怎样才能防止它呢？

我们必须实现一个对象的终结器。'终结者'是故意以这种方式命名的。它可能看起来不像析构函数，因为类似的方法在C＃中调用终结器和在C ++中调用析构函数。区别在于终结器将被调用*无论如何*，与析构函数（以及```Dispose（）```相反）。启动垃圾收集时会调用终结器（现在已经足够了解它，但事情有点复杂）。如果*出现问题*，它用于保证资源的释放。我们必须*实现终结器来释放非托管资源。同样，因为在GC启动时调用终结器，我们不知道何时发生这种情况。

让我们扩展我们的代码：

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

我们使用有关最终化过程的知识增强了示例，并且如果未调用Dispose（），则保护应用程序不会丢失资源信息。如果成功调用Dispose（），我们还调用GC.SuppressFinalize来禁用该类型实例的最终化。没有必要两次发布相同的资源，对吗？因此，我们还通过放弃一个随机区域的代码来减少终结队列，这个代码区域很可能在一段时间之后并行完成。现在，让我们进一步增强这个例子。

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

现在，我们封装非托管资源的类型示例看起来很完整。不幸的是，第二个```Dispose（）```实际上是平台的标准，我们允许调用它。注意，人们经常允许第二次调用```Dispose（）```来避免调用代码的问题，这是错误的。但是，您的库的用户查看MS文档可能不这么认为，并允许多次调用Dispose（）。调用其他公共方法无论如何都会破坏对象的完整性。如果我们破坏了对象，我们就不能再使用它了。这意味着我们必须在每个公共方法的开头调用```CheckDisposed```。

但是，此代码包含严重问题，导致其无法按预期工作。如果我们记得垃圾收集的工作原理，我们会注意到一个功能。收集垃圾时，GC *主要*完成直接从* Object *继承的所有内容。接下来，它处理实现* CriticalFinalizerObject *的对象。这成为一个问题，因为我们设计的两个类都继承了Object。我们不知道他们将以何种顺序来到“最后一英里”。但是，更高级别的对象可以使用其终结器来使用非托管资源完成对象。虽然，这听起来不是一个好主意。最终确定的顺序在这里非常有用。要设置它，具有封装的非托管资源的低级类型必须从`CriticalFinalizerObject`继承。

第二个原因更为深刻。想象一下，你敢于编写一个不需要太多记忆的应用程序。它大量分配内存，没有兑现和其他细微之处。有一天，此应用程序将与OutOfMemoryException崩溃。当它发生时，代码专门运行。它不能分配任何东西，因为它会导致重复的异常，即使第一个异常被捕获。这并不意味着我们不应该创建新的对象实例。即使是简单的方法调用也可能抛出此异常，例如终结。我提醒你，第一次调用方法时会编译方法。这是通常的行为。我们怎样才能防止这个问题？很容易。如果您的对象是从* CriticalFinalizerObject *继承的，则此类型的* all *方法将在将其加载到内存后立即编译。

为什么这很重要？为什么要对那些过世的人花费太多精力？因为非托管资源可以在系统中暂停很长时间。即使您重新启动计算机后。如果用户从应用程序中的文件共享中打开文件，则前者将被远程主机锁定，并在超时时释放，或者通过关闭文件释放资源时释放。如果您的应用程序在打开文件时崩溃，即使重启后也不会释放。您将不得不等待很长时间，直到远程主机释放它。此外，您不应该在终结器中允许例外。这会导致CLR和应用程序的加速崩溃，因为您无法在* try .. catch *中包装终结器的调用。我的意思是，当您尝试释放资源时，您必须确保它可以被释放。最后但并非不太重要的事实：

## SafeHandle / CriticalHandle / SafeBuffer / 派生类型

我觉得我打算为你打开潘多拉的盒子。我们来谈谈特殊类型：SafeHandle，CriticalHandle及其派生类型。
这是关于提供对非托管资源的访问的类型模式的最后一件事。但首先，让我们列出我们从非托管世界获得的所有内容：

  - 第一个也很明显的是手柄。对于.NET开发人员来说，这可能是一个毫无意义的词，但它是操作系统世界中非常重要的组成部分。句柄本质上是32位或64位数。它指定与操作系统交互的打开会话。例如，当您打开文件时，您将获得WinApi函数的句柄。然后你可以使用它并执行 *Seek*，*Read* 或 *Write* 操作。或者，您可以打开一个用于网络访问的套接字。操作系统将再次传递给您一个句柄。在.NET中，句柄存储为 *IntPtr* 类型;
  - 第二件事是数据阵列。您可以通过不安全的代码处理非托管数组（此处不安全是关键词）或使用SafeBuffer将数据缓冲区包装到合适的.NET类中。请注意，第一种方式更快（例如，您可以大大优化循环），但第二种方式更安全，因为它基于SafeHandle;
  - 然后去串。字符串很简单，因为我们需要确定我们捕获的字符串的格式和编码。它然后被复制给我们（一个字符串是一个不可变的类），我们不再担心它了。
  - 最后一件事是刚刚复制的ValueTypes，所以我们根本不需要考虑它们。

SafeHandle是一个特殊的.NET CLR类，它继承了CriticalFinalizerObject，并且应该以最安全和最舒适的方式包装操作系统的句柄。

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

要理解从SafeHandle派生的类的有用性，您需要记住为什么.NET类型如此之大：GC可以自动收集它们的实例。在管理SafeHandle时，它包装的非托管资源会继承托管环境的所有特征。它还包含CLR无法使用的外部引用的内部计数器。我指的是来自不安全代码的引用。您根本不需要手动递增或递减计数器。当您声明从SafeHandle派生的类型作为不安全方法的参数时，计数器在输入该方法时递增或在退出后递减。原因是当你通过传递一个句柄去一个不安全的代码时，你可能会得到GC收集的这个SafeHandle，通过在另一个线程中重置对此句柄的引用（如果您处理来自多个线程的一个句柄）。使用引用计数器可以更轻松地工作：在计数器归零之前，不会创建SafeHandle。这就是您不需要手动更改计数器的原因。或者，您应该尽可能小心地返回它。

引用计数器的第二个目的是设置相互引用的```CriticalFinalizerObject```的完成顺序。如果一个基于SafeHandle的类型引用另一个，则需要在引用类型的构造函数中另外增加引用计数器，并减少ReleaseHandle方法中的计数器。因此，您的对象将一直存在，直到您的对象引用的对象不被销毁。但是，最好避免这种困惑。让我们使用关于SafeHandlers的知识并编写我们类的最终变体：

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

它有什么不同？如果你将**任何**基于SafeHandle的类型（包括你自己的）设置为DllImport方法中的返回值，那么Marshal将正确创建并初始化此类型并将计数器设置为1.知道这一点我们将SafeFileHandle类型设置为CreateFile内核函数的返回类型。当我们得到它时，我们将完全使用它来调用ReadFile和WriteFile（作为计数器值在调用时递增，在退出时递减将确保句柄在读取和写入文件时仍然存在）。这是一个设计正确的类型，如果线程被中止，它将可靠地关闭文件句柄。这意味着我们不需要实现自己的终结器和与之相关的所有内容。整个类型简化了。

### 在实例方法工作时执行终结器

在垃圾收集期间使用一种优化技术，旨在在更短的时间内收集更多对象。我们来看看以下代码：

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

一方面，代码看起来很安全，而且我们为什么要关心并不清楚。但是，如果您记得有类包装非托管资源，您将理解错误设计的类可能会导致非托管世界的异常。此异常将报告以前获取的句柄未处于活动状态：

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

承认这个代码或多或少看起来不错。无论如何，它看起来不像是有问题。事实上，存在严重的问题。类终结器可能会在读取文件时尝试关闭文件，这几乎不可避免地导致错误。因为在这种情况下显式返回错误（`IntPtr == -1`），我们不会看到这个。`_handle`将设置为零，以下`Dispose`将无法关闭文件，资源将泄漏。要解决这个问题，你应该使用`SafeHandle`，`CriticalHandle`，`SafeBuffer`及其派生类。除了这些类在非托管代码中具有使用计数器之外，这些计数器在将方法的参数传递给非托管世界时也自动递增，而在离开时递减。

## 多线程

现在让我们来谈谈薄冰吧。在前面关于IDisposable的部分中，我们触及了一个非常重要的概念，它不仅是一次性类型的设计原则，而且是一般类型的基础。这是对象的完整性概念。这意味着在任何给定的时刻，对象处于严格确定的状态，并且具有该对象的任何动作将其状态变为在设计该对象的类型时预先确定的变体之一。换句话说，对象的任何操作都不应将其转换为未定义状态。这导致上述示例中设计的类型的问题。它们不是线程安全的。当对象的销毁正在进行时，有可能会调用这些类型的公共方法。让我们解决这个问题，并决定是否应该解决它。

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

Dispose（）中的``_disposed```验证码应初始化为临界区。事实上，公共方法的整个代码应该初始化为一个关键部分。这将解决并发访问实例类型的公共方法及其销毁方法的问题。然而，它带来了其他成为定时炸弹的问题：

  - 密集使用类型实例方法以及创建和销毁对象将显着降低性能。这是因为拿锁会消耗时间。这次是分配SyncBlockIndex表，检查当前线程和许多其他事情所必需的（我们将在关于多线程的章节中处理它们）。这意味着我们必须在其生命的“最后一英里”的整个生命周期中牺牲对象的表现。
  - 同步对象的额外内存流量。
  - GC应该采取的额外步骤来完成对象图。

现在，让我们说出第二个，在我看来，这是最重要的事情。我们允许销毁一个对象，同时希望再次使用它。在这种情况下我们希望什么？它会失败吗？因为如果首先运行Dispose，那么以下对象方法的使用肯定会导致```ObjectDisposedException```。因此，您应该将Dispose（）调用和类型的其他公共方法之间的同步委托给服务端，即创建```FileWrapper```类实例的代码。这是因为只有创建方知道它将如何处理类的实例以及何时销毁它。另一方面，Dispose调用应该只产生严重错误，例如`OutOfMemoryException`，但不会产生IOException。这是因为对实现IDisposable的类的体系结构的要求。这意味着如果一次从多个线程调用Dispose，则实体的销毁可能同时发生在两个线程中（我们跳过检查`if（_disposed）return;`）。这取决于具体情况：如果资源*可以多次*释放，则无需进行额外检查。否则，保护是必要的：

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

## 两个级别的一次性设计原则

在.NET书籍和互联网上可以实现的```IDisposable```最流行的模式是什么？在为潜在的新工作进行面试时，您期望从中获得什么样的模式？最有可能是这一个：

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

这个例子有什么问题，为什么我们之前没有这样写过？事实上，这是一个适合所有情况的好模式。然而，在我看来，它的普遍使用并不是一种好的风格，因为我们几乎没有在实践中处理非托管资源，这使得一半的模式没有用处。此外，由于它同时管理托管和非托管资源，因此违反了责任分工原则。我认为这是错误的。让我们看一个略有不同的方法。*一次性设计原则*。简而言之，它的工作原理如下：

处置分为两个级别：

  - 级别0类型直接封装非托管资源
    - 它们是抽象的或包装的。
    - 应标记所有方法：
      - PrePrepareMethod，以便在加载类型时可以编译方法
      - SecuritySafeCritical，以防止来自代码的调用，在限制下工作
      - ReliabilityContract（Consistency.WillNotCorruptState，Cer.Success / MayFail）]为方法及其所有子调用放置CER
  - 它们可以引用0级类型，但应增加引用对象的计数器以保证进入“最后一英里”的正确顺序
  - 级别1类型仅封装受管资源
    - 它们仅从Level 1类型继承或直接实现IDisposable 
    - 它们不能继承Level 0类型或CriticalFinalizerObject
    - 它们可以封装1级和0级托管类型
    - 它们通过从Level 0类型开始销毁封装对象并转到Level 1来实现IDisposable.Dispose
    - 他们没有实现终结器，因为他们不处理非托管资源 
    - 它们应包含一个受保护的属性，可以访问Level 0类型。

这就是为什么我从一开始就将分区用于两种类型：一种包含托管资源，另一种包含非托管资源。它们的功能应该不同。

## 使用Dispose的其他方法

创建IDisposable背后的想法是释放非托管资源。但与许多其他模式一样，它对其他任务非常有用，例如，释放对托管资源的引用。虽然发布托管资源听起来不是很有帮助。我的意思是他们被称为有目的管理，所以我们会对C / C ++开发人员咧嘴一笑，对吧？但事实并非如此。总会有一种情况，我们失去对一个对象的引用，但同时认为一切正常：GC将收集垃圾，包括我们的对象。但事实证明，内存增长了。我们进入内存分析程序，看到其他东西保存了这个对象。问题是，在.NET平台和外部类的体系结构中，可以存在隐式捕获实体引用的逻辑。

### 代表，活动

让我们看看这个合成的例子：

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

这段代码显示哪个问题？辅助类在`_action`字段中存储`Action`类型委托，在`SaveForUseInFuture`方法中接受。接下来，`Primary`类中的`PlanSayHello`方法将指向`Strategy`方法的指针传递给`Secondary`类。这很奇怪但是，如果在这个例子中，你传递一个静态方法或一个实例方法，传递的`SaveForUseInFuture`将不会被改变，但是`Primary`类实例将被隐式引用或根本不被引用。从外表看起来你指示要调用哪种方法。但实际上，委托不仅使用方法指针而且还使用指向类实例的指针。主叫方应该了解它必须调用“策略”方法的类的哪个实例！这是`Secondary`类的实例已隐式接受并保存指向`Primary`类实例的指针，尽管未明确指出。对我们来说，这意味着只有当我们在其他地方传递`_foo`指针而失去对`Primary`的引用时，GC *才会收集*`Primary`对象，因为`Secondary`将保留它。我们怎样才能避免这种情况呢？我们需要一种坚定的方法来释放对我们的引用。完全符合这一目的的机制是`IDisposable`

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

现在这个例子看起来可以接 如果一个类的实例被传递给第三方并且在此过程中对`_action`委托的引用将丢失，我们将其设置为零，并且将通知第三方有关实例的销毁并删除该引用它。
代表运行代码的第二个危险是`event`的功能原则。让我们来看看它们的结果：

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

C＃messaging隐藏了事件的内部，并保存了所有订阅通过`event`更新的对象。如果出现问题，对已签名对象的引用仍保留在`OnDisposed`中并保留对象。这是一种奇怪的情况，因为在架构方面我们得到了一个“事件源”的概念，它不应该在逻辑上保持任何东西。但事实上，订阅更新的对象是隐含的。此外，虽然实体属于我们，但我们无法更改此代理数组中的某些内容。我们唯一能做的就是通过为事件源分配null来删除此列表。

第二种方法是显式实现`add` /`remove`方法，这样我们就可以控制一个委托集合。

> 这里可能会出现另一种隐含情况。看起来如果将null赋给事件源，以下对事件的订阅将导致`NullReferenceException`。我认为这更符合逻辑。

但是，事实并非如此。如果外部代码在清除事件源之后订阅事件，FCL将创建一个Action类的新实例并将其存储在`OnDisposed`中。C＃中的这种隐含性会误导程序员：处理空字段应该产生一种警觉而不是冷静。在这里，我们还演示了一种方法，当程序员的粗心大意导致内存泄漏时。

### Lambda，闭包

使用像lambda这样的语法糖特别危险。

> 我想谈谈整个语法糖。我认为你应该谨慎使用它，并且只有在你完全了解结果的情况下才能使用它。lambda表达式的示例包括闭包，表达式中的闭包以及您可以对自己造成的许多其他错误。

当然，您可能会说您知道lambda表达式会创建一个闭包，并可能导致资源泄漏的风险。但它是如此简洁，令人愉快，以至于很难避免使用lambda而不是分配整个方法，这将在不同于它将被使用的地方进行描述。事实上，你不应该接受这种挑衅，尽管不是每个人都能抗拒。我们来看看这个例子：

```csharp
 button.Clicked += () => service.SendMessageAsync(MessageType.Deploy);
```

同意，这条线看起来很安全。但它隐藏了一个大问题：现在`button`变量隐式引用`service`并保留它。即使我们决定不再需要`service`，当这个变量存活时，`button`仍将保持引用。解决这个问题的方法之一是使用一个模式从任何`Action`（`System.Reactive.Disposables`）创建`IDisposable`：

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

承认，这看起来有点冗长，我们失去了使用lambda表达式的全部目的。使用通用私有方法隐式捕获变量更安全，更简单。

### ThreadAbort保护

为第三方开发人员创建库时，无法预测其在第三方应用程序中的行为。有时您只能猜测程序员对您的库所做的事情会导致特定的结果。一个例子是在多线程环境中运行，当资源清理的一致性成为关键问题时。注意，当我们编写`Dispose（）`方法时，我们可以保证不存在异常。但是，我们无法确保在运行`Dispose（）`方法时不会出现“ThreadAbortException”，这会禁用我们的执行线程。在这里我们应该记住，当发生`ThreadAbortException`时，无论如何都会执行所有catch / finally块（在catch / finally块的末尾ThreadAbort进一步发生）。因此，要确保使用Thread执行某些代码。

```csharp
void Dispose()
{
    if(_disposed) return;

    _someInstance.Unsubscribe(this);
    _disposed = true;
}
```

可以使用`Thread.Abort`在任何时候中止这一点。它会部分地破坏一个对象，尽管你将来仍然可以使用它。同时，以下代码：

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

即使在调用`Unsubscribe`方法和执行其指令之间出现`Thread.Abort`，也可以保护它免受这种中止的影响，并且可以顺利运行。

## 结果

### 好处

好吧，我们学到了很多关于这个最简单的模式。让我们确定它的优点：

  1.模式的主要优点是能够确定地释放资源，即在您需要时。
  2.第二个优点是引入了一种经过验证的方法来检查特定实例是否需要在使用后销毁其实例。
  3.如果正确实现模式，设计类型将在第三方组件的使用方面安全地运行，以及在进程崩溃时卸载和销毁资源（例如由于内存不足）。这是最后一个优势。

### 缺点

在我看来，这种模式比缺点更有缺点。

  1. 一方面，任何实现此模式的类型都指示其他部分，如果他们使用它，他们会采取某种公开要约。这是隐含的，因为在公开提供的情况下，类型的用户并不总是知道该类型具有该接口。因此，您必须按照IDE提示（键入句点，Dis ..并检查类的筛选成员列表中是否存在方法）。如果您看到Dispose模式，则应在代码中实现它。有时它不会立即发生，在这种情况下，您应该通过添加功能的类型系统实现模式。一个很好的例子是```IEnumerator <T>```需要```IDisposable```。
  2. 通常在设计接口时，当其中一个接口必须继承IDisposable时，需要将IDisposable插入到类型接口的系统中。 在我看来，这会破坏我们设计的界面。 我的意思是，当您设计界面时，首先要创建交互协议。 这是您可以使用隐藏在界面后面的内容执行的一组操作。 `Dispose（）`是一种用于销毁类实例的方法。 这与交互协议的本质相矛盾。 实际上，这些是渗透到界面中的实现细节。
  3. 尽管被确定，Dispose（）并不意味着直接破坏一个物体。该对象在* destroy *之后仍然存在，但在另一个状态下。要使其成立，CheckDisposed（）必须是每个公共方法的第一个命令。这看起来像是一个临时的解决方案，有人给我们说：“前进和繁衍”;
  4. 通过* explicit *实现获得一个实现```IDisposable```的类型的机会也很小。或者你可以得到一个实现IDisposable的类型，但没有机会确定谁必须销毁它：你或者给你的派对。这导致了Dispose（）的多次调用的反模式，允许销毁被破坏的对象;
  5. 完整的实现很困难，托管和非托管资源也不同。在这里，通过GC促进开发人员工作的尝试看起来很尴尬。您可以覆盖`virtual void Dispose（）`方法并引入一些实现整个模式的DisposableObject类型，但这并不能解决与模式相关的其他问题;
  6. 通常，Dispose（）方法在文件末尾实现，而'.ctor'在开头声明。如果修改类或引入新资源，很容易忘记为它们添加处理。
  7. 最后，当对象完全或部分实现该模式的对象图使用模式时，很难确定多线程环境中* destruction *的顺序。我的意思是Dispose（）可以从图的不同端开始。这里最好使用其他模式，例如Lifetime模式。
  8. 平台开发人员希望自动化内存控制与现实相结合：应用程序经常与非托管代码交互+您需要控制对象引用的释放，以便垃圾收集器可以收集它们。这在理解诸如“我们应该如何正确实现模式”这样的问题时增加了很多困惑？“有可靠的模式”吗？也许叫`delete obj; 删除[] arr;`更简单？

## 域名卸载并退出应用程序

如果你完成这一部分，你对未来的面试成功会更有信心。但是，我们并未讨论与这种简单相关的所有问题，如图所示。最后一个问题是，在简单的垃圾收集和在域卸载期间以及退出应用程序时收集垃圾时，应用程序的行为是否不同。这个问题只涉及`Dispose（）`...但是`Dispose（）`和finalization齐头并进，我们很少遇到一个已经完成但没有`Dispose（）`方法的类的实现。所以，让我们在单独的部分中描述最终确定。在这里，我们只添加一些重要细节。

在应用程序域卸载期间，卸载加载到应用程序域中的程序集以及作为要卸载的域的一部分创建的所有对象。实际上，这意味着对这些对象进行清理（通过GC进行收集）并为它们调用终结器。如果终结器的逻辑等待以正确的顺序销毁其他对象的完成，您可以注意`Environment.HasShutdownStarted`属性，指示应用程序从内存中卸载到`AppDomain.CurrentDomain.IsFinalizingForUnload()`指示此域已卸载的方法，这是最终确定的原因。如果发生这些事件，资源最终确定的顺序通常变得不重要。我们不能延迟卸载域或应用程序，因为我们应该尽快完成所有操作。

这是此任务作为类[LoaderAllocatorScout]（http://referencesource.microsoft.com/#mscorlib/system/reflection/loaderallocator.cs,25551b0f6db5f579）的一部分解决的方式

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

## 典型的实现错误

正如我向您展示的那样，没有通用模式来实现IDisposable。此外，一些对自动内存控制的依赖会误导人们，并且在实现模式时会做出令人困惑的决定。整个.NET Framework在其实现中充满了错误。为了证明我的观点，让我们准确地使用.NET Framework的例子来看看这些错误。所有实现都可通过以下方式获得：[IDisposable Usages](http://referencesource.microsoft.com/#mscorlib/system/idisposable.cs,1f55292c3174123d,references)

**FileEntry Class** [cmsinterop.cs](http://referencesource.microsoft.com/#mscorlib/system/deployment/cmsinterop.cs,eeedb7095d7d3053,references)

> 这段代码写得很匆忙，只是为了解决这个问题。显然，作者想要做一些事情，但改变主意并保持一个有缺陷的解决方案

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

> 此错误位于.NET Framework关于IDisposable的错误的顶部：对于没有终结器的类，SuppressFinalize。这很常见。

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

> 有时人们会同时关闭和处理。这是错误的，虽然它不会产生错误，因为第二个Dispose不会产生异常。

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

## 一般结果

  1. IDisposable是平台的标准，其实施的质量会影响整个应用程序的质量。此外，在某些情况下，它会影响应用程序的安全性，可以通过非托管资源进行攻击。
  2. IDisposable的实施必须最大限度地提高效率。对于最终化部分尤其如此，它与其余代码并行工作，加载垃圾收集器。
  3. 实现IDisposable时，不应将Dispose（）与类的公共方法同时使用。破坏不能与使用同时进行。在设计将使用IDisposable对象的类型时应考虑这一点。
  但是，应该保护不要同时从两个线程调用`Dispose（）`。这是因为Dispose（）不应该产生错误。
  5. 包含非托管资源的类型应与其他类型分开。我的意思是如果你包装一个非托管资源，你应该为它分配一个单独的类型。此类型应包含finalization，并应继承自`SafeHandle / CriticalHandle / CriticalFinalizerObject`。这种责任分离将导致类型系统的改进支持，并将通过Dispose（）简化实现以销毁类型的实例：具有此实现的类型将不需要实现终结器。
  6. 一般来说，这种模式在使用和代码维护方面都不舒服。当我们通过`Lifetime`模式破坏对象的状态时，我们应该使用Inversion of Control方法。但是，我们将在下一节中讨论它。
# Memory\<T> and Span\<T>

> [讨论链接](https://github.com/sidristij/dotnetbook/issues/55)

从.NET Core 2.0和.NET Framework 4.5开始，我们可以使用新的数据类型： `Span` 和 `Memory`。要使用它们，您只需要安装 `System.Memory` nuget package:

  - `PM> Install-Package System.Memory`

这些数据类型值得注意，因为CLR团队通过将这些数据类型直接嵌入到核心中，在.NET Core 2.1+ JIT编译器的代码中实现了他们的特殊支持。这些数据类型是什么类型，为什么它们值得整整一章？

如果我们谈论使这些类型出现的问题，我应该说出其中的三个。第一个是非托管代码。

语言和平台都存在多年以及使用非托管代码的方法。那么，如果前者基本上存在多年，为什么要发布另一个API来处理非托管代码呢？要回答这个问题，我们应该了解我们以前缺乏的东西。

平台开发人员已经尝试为我们提供非托管资源的使用。他们为导入的方法实现了自动包装，并且在大多数情况下都自动运行。这里也属于`stackalloc`，在关于线程堆栈的章节中提到过。但是，正如我所看到的，第一批C＃开发人员来自C ++世界(我的情况)，但现在他们转向更高级的语言(我知道之前用JavaScript编写的开发人员)。这意味着人们对非托管代码和C / C +构造越来越怀疑，这对Assembler来说更是如此。

因此，项目包含越来越少的不安全代码，并且对平台API的信心越来越强。如果我们在公共存储库中搜索`stackalloc`用例，这很容易检查 - 它们很少。但是，让我们使用任何使用它的代码:

**Interop.ReadDir class**
[/src/mscorlib/shared/Interop/Unix/System.Native/Interop.ReadDir.cs](https://github.com/dotnet/coreclr/blob/b29f6328510207970763580d6f4db864e4b198af/src/mscorlib/shared/Interop/Unix/System.Native/Interop.ReadDir.cs#L71-L83)

```csharp
unsafe
{
    // s_readBufferSize is zero when the native implementation does not support reading into a buffer.
    byte* buffer = stackalloc byte[s_readBufferSize];
    InternalDirectoryEntry temp;
    int ret = ReadDirR(dir.DangerousGetHandle(), buffer, s_readBufferSize, out temp);
    // We copy data into DirectoryEntry to ensure there are no dangling references.
    outputEntry = ret == 0 ?
                new DirectoryEntry() { InodeName = GetDirectoryEntryName(temp), InodeType = temp.InodeType } :
                default(DirectoryEntry);

    return ret;
}
```

我们可以看到为什么它不受欢迎。只需浏览此代码并质疑自己是否信任它。我猜答案是'不'。那么，问问自己为什么。很明显：我们不仅看到了“Dangerous”这个词，这表明某些东西可能出错，但是有`unsafe`关键字和`byte* buffer = stackalloc byte[s_readBufferSize];`line(具体 - `byte *`)改变了我们的态度。这是你思考的一个触发因素：“没有其他方法可以做到吗？” 那么，让我们深入了解精神分析：你为什么这么想？一方面，我们使用语言结构，这里提供的语法远不是，例如，C++/CLI，它允许任何东西(甚至插入纯的汇编程序代码)。另一方面，这种语法看起来很不寻常。

开发人员隐含或明确地想到的第二个问题是string和char []类型的不兼容性。虽然逻辑上字符串是一个字符数组，但是您不能将字符串强制转换为char []：您只能创建一个新对象并将字符串的内容复制到数组中。引入这种不兼容性是为了在存储方面优化字符串(没有只读数组)。但是，当您开始处理文件时会出现问题。怎么看？作为字符串还是数组？如果选择数组，则无法使用某些设计用于处理字符串的方法。读字符串怎么样？可能太长了。如果您需要解析它，那么您应该为原始数据类型选择什么解析器：您并不总是想要手动解析它们(整数，浮点数，以不同格式给出)。我们有很多经过验证的算法可以更快，更有效地完成它，不是吗？但是，这样的算法通常使用只包含原始类型的字符串。所以，有一个两难的境地。

第三个问题是算法所需的数据很少在从某个源读取的数组的一部分内形成连续的固体数据切片。例如，在从套接字读取文件或数据的情况下，我们有一部分已经由算法处理的部分，后面是必须由我们的方法处理的一部分数据，然后是尚未处理的数据。理想情况下，我们的方法只需要设计此方法的数据。例如，解析整数的方法将不满意包含一些具有预期数字的字符串的字符串。这种方法想要一个数字，而不是别的。或者，如果我们传递整个数组，则需要指示例如数组开头的数字的偏移量

```csharp
int ParseInt(char[] input, int index)
{
    while(char.IsDigit(input[index]))
    {
        // ...
        index++;
    }
}
```

但是，这种方法很差，因为这种方法会得到不必要的数据。换句话说*该方法是为上下文调用的，它不是为*而设计的，必须解决一些外部任务。这是一个糟糕的设计。如何避免这些问题？作为一个选项，我们可以使用`ArraySegment <T>`类型，它可以访问数组的一部分：

```csharp
int ParseInt(IList<char>[] input)
{
    while(char.IsDigit(input.Array[index]))
    {
        // ...
        index++;
    }
}

var arraySegment = new ArraySegment(array, from, length);
var res = ParseInt((IList<char>)arraySegment);
```

但是，我认为这在逻辑和性能下降方面都太过分了。与使用数组进行的相同操作相比，`ArraySegment`的设计很差，并且对元素的访问速度减慢了7倍。

那么我们如何解决这些问题呢？我们如何让开发人员回到使用非托管代码并为他们提供统一且快速的工具来处理异构数据源：数组，字符串和非托管内存。有必要让他们有一种自信，他们不会在不知不觉中犯错误。有必要为它们提供一种不会在性能方面减少本机数据类型的工具，而是解决所列出的问题。`Span <T>`和`Memory <T>`类型就是这些乐器。

## Span\<T>, ReadOnlySpan\<T>

`Span`类型是一种处理数据数据部分内的数据或其值的子范围的工具。在数组的情况下，它允许读取和写入该子范围的元素，但有一个重要的约束：你得到或创建一个`Span <T>`仅用于*临时*与数组一起工作，只是为了调用一组方法。但是，为了获得一般性的理解，让我们比较“Span”设计的数据类型，并查看其可能的使用场景。

第一类数据是通常的数组。数组以下列方式使用`Span`：

```csharp
    var array = new [] {1,2,3,4,5,6};
    var span = new Span<int>(array, 1, 3);
    var position = span.BinarySearch(3);
    Console.WriteLine(span[position]);  // -> 3
```

首先，我们创建一个数据数组，如本例所示。接下来，我们创建引用数组的`Span`(或子集)，并使使用该数组的代码可以访问先前初始化的值范围。

在这里，我们看到此类数据的第一个特征，即创建特定上下文的能力。让我们扩展我们对上下文的看法：

```csharp
void Main()
{
    var array = new [] {'1','2','3','4','5','6'};
    var span = new Span<char>(array, 1, 3);
    if(TryParseInt32(span, out var res))
    {
        Console.WriteLine(res);
    }
    else
    {
        Console.WriteLine("Failed to parse");
    }
}

public bool TryParseInt32(Span<char> input, out int result)
{
    result = 0;
    for (int i = 0; i < input.Length; i++)
    {
        if(input[i] < '0' || input[i] > '9')
            return false;
    result = result * 10 + ((int)input[i] - '0');
    }
    return true;
}

-----
234
```

正如我们所看到的，`Span <T>`提供了对读取和写入的内存范围的抽象访问。它给了我们什么？如果我们还记得我们可以使用`Span`的其他内容，我们将考虑非托管资源和字符串：

```csharp
// Managed array
var array = new[] { '1', '2', '3', '4', '5', '6' };
var arrSpan = new Span<char>(array, 1, 3);
if (TryParseInt32(arrSpan, out var res1))
{
    Console.WriteLine(res1);
}

// String
var srcString = "123456";
var strSpan = srcString.AsSpan();
if (TryParseInt32(strSpan, out var res2))
{
    Console.WriteLine(res2);
}

// void *
Span<char> buf = stackalloc char[6];
buf[0] = '1'; buf[1] = '2'; buf[2] = '3';
buf[3] = '4'; buf[4] = '5'; buf[5] = '6';

if (TryParseInt32(buf, out var res3))
{
    Console.WriteLine(res3);
}

-----
234
234
234
```

这意味着`Span <T>`是一种统一处理内存管理方式的工具，包括托管和非托管内存。它可确保在垃圾收集期间处理此类数据时的安全性。也就是说，如果具有非托管资源的内存范围开始移动，那么它将是安全的。

但是，我们应该如此兴奋吗？我们能早点实现吗？例如，在托管数组的情况下毫无疑问：你只需要将一个数组包装在另一个类中(例如，长存在的[ArraySegment](https://referencesource.microsoft.com/#mscorlib/system/arraysegment.cs,31))因此提供了类似的接口，就是这样。此外，你可以用字符串做同样的事情 - 他们有必要的方法。同样，您只需要包含相同类型的字符串并提供使用它的方法。但是，要将字符串，缓冲区和数组存储在一种类型中，您将在单个实例中保留对每个可能变体的引用(显然只有一个活动变体)。

```csharp
public readonly ref struct OurSpan<T>
{
    private T[] _array;
    private string _str;
    private T * _buffer;

    // ...
}
```

或者，基于体系结构，您可以创建三种实现统一接口的类型。因此，不可能在这些数据类型之间创建一个与`Span <T>`不同的统一接口，并保持最大性能。

接下来，有一个关于`Span`的`ref struct`的问题？这些正是我们在求职面试中经常听到的那些“仅在堆叠上存在的结构”。这意味着此数据类型只能在堆栈上分配，不能转到堆。这就是为什么作为ref结构的“Span”是一种上下文数据类型，它支持方法的工作而不是内存中对象的工作。这是我们在尝试理解它时需要的基础。

现在我们可以定义`Span`类型和相关的`ReadOnlySpan`类型：

> Span是一种数据类型，它实现统一的接口以处理异构类型的数据数组，并允许将数组的子集传递给方法，这样无论深度如何，访问原始数组的速度都是恒定的和最高的。上下文。

的确，如果我们有像这样的代码

```csharp
public void Method1(Span<byte> buffer)
{
    buffer[0] = 0;
    Method2(buffer.Slice(1,2));
}
Method2(Span<byte> buffer)
{
    buffer[0] = 0;
    Method3(buffer.Slice(1,1));
}
Method3(Span<byte> buffer)
{
    buffer[0] = 0;
}
```

在使用托管指针而不是托管对象时，访问原始缓冲区的速度最高。这意味着您在托管包装器中使用不安全类型，但不使用.NET托管类型。

### Span\<T> 用法示例

在他或她获得一些经验之前，人类本质上无法完全理解某种工具的目的。那么，让我们转向一些例子。

#### ValueStringBuilder

关于算法最有趣的例子之一是`ValueStringBuilder`类型。但是，它深埋在mscorlib内部，并用`internal`修饰符标记为许多其他非常有趣的数据类型。这意味着如果我们没有研究mscorlib源代码，我们就不会找到这种非凡的优化工具。

`StringBuilder`系统类型的主要缺点是什么？它的主要缺点是类型及其基础 - 它是一个引用类型，基于`char []`，即一个字符数组。至少，这意味着两件事：无论如何我们使用堆(尽管不多)并增加错过CPU现金的机会。

我遇到的`StringBuilder`的另一个问题是构造小字符串，即结果字符串必须短，例如少于100个字符。简短格式化会引发性能问题。

```csharp
    $"{x} is in range [{min};{max}]"
```

这种变体在多大程度上比通过`StringBuilder`手动构建更糟糕？答案并不总是显而易见的。这取决于施工地点和调用此方法的频率。最初，`string.Format`为内部`StringBuilder`分配内存，它将创建一个字符数组(SourceString.Length + args.Length * 8)。如果在数组构造期间发现长度被错误地确定，则将创建另一个`StringBuilder`来构造其余部分。这将导致创建单个链表。因此，它必须返回构造的字符串，这意味着另一次复制。那是浪费。如果我们可以摆脱在堆上分配已形成字符串的数组，那将是很好的：这将解决我们的一个问题。

让我们从`mscorlib`的深度看这个类型：

**ValueStringBuilder class**
[/src/mscorlib/shared/System/Text/ValueStringBuilder](https://github.com/dotnet/coreclr/blob/efebb38f3c18425c57f94ff910a50e038d13c848/src/mscorlib/shared/System/Text/ValueStringBuilder.cs)

```csharp
    internal ref struct ValueStringBuilder
    {
        // this field will be active if we have too many characters
        private char[] _arrayToReturnToPool;
        // this field will be the main
        private Span<char> _chars;
        private int _pos;
        // the type accepts the buffer from the outside, delegating the choice of its size to a calling party
        public ValueStringBuilder(Span<char> initialBuffer)
        {
            _arrayToReturnToPool = null;
            _chars = initialBuffer;
            _pos = 0;
        }

        public int Length
        {
            get => _pos;
            set
            {
                int delta = value - _pos;
                if (delta > 0)
                {
                    Append('\0', delta);
                }
                else
                {
                    _pos = value;
                }
            }
        }

        // Here we get the string by copying characters from the array into another array
        public override string ToString()
        {
            var s = new string(_chars.Slice(0, _pos));
            Clear();
            return s;
        }

        // To insert a required character into the middle of the string
        //you should add space into the characters of that string and then copy that character
        public void Insert(int index, char value, int count)
        {
            if (_pos > _chars.Length - count)
            {
                Grow(count);
            }

            int remaining = _pos - index;
            _chars.Slice(index, remaining).CopyTo(_chars.Slice(index + count));
            _chars.Slice(index, count).Fill(value);
            _pos += count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(char c)
        {
            int pos = _pos;
            if (pos < _chars.Length)
            {
                _chars[pos] = c;
                _pos = pos + 1;
            }
            else
            {
                GrowAndAppend(c);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void GrowAndAppend(char c)
        {
            Grow(1);
            Append(c);
        }

        // If the original array passed by the constructor wasn’t enough
        // we allocate an array of a necessary size from the pool of free arrays
        // It would be ideal if the algorithm considered
        // discreteness of array size to avoid pool fragmentation. 
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Grow(int requiredAdditionalCapacity)
        {
            Debug.Assert(requiredAdditionalCapacity > _chars.Length - _pos);

            char[] poolArray = ArrayPool<char>.Shared.Rent(Math.Max(_pos + requiredAdditionalCapacity, _chars.Length * 2));

            _chars.CopyTo(poolArray);

            char[] toReturn = _arrayToReturnToPool;
            _chars = _arrayToReturnToPool = poolArray;
            if (toReturn != null)
            {
                ArrayPool<char>.Shared.Return(toReturn);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Clear()
        {
            char[] toReturn = _arrayToReturnToPool;
            this = default; // for safety, to avoid using pooled array if this instance is erroneously appended to again
            if (toReturn != null)
            {
                ArrayPool<char>.Shared.Return(toReturn);
            }
        }

        // Missing methods:  the situation is crystal clear
        private void AppendSlow(string s);
        public bool TryCopyTo(Span<char> destination, out int charsWritten);
        public void Append(string s);
        public void Append(char c, int count);
        public unsafe void Append(char* value, int length);
        public Span<char> AppendSpan(int length);
    }
```

这个类在功能上类似于它的高级同事`StringBuilder`，虽然它有一个有趣且非常重要的特性：它是一个值类型。这意味着它完全按值存储和传递。此外，新的`ref`类型修饰符(它是类型声明签名的一部分)表示此类型具有附加约束：它只能在堆栈上分配。我的意思是将其实例传递给类字段将产生错误。这些东西是什么？要回答这个问题，你只需要查看`StringBuilder`类，我们刚刚描述了它的本质：

**StringBuilder class** [/src/mscorlib/src/System/Text/StringBuilder.cs](https://github.com/dotnet/coreclr/blob/68f72dd2587c3365a9fe74d1991f93612c3bc62a/src/mscorlib/src/System/Text/StringBuilder.cs#L47-L62)

```csharp
public sealed class StringBuilder : ISerializable
{
    // A StringBuilder is internally represented as a linked list of blocks each of which holds
    // a chunk of the string.  It turns out string as a whole can also be represented as just a chunk,
    // so that is what we do.
    internal char[] m_ChunkChars;                // The characters in this block
    internal StringBuilder m_ChunkPrevious;      // Link to the block logically before this block
    internal int m_ChunkLength;                  // The index in m_ChunkChars that represent the end of the block
    internal int m_ChunkOffset;                  // The logical offset (sum of all characters in previous blocks)
    internal int m_MaxCapacity = 0;

    // ...

    internal const int DefaultCapacity = 16;
```

`StringBuilder`是一个包含对字符数组的引用的类。因此，在创建它时，实际上会出现两个对象：`StringBuilder`和一个字符数组，其大小至少为16个字符。这就是为什么必须设置字符串的预期长度：它将通过生成一个包含16个字符的数组的链接列表来构建。承认，这是一种浪费。就`ValueStringBuilder`类型而言，它意味着没有默认的`capacity`，因为它借用了外部存储器。此外，它是一种值类型，它使用户为堆栈上的字符分配缓冲区。因此，类型的整个实例与其内容一起被放在堆栈上并且解决了优化问题。由于不需要在堆上分配内存，因此在处理堆时性能降低没有问题。所以，你可能有一个问题：为什么我们不总是使用`ValueStringBuilder`(或它的自定义模拟，因为我们不能使用原文，因为它是内部的)？答案是：它取决于任务。结果字符串是否具有确定的大小？它是否具有已知的最大长度？如果回答“是”并且字符串不超过合理的边界，则可以使用`StringBuilder`的值版本。但是，如果您期望冗长的字符串，请使用通常的版本。

#### ValueListBuilder

```csharp
internal ref partial struct ValueListBuilder<T>
{
    private Span<T> _span;
    private T[] _arrayFromPool;
    private int _pos;

    public ValueListBuilder(Span<T> initialSpan)
    {
        _span = initialSpan;
        _arrayFromPool = null;
        _pos = 0;
    }

    public int Length { get; set; }

    public ref T this[int index] { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(T item);

    public ReadOnlySpan<T> AsSpan();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose();

    private void Grow();
}
```

我特别想要注意的第二类数据是`ValueListBuilder`类型。当您需要在短时间内创建元素集合并将其传递给算法进行处理时，可以使用它。

承认，这个任务看起来非常类似于`ValueStringBuilder`任务。它以类似的方式解决：

** File [ValueListBuilder.cs (https://github.com/dotnet/coreclr/blob/dbaf2957387c5290a680c8918779683194137b1d/src/System.Private.CoreLib/shared/System/Collections/Generic/ValueListBuilder.cs)**

说清楚，这些情况经常发生。但是，之前我们以另一种方式解决了这个问题 我们曾经创建一个`List`，用数据填充它并丢失对它的引用。如果经常调用该方法，这将导致一种悲惨的情况：许多`List`实例(和相关的数组)在堆上被挂起。现在解决了这个问题：不会创建其他对象。但是，就像`ValueStringBuilder`一样，它仅针对Microsoft程序员解决：此类具有`internal`修饰符。

### 规则和使用实践

要通过编写使用它的两个或三个或更多方法来完全理解您需要使用的新数据类型。但是，现在可以学习主要规则：

  - 如果您的方法处理某些输入数据集而不更改其大小，您可以尝试坚持“Span”类型。如果您不打算修改缓冲区，请选择`ReadOnlySpan`类型;
  - 如果你的方法处理字符串计算一些统计信息或解析这些字符串，它_must_接受`ReadOnlySpan <char>`。必须是一个新规则。因为当你接受一个字符串时，你会让某人为你创建一个子字符串;
  - 如果需要为方法创建一个短数据数组(不超过10 kB)，可以使用`Span <TType> buf = stackalloc TType [size]`轻松排列。请注意，TType应该是值类型，因为`stackalloc`仅适用于值类型。

在其他情况下，您最好仔细查看“Memory”或使用经典数据类型。

### 如何工作？

我想谈谈“跨度”功能如何以及为什么值得注意。还有一些事情要谈。这种类型的数据有两个版本：一个用于.NET Core 2.0+，另一个用于其余版本。

**File [Span.Fast.cs, .NET Core 2.0](https://github.com/dotnet/coreclr/blob/38403e661a4202ca4c8a72e4bbd9a263bddeb891/src/System.Private.CoreLib/shared/System/Span.Fast.cs)**

```csharp
public readonly ref partial struct Span<T>
{
    /// A reference to a .NET object or a pure pointer
    internal readonly ByReference<T> _pointer;
    /// The length of the buffer based on the pointer
    private readonly int _length;
    // ...
}
```

**File ??? [decompiled]**

```csharp
public ref readonly struct Span<T>
{
    private readonly System.Pinnable<T> _pinnable;
    private readonly IntPtr _byteOffset;
    private readonly int _length;
    // ...
}
```

问题是_huge_ .NET Framework和.NET Core 1. *没有以特殊方式更新垃圾收集器(与.NET Core 2.0+不同)，并且他们必须使用指向缓冲区开头的附加指针使用。这意味着，内部的“Span”处理托管的.NET对象，就好像它们是不受管理的一样。只需看看结构的第二个变体：它有三个字段。第一个是对manged对象的引用。第二个是从该对象的开头以字节为单位的偏移量，用于定义数据缓冲区的开头(在字符串中，此缓冲区包含`char`字符，而在数组中，它包含数组的数据)。最后，第三个字段包含连续放置的缓冲区中的元素数量。

让我们看看`Span`如何处理字符串，例如：

**File [coreclr::src/System.Private.CoreLib/shared/System/MemoryExtensions.Fast.cs](https://github.com/dotnet/coreclr/blob/2b50bba8131acca2ab535e144796941ad93487b7/src/System.Private.CoreLib/shared/System/MemoryExtensions.Fast.cs#L409-L416)**

```csharp
public static ReadOnlySpan<char> AsSpan(this string text)
{
    if (text == null)
        return default;

    return new ReadOnlySpan<char>(ref text.GetRawStringData(), text.Length);
}
```

`string.GetRawStringData()` 的方式如下：

**A file with the definition of fields [coreclr::src/System.Private.CoreLib/src/System/String.CoreCLR.cs](https://github.com/dotnet/coreclr/blob/2b50bba8131acca2ab535e144796941ad93487b7/src/System.Private.CoreLib/src/System/String.CoreCLR.cs#L16-L23)**

**A file with the definition of GetRawStringData [coreclr::src/System.Private.CoreLib/shared/System/String.cs](https://github.com/dotnet/coreclr/blob/2b50bba8131acca2ab535e144796941ad93487b7/src/System.Private.CoreLib/shared/System/String.cs#L462)**

```csharp
public sealed partial class String :
    IComparable, IEnumerable, IConvertible, IEnumerable<char>,
    IComparable<string>, IEquatable<string>, ICloneable
{

    //
    // These fields map directly onto the fields in an EE StringObject.  See object.h for the layout.
    //
    [NonSerialized] private int _stringLength;

    // For empty strings, this will be '\0' since
    // strings are both null-terminated and length prefixed
    [NonSerialized] private char _firstChar;


    internal ref char GetRawStringData() => ref _firstChar;
}
```

事实证明，该方法直接访问字符串的内部，而`ref char`规范允许GC通过在GC处于活动状态时将字符串与字符串一起移动来跟踪字符串内部的非托管引用。

数组也是如此：当创建`Span`时，一些内部JIT代码计算数据数组开头的偏移量，并用此偏移量初始化`Span`。关于字符串和数组的偏移量的计算方法在关于内存中对象结构的章节中讨论过(.\ObjectsStructure.md)。

### Span\<T> 作为返回值

尽管存在所有的和谐，但是“Span”在从方法返回时有一些逻辑但意外的约束。如果我们查看以下代码：

```csharp
unsafe void Main()
{
    var x = GetSpan();
}

public Span<byte> GetSpan()
{
    Span<byte> reff = new byte[100];
    return reff;
}
```

我们可以看到它是合乎逻辑的和良好的。但是，如果我们用另一个指令替换一个指令：

```csharp
unsafe void Main()
{
    var x = GetSpan();
}

public Span<byte> GetSpan()
{
    Span<byte> reff = stackalloc byte[100];
    return reff;
}
```
编译器会禁止它。在我说出原因之前，我想让你猜出这个结构带来了哪些问题。

好吧，我希望你能想到，猜到甚至理解原因。如果是的话，我写一篇关于 [线程堆栈](./ThreadStack.md) 的详细章节的努力得到了回报。因为当您从完成其工作的方法返回对局部变量的引用时，您可以调用另一个方法，等待它完成其工作，然后使用 x[0.99] 读取这些局部变量的值。

幸运的是，当我们尝试编写这样的代码时，编译器会在我们的手腕上发出警告：`CS8352 Cannot use local 'reff' in this context because it may expose referenced variables outside of their declaration scope`，因为它可能会在其声明范围之外公开引用的变量。编译器是正确的，因为如果你绕过这个错误，在插件中，有可能窃取他人的密码或提升运行我们的插件的权限。


## Memory\<T> 和 ReadOnlyMemory\<T>

`Memory <T>`和`Span <T>`之间存在两种视觉差异。第一个是`Memory <T>`类型在类型的标题中不包含`ref`修饰符。换句话说，`Memory <T>`类型既可以在堆栈上分配，也可以是局部变量，方法参数，或者返回值，在堆上，从那里引用内存中的一些数据。然而，与`Span <T>`相比，这种小差异在`Memory<T>`的行为和能力方面产生了巨大的差异。与`Span <T>`不同，某些方法使用某些数据缓冲区是，`Memory <T>`类型用于存储有关缓冲区的信息，但不处理它。因此，API存在差异。

  - `Memory <T>`没有方法来访问它负责的数据。相反，它具有`Span`属性和`Slice`方法，它返回`Span`类型的实例。
  - 此外，`Memory <T>`包含`Pin()`方法，用于存储缓冲区数据应该传递给`unsafe`代码的场景。如果在.NET中分配内存时调用此方法，则缓冲区将被固定，并且在GC处于活动状态时不会移动。这个方法将返回一个`MemoryHandle`结构的实例，它封装了'GCHandle`来表示生命周期的一段，并在内存中固定数组缓冲区。

但是，我建议我们熟悉整套课程。首先，让我们看一下`Memory <T>`结构本身(这里我只展示那些我发现最重要的类型成员)：

```csharp
    public readonly struct Memory<T>
    {
        private readonly object _object;
        private readonly int _index, _length;

        public Memory(T[] array) { ... }
        public Memory(T[] array, int start, int length) { ... }
        internal Memory(MemoryManager<T> manager, int length) { ... }
        internal Memory(MemoryManager<T> manager, int start, int length) { ... }

        public int Length => _length & RemoveFlagsBitMask;
        public bool IsEmpty => (_length & RemoveFlagsBitMask) == 0;

        public Memory<T> Slice(int start, int length);
        public void CopyTo(Memory<T> destination) => Span.CopyTo(destination.Span);
        public bool TryCopyTo(Memory<T> destination) => Span.TryCopyTo(destination.Span);

        public Span<T> Span { get; }
        public unsafe MemoryHandle Pin();
    }
```

我们看到结构包含基于数组的构造函数，但是将数据存储在对象中。这是另外引用没有为它们设计的构造函数的字符串，但可以与`AsMemory()``string`方法一起使用，它返回`ReadOnlyMemory`。但是，由于两种类型都应该是二进制相似的，因此`Object`是`_object`字段的类型。

接下来，我们看到两个基于`MemoryManager`的构造函数。我们稍后会讨论它们。获取“长度”(大小)和`IsEmpty`的属性检查空集。此外，还有用于获取子集的`Slice`方法以及用于复制的`CopyTo`和`TryCopyTo`方法。

谈论“内存”我想详细描述这种类型的两种方法：`Span`属性和`Pin`方法。

### Memory\<T>.Span

```csharp
public Span<T> Span
{
    get
    {
        if (_index < 0)
        {
            return ((MemoryManager<T>)_object).GetSpan().Slice(_index & RemoveFlagsBitMask, _length);
        }
        else if (typeof(T) == typeof(char) && _object is string s)
        {
            // This is dangerous, returning a writable span for a string that should be immutable.
            // However, we need to handle the case where a ReadOnlyMemory<char> was created from a string
            // and then cast to a Memory<T>. Such a cast can only be done with unsafe or marshaling code,
            // in which case that's the dangerous operation performed by the dev, and we're just following
            // suit here to make it work as best as possible.
            return new Span<T>(ref Unsafe.As<char, T>(ref s.GetRawStringData()), s.Length).Slice(_index, _length);
        }
        else if (_object != null)
        {
            return new Span<T>((T[])_object, _index, _length & RemoveFlagsBitMask);
        }
        else
        {
            return default;
        }
    }
}
```

即，处理字符串管理的行。他们说如果我们将`ReadOnlyMemory <T>`转换为`Memory <T>`(这些东西在二进制表示中是相同的，甚至有一个注释，这些类型必须以二进制方式重合，因为一个是通过调用从另一个产生的`Unsafe.As`)我们将获得一个秘密室有机会改变字符串。这是一个非常危险的机制：

```csharp
unsafe void Main()
{
    var str = "Hello!";
    ReadOnlyMemory<char> ronly = str.AsMemory();
    Memory<char> mem = (Memory<char>)Unsafe.As<ReadOnlyMemory<char>, Memory<char>>(ref ronly);
    mem.Span[5] = '?';

    Console.WriteLine(str);
}
---
Hello?
```

这种机制与字符串实习相结合会产生可怕的后果。

### Memory\<T>.Pin

引起强烈关注的第二种方法是`Pin`：

```csharp
public unsafe MemoryHandle Pin()
{
    if (_index < 0)
    {
        return ((MemoryManager<T>)_object).Pin((_index & RemoveFlagsBitMask));
    }
    else if (typeof(T) == typeof(char) && _object is string s)
    {
        // This case can only happen if a ReadOnlyMemory<char> was created around a string
        // and then that was cast to a Memory<char> using unsafe / marshaling code.  This needs
        // to work, however, so that code that uses a single Memory<char> field to store either
        // a readable ReadOnlyMemory<char> or a writable Memory<char> can still be pinned and
        // used for interop purposes.
        GCHandle handle = GCHandle.Alloc(s, GCHandleType.Pinned);
        void* pointer = Unsafe.Add<T>(Unsafe.AsPointer(ref s.GetRawStringData()), _index);
        return new MemoryHandle(pointer, handle);
    }
    else if (_object is T[] array)
    {
        // Array is already pre-pinned
        if (_length < 0)
        {
            void* pointer = Unsafe.Add<T>(Unsafe.AsPointer(ref array.GetRawSzArrayData()), _index);
            return new MemoryHandle(pointer);
        }
        else
        {
            GCHandle handle = GCHandle.Alloc(array, GCHandleType.Pinned);
            void* pointer = Unsafe.Add<T>(Unsafe.AsPointer(ref array.GetRawSzArrayData()), _index);
            return new MemoryHandle(pointer, handle);
        }
    }
    return default;
}
```

它也是统一的重要工具，因为如果我们想要将缓冲区传递给非托管代码，我们只需调用`Pin()`方法并将指针传递给此代码，无论数据类型是什么`Memory<T>`指的是。该指针将存储在结果结构的属性中。

```csharp
void PinSample(Memory<byte> memory)
{
    using(var handle = memory.Pin())
    {
        WinApi.SomeApiMethod(handle.Pointer);
    }
}
```

在这段代码中调用`Pin()`并不重要：它可以是`Memory`，它代表`T []`，或者`string`或非托管内存的缓冲区。仅仅数组和字符串将获得一个真正的`GCHandle.Alloc(array，GCHandleType.Pinned)`并且在非托管内存的情况下什么都不会发生。

## MemoryManager, IMemoryOwner, MemoryPool

除了指示结构字段之外，我还要注意，还有另外两个基于另一个实体的`internal`类型构造函数 - “MemoryManager”。这不是您可能想到的经典内存管理器，我们稍后会讨论它。您可能已经想过的经典内存管理器，我们稍后会讨论它。与`Span`类似，`Memory`引用了一个导航对象，一个偏移量和一个内部缓冲区的大小。请注意，您可以使用`new`运算符仅从数组创建`Memory`。或者，您可以使用扩展方法从字符串，数组或`ArraySegment`创建`Memory`。我的意思是它不是设计为手动从非托管内存创建的。但是，我们可以看到有一个内部方法使用`MemoryManager`创建这个结构。

**File [MemoryManager.cs](https://github.com/dotnet/corefx/blob/master/src/Common/src/CoreLib/System/Buffers/MemoryManager.cs)**

```csharp
public abstract class MemoryManager<T> : IMemoryOwner<T>, IPinnable
{
    public abstract MemoryHandle Pin(int elementIndex = 0);
    public abstract void Unpin();

    public virtual Memory<T> Memory => new Memory<T>(this, GetSpan().Length);
    public abstract Span<T> GetSpan();
    protected Memory<T> CreateMemory(int length) => new Memory<T>(this, length);
    protected Memory<T> CreateMemory(int start, int length) => new Memory<T>(this, start, length);

    void IDisposable.Dispose()
    protected abstract void Dispose(bool disposing);
}
```

此结构表示内存范围的所有者。换句话说，`Span`是一种使用内存的工具，`Memory`是一种存储特定内存范围信息的工具，而`MemoryManager`是一种控制该范围生命周期的工具，即它的所有者。例如，我们可以查看`NativeMemoryManager<T>`类型。虽然它用于测试，但这种类型清楚地代表了“所有权”的概念。

**File [NativeMemoryManager.cs](https://github.com/dotnet/corefx/blob/888088448ac5dd1053d88434dfd819dcbc0fd9a1/src/Common/tests/System/Buffers/NativeMemoryManager.cs)**

```csharp
internal sealed class NativeMemoryManager : MemoryManager<byte>
{
    private readonly int _length;
    private IntPtr _ptr;
    private int _retainedCount;
    private bool _disposed;

    public NativeMemoryManager(int length)
    {
        _length = length;
        _ptr = Marshal.AllocHGlobal(length);
    }

    public override void Pin() { ... }

    public override void Unpin()
    {
        lock (this)
        {
            if (_retainedCount > 0)
            {
                _retainedCount--;
                if (_retainedCount== 0)
                {
                    if (_disposed)
                    {
                        Marshal.FreeHGlobal(_ptr);
                        _ptr = IntPtr.Zero;
                    }
                }
            }
        }
    }

    // Other methods
}
```

这意味着该类允许嵌套调用`Pin()`方法，从而计算来自`unsafe`世界的生成引用。

与`Memory`紧密相关的另一个实体是`MemoryPool`，它汇集了`MemoryManager`实例 (实际上是`IMemoryOwner`):

**File [MemoryPool.cs](https://github.com/dotnet/corefx/blob/f592e887e2349ed52af6a83070c42adb9d26408c/src/System.Memory/src/System/Buffers/MemoryPool.cs)**

```csharp
public abstract class MemoryPool<T> : IDisposable
{
    public static MemoryPool<T> Shared => s_shared;

    public abstract IMemoryOwner<T> Rent(int minBufferSize = -1);

    public void Dispose() { ... }
}
```

它用于租用必要大小的缓冲区以供临时使用。实现了`IMemoryOwner<T>`接口的租用实例使用`Dispose()`方法将租用的数组返回给数组池。默认情况下，您可以使用在`ArrayMemoryPool`上构建的可共享缓冲池：

**File [ArrayMemoryPool.cs](https://github.com/dotnet/corefx/blob/56dfb8834fa50f3bc61ea9b4bfdc9dcc759b6ec9/src/System.Memory/src/System/Buffers/ArrayMemoryPool.cs)**

```csharp
internal sealed partial class ArrayMemoryPool<T> : MemoryPool<T>
{
    private const int MaximumBufferSize = int.MaxValue;
    public sealed override int MaxBufferSize => MaximumBufferSize;
    public sealed override IMemoryOwner<T> Rent(int minimumBufferSize = -1)
    {
        if (minimumBufferSize == -1)
            minimumBufferSize = 1 + (4095 / Unsafe.SizeOf<T>());
        else if (((uint)minimumBufferSize) > MaximumBufferSize)
            ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.minimumBufferSize);

        return new ArrayMemoryPoolBuffer(minimumBufferSize);
    }
    protected sealed override void Dispose(bool disposing) { }
}
```

基于这种架构，我们有以下图片：

  - 如果要读取数据(`ReadOnlySpan`)或读取和写入数据(`Span`)，则应使用`Span`数据类型作为方法参数。但是，它不应该存储在类的字段中以供将来使用。
  - 如果需要将类的字段中的引用存储到数据缓冲区，则需要根据目标使用`Memory<T>`或`ReadOnlyMemory <T>`。
  - `MemoryManager <T>`是数据缓冲区的所有者(可选)。例如，如果你需要计算`Pin()`调用，可能是必要的。或者，如果您需要知道如何释放内存。
  - 如果`Memory`是围绕非托管内存范围构建的，`Pin()`什么也不做。但是，这种制服使用不同类型的缓冲区：对于托管代码和非托管代码，交互界面将是相同的。  
  - 每种类型都有公共构造函数。这意味着你可以直接使用`Span`或从`Memory`获取它的实例。对于`Memory`这样，你可以单独创建它，或者你可以创建一个由`IMemoryOwner`拥有并由`Memory`引用的内存范围。任何基于`MemoryManger`的类型都可以被视为一个特定的案例，它拥有一些本地存储器范围(例如，伴随着来自`unsafe`世界的引用计数)。另外，如果你需要集中这些缓冲区(预计会有几乎相同大小的缓冲区的频繁流量)，你可以使用`MemoryPool`类型。
  - 如果你打算通过在那里传递一个数据缓冲区来处理`unsafe`代码，你应该使用`Memory`类型，它具有`Pin()`方法，如果在那里创建了一个缓冲区，它会在.NET堆上自动引脚。
  - 如果你有一些缓冲区流量(例如你解析程序或DSL的文本)，最好使用`MemoryPool`类型。您可以正确地实现它以从池中输出必要大小的缓冲区(例如，如果没有合适的缓冲区，则使用稍大的缓冲区，但使用`originalMemory.Slice(requiredSize)`以避免池碎片)。

## 性能

为了衡量新数据类型的性能，我决定使用已经成为标准的库 [BenchmarkDotNet](https://github.com/dotnet/BenchmarkDotNet):

```csharp
[Config(typeof(MultipleRuntimesConfig))]
public class SpanIndexer
{
    private const int Count = 100;
    private char[] arrayField;
    private ArraySegment<char> segment;
    private string str;

    [GlobalSetup]
    public void Setup()
    {
        str = new string(Enumerable.Repeat('a', Count).ToArray());
        arrayField = str.ToArray();
        segment = new ArraySegment<char>(arrayField);
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = Count)]
    public int ArrayIndexer_Get()
    {
        var tmp = 0;
        for (int index = 0, len = arrayField.Length; index < len; index++)
        {
            tmp = arrayField[index];
        }
        return tmp;
    }

    [Benchmark(OperationsPerInvoke = Count)]
    public void ArrayIndexer_Set()
    {
        for (int index = 0, len = arrayField.Length; index < len; index++)
        {
            arrayField[index] = '0';
        }
    }

    [Benchmark(OperationsPerInvoke = Count)]
    public int ArraySegmentIndexer_Get()
    {
        var tmp = 0;
        var accessor = (IList<char>)segment;
        for (int index = 0, len = accessor.Count; index < len; index++)
        {
            tmp = accessor[index];
        }
        return tmp;
    }

    [Benchmark(OperationsPerInvoke = Count)]
    public void ArraySegmentIndexer_Set()
    {
        var accessor = (IList<char>)segment;
        for (int index = 0, len = accessor.Count; index < len; index++)
        {
            accessor[index] = '0';
        }
    }

    [Benchmark(OperationsPerInvoke = Count)]
    public int StringIndexer_Get()
    {
        var tmp = 0;
        for (int index = 0, len = str.Length; index < len; index++)
        {
            tmp = str[index];
        }

        return tmp;
    }

    [Benchmark(OperationsPerInvoke = Count)]
    public int SpanArrayIndexer_Get()
    {
        var span = arrayField.AsSpan();
        var tmp = 0;
        for (int index = 0, len = span.Length; index < len; index++)
        {
            tmp = span[index];
        }
        return tmp;
    }

    [Benchmark(OperationsPerInvoke = Count)]
    public int SpanArraySegmentIndexer_Get()
    {
        var span = segment.AsSpan();
        var tmp = 0;
        for (int index = 0, len = span.Length; index < len; index++)
        {
            tmp = span[index];
        }
        return tmp;
    }

    [Benchmark(OperationsPerInvoke = Count)]
    public int SpanStringIndexer_Get()
    {
        var span = str.AsSpan();
        var tmp = 0;
        for (int index = 0, len = span.Length; index < len; index++)
        {
            tmp = span[index];
        }
        return tmp;
    }

    [Benchmark(OperationsPerInvoke = Count)]
    public void SpanArrayIndexer_Set()
    {
        var span = arrayField.AsSpan();
        for (int index = 0, len = span.Length; index < len; index++)
        {
            span[index] = '0';
        }
    }

    [Benchmark(OperationsPerInvoke = Count)]
    public void SpanArraySegmentIndexer_Set()
    {
        var span = segment.AsSpan();
        for (int index = 0, len = span.Length; index < len; index++)
        {
            span[index] = '0';
        }
    }
}

public class MultipleRuntimesConfig : ManualConfig
{
    public MultipleRuntimesConfig()
    {
        Add(Job.Default
            .With(CsProjClassicNetToolchain.Net471) // Span not supported by CLR
            .WithId(".NET 4.7.1"));

        Add(Job.Default
            .With(CsProjCoreToolchain.NetCoreApp20) // Span supported by CLR
            .WithId(".NET Core 2.0"));

        Add(Job.Default
            .With(CsProjCoreToolchain.NetCoreApp21) // Span supported by CLR
            .WithId(".NET Core 2.1"));

        Add(Job.Default
            .With(CsProjCoreToolchain.NetCoreApp22) // Span supported by CLR
            .WithId(".NET Core 2.2"));
    }
}
```

现在，让我们看看结果。

![Performance chart](../imgs/Span/Performance.png)

看着它们，我们可以得到以下信息:

  - `ArraySegment`很糟糕。但如果你把它包裹在`Span`中，你可以减少它的可怕性。在这种情况下，性能将提高7倍。
  - 如果我们考虑使用.NET Framework 4.7.1（同样适用于4.5），使用`Span`会在使用数据缓冲区时显着降低性能。它将减少30-35％。
  - 但是，如果我们查看.NET Core 2.1+，性能仍然相似甚至增加，因为`Span`可以使用数据缓冲区的一部分，创建上下文。可以在`ArraySegment`中找到相同的功能，但它的工作速度非常慢。

因此，我们可以得出关于这些数据类型使用的简单结论：

  - 对于`.NET Framework 4.5 +``.NET Core`，它们具有唯一的优势：在处理原始数组的子集时，它们比`ArraySegment`更快;
  - 在`.NET Core 2.1 +`中，它们的使用为'ArraySegment`和任何'Slice`的手动实现提供了无可否认的优势;
  - 所有这三种方式都尽可能高效，并且使用任何统一数组的工具都无法实现。
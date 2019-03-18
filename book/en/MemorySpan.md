# Memory\<T> and Span\<T>

> [A link to the discussion](https://github.com/sidristij/dotnetbook/issues/55)

Starting from .NET Core 2.0 and .NET Framework 4.5 we can use new data types:  `Span` and `Memory`. To use them, you just need to install the `System.Memory` nuget package:

  - `PM> Install-Package System.Memory`

These data types are notable because the CLR team has done a great job to implement their special support inside the code of .NET Core 2.1+ JIT compiler by embedding these data types right into the core. What kind of data types are these and why are they worth a whole chapter?

If we talk about problems that made these types appear, I should name three of them. The first one is unmanaged code.

Both the language and the platform have existed for many years along with means to work with unmanaged code. So, why release another API to work with unmanaged code if the former basically existed for many years? To answer this question, we should understand what we lacked before.

The platform developers already tried to facilitate the use of unmanaged resources for us. They implemented auto wrappers for imported methods and marshaling that works automatically in most cases. Here also belongs `stackalloc`, mentioned in the chapter about a thread stack. However, as I see it, the first C# developers came from C++ world (my case), but now they shift from more high-level languages (I know a developer who wrote in JavaScript before). This means people are getting more suspicious to unmanaged code and C/C+ constructs, so much the more to Assembler.

As a result, projects contain less and less unsafe code and the confidence in the platform API grows more and more. This is easy to check if we search for `stackalloc` use cases in public repositories  — they are scarce. However, let’s take any code that uses it:

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

We can see why it is not popular. Just skim this code and question yourself  whether you trust it. I guess the answer is ‘No’. Then, ask yourself why. It is obvious: not only do we see the word `Dangerous`, which kind of suggests that something may go wrong, but there is the `unsafe` keyword and `byte* buffer = stackalloc byte[s_readBufferSize];` line (specifically — `byte*`) that change our attitude. This is a trigger for you to think: “Wasn’t there another way to do it”? So, let’s get deeper into psychoanalysis: why might you think that way? On the one hand, we use language constructs and the syntax offered here is far from, for example, C++/CLI, which allows anything (even inserting pure Assembler code). On the other hand, this syntax looks unusual.

The second issue developers thought of implicitly or explicitly is incompatibility of  string and char[] types. Although, logically a string is an array of characters, but you can’t cast a string to char[]: you can only create a new object and copy the content of a string to an array. This incompatibility is introduced to optimize strings in terms of storage (there are no readonly arrays). However, problems appear when you start working with files. How to read them? As a string or as an array? If you choose an array you cannot use some methods designed to work with strings. What about reading as a string? It may be too long. If you need to parse it then, what parser should you choose for primitive data types: you don’t always want to parse them manually (integers, floats, given in different formats). We have a lot of proven algorithms that do it quicker and more efficiently, don’t we? However, such algorithms often work with strings that contain nothing but a primitive type itself. So, there is a dilemma.

The third problem is that the data required by an algorithm rarely make a continuous, solid data slice within a section of an array read from some source.  For example, in case of files or data read from a socket, we have some part of  those already processed by an algorithm, followed by a part of data that must be processed by our method, and then by not yet processed data.  Ideally, our method wants only the data for which this method was designed. For example, a method that parses integers won’t be happy with a string that contains some words with an expected number somewhere among them. This method wants a number and nothing else. Or, if we pass an entire array, there is a requirement to indicate, for example, the offset for a number from the beginning of the array.

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

However, this approach is poor, as this method gets unnecessary data. In other words *the method is called for contexts it was not designed for*, and has to solve some external tasks. This is a bad design. How to avoid these problems? As an option we can use the `ArraySegment<T>` type that can give access to a section of an array:

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

However, I think this is too much both in terms of logic and a decrease in performance. `ArraySegment` is poorly designed and slows the access to elements 7 times more in comparison with the same operations done with an array.

So how do we solve these problems? How do we get developers back to using unmanaged code and give them a unified and quick tool to work with heterogeneous data sources:  arrays, strings and unmanaged memory. It was necessary to give them a sense of confidence that they can’t do a mistake unknowingly. It was necessary to give them an instrument that doesn’t diminish native data types in terms of performance but solves the listed problems. `Span<T>` and `Memory<T>` types are exactly these instruments.

## Span\<T>, ReadOnlySpan\<T>

`Span` type is an instrument to work with data within a section of a data array or with a subrange of its values. As in case of an array it allows both reading and writing to the elements of this subrange, but with one important constraint: you get or create a `Span<T>` only for a *temporary* work with an array, Just to  call a group of methods. However, to get a general understanding let’s compare the types of data which `Span` is designed for and look at its possible use scenarios.

The first type of data is a usual array. Arrays work with `Span` in the following way:

```csharp
    var array = new [] {1,2,3,4,5,6};
    var span = new Span<int>(array, 1, 3);
    var position = span.BinarySearch(3);
    Console.WriteLine(span[position]);  // -> 3
```

At first, we create an array of data, as shown by this example. Next, we create `Span` (or a subset) which references to the array, and makes a previously initialized value range accessible to code that uses the array. 

Here we see the first feature of this type of data  i.e. the ability to create a certain context. Let’s expand our idea of contexts:

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

As we see `Span<T>` provides abstract access to a memory range both for reading and writing. What does it give us? If we remember what else we can use `Span` for, we will think about unmanaged resources and strings:

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

That means `Span<T>` is a tool to unify ways of working with memory,  both managed and unmanaged. It ensures safety while working with such data during Garbage Collection.  That is if memory ranges with unmanaged resources start to move, it will be safe.

However, should we be so excited? Could we achieve this earlier? For example, in case of managed arrays there is no doubt about it:  you just need to wrap an array in one more class (e.g. long-existing [ArraySegment] (https://referencesource.microsoft.com/#mscorlib/system/arraysegment.cs,31)) thus giving a similar interface and that is it. Moreover, you can do the same with strings  — they have necessary methods.  Again, you just need to wrap a string in the same type and provide methods to work with it. However, to store a string, a buffer and an array in one type you will have much to do with keeping references to each possible variant in a single instance (with only one active variant, obviously).

```csharp
public readonly ref struct OurSpan<T>
{
    private T[] _array;
    private string _str;
    private T * _buffer;

    // ...
}
```

Or, based on architecture you can create three types that implement a uniform interface. Thus, it is not possible to create a uniform interface between these data types that is different from `Span<T>` and keep the maximum performance.

Next, there is a question of what is `ref struct` in respect to `Span`? These are exactly those “structures existing only on stack” that we hear about during job interviews so often. It means this data type can be allocated on the stack only and cannot go to the heap. This is why `Span`, which is a ref structure, is a context data type that enables work of methods but not that of objects in memory. That is what we need to base on when trying to understand it.

Now we can define the `Span` type and the related `ReadOnlySpan` type:

> Span is a data type that implements a uniform interface to work with heterogeneous types of data arrays and enables passing a subset of an array to a method so that the speed of access to the original array would be constant and highest regardless of the depth of the context.

Indeed,  if we have a code like

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

the speed of access to the original buffer will be the highest as you work with a managed pointer and not a managed object. That means you work with an unsafe type in a managed wrapper, but not with a .NET managed type.

### Span\<T> usage examples

A human by nature cannot fully understand the purpose of a certain instrument until he or she gets some experience. So, let’s turn to some examples.

#### ValueStringBuilder

One of the most interesting examples in respect to algorithms is the `ValueStringBuilder` type. However, it is buried deep inside mscorlib and marked with the `internal` modifier as many other very interesting data types. This means we would not find this remarkable instrument for optimization if we haven’t researched the mscorlib source code.

What is the main disadvantage of the `StringBuilder` system type? Its main drawback is  the type and its basis — it is a reference type and is based on `char[]`, i.e. a character array. At least, this means two things: we use the heap (though not much) anyway and increase the chances to miss the CPU cash.

Another issue with `StringBuilder` that I faced is the construction of small strings,  that is when the resulting string must be short  e.g. less than 100 characters. Short formatting raises issues on performance.

```csharp
    $"{x} is in range [{min};{max}]"
```

To what extent is this variant worse than manual construction through `StringBuilder`? The answer is not always obvious.  It depends on the place of construction  and the frequency of calling this method. Initially, `string.Format` allocates memory for internal `StringBuilder` that will create an array of characters (SourceString.Length + args.Length * 8). If during array construction it turns out that the length was incorrectly determined, another `StringBuilder` will be created to construct the rest. This will lead to the creation of a single linked list. As a result, it must return the constructed string  which means another copying. That is a waste. It would be great if we could get rid of allocating the array of a formed string on the heap: this would solve one of our problems.

Let’s look at this type from the depth of `mscorlib`:

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

This class is functionally similar to its senior fellow `StringBuilder`, although having one interesting and very important feature:  it is a value type. That means it is stored and passed entirely by value. Also, a new `ref` type modifier, which is a part of a type declaration signature, indicates that this type has an additional constraint: it can be allocated only on the stack. I mean passing its instances to class fields will produce an error. What is all this stuff for? To answer this question, you just need to look at the `StringBuilder` class, the essence of which we have just described:

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

`StringBuilder` is a class that contains a reference to an array of characters. Thus, when you create it, there appear two objects in fact:  `StringBuilder` and an array of characters which is at least 16 characters in size. This is why it is essential to set the expected length of a string: it will be built by generating a single linked list of arrays with 16 characters each. Admit, that is a waste. In terms of `ValueStringBuilder` type, it means no default `capacity`, as it borrows external memory. Also, it is a value type, and it makes a user allocate a buffer for characters on the stack.  Thus, the whole instance of a type is put on the stack together with its contents and the issue of optimization is solved. As there is no need to allocate memory on the heap, there are no problems with a decrease in performance when dealing with the heap. So, you might have a question:  why don’t we always use `ValueStringBuilder` (or its custom analog as we cannot use the original because it is internal)? The answer is:  it depends on a task. Will a resulting string have a definite size? Will it have a known maximum length? If you answer “yes” and if the string doesn’t exceed reasonable boundaries, you can use the value version of `StringBuilder`. However, if you expect lengthy strings, use the usual version.

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

The second type of data that I especially want to note is the `ValueListBuilder` type. It is used when you need to create a collection of elements for a short time and pass it to an algorithm for processing.

Admit,  this task looks pretty similar to the `ValueStringBuilder` task. And it is solved in a similar way:

** File [ValueListBuilder.cs (https://github.com/dotnet/coreclr/blob/dbaf2957387c5290a680c8918779683194137b1d/src/System.Private.CoreLib/shared/System/Collections/Generic/ValueListBuilder.cs)**

To put it clearly, these situations are often. However, previously we solved the issue another way. We used to create a `List`, fill it with data and lose the reference to it. If the method is called frequently, this will lead to a sad situation: many `List` instances (and associated arrays) get suspended on the heap. Now this problem is solved: no additional objects will be created. However, as in case of `ValueStringBuilder` it is solved only for Microsoft programmers:  this class has the `internal` modifier.

### Rules and use practice

To fully understand the new type of data you need to play with it by writing two or three or better more methods that use it. However, it is possible to learn main rules right now:

  - If your method processes some input dataset without changing its size, you may try to stick to the `Span` type. If you are not going to modify buffer, choose the `ReadOnlySpan` type;
  - If your method handles strings calculating some statistics or parsing these strings, it _must_ accept `ReadOnlySpan<char>`. Must is a new rule. Because when you accept a string, you make somebody create a substring for you;
  - If you need to create a short data array (no more than 10 kB) for a method, you can easily arrange that using `Span<TType> buf = stackalloc TType[size]`. Note that TType should be a value type as `stackalloc` works with value types only.

In other cases, you’d better look closer at `Memory` or use classic data types.

### How does Span work?

I would like to say a few additional words on how `Span` functions and why it is that notable. And there is something to talk about. This type of data has two versions:  one for .NET Core 2.0+ and the other for the rest.

**File [Span.Fast.cs, .NET Core 2.0] (https://github.com/dotnet/coreclr/blob/38403e661a4202ca4c8a72e4bbd9a263bddeb891/src/System.Private.CoreLib/shared/System/Span.Fast.cs)**

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

The thing is that _huge_ .NET Framework and .NET Core 1.* don’t have a garbage collector updated in a special way (unlike .NET Core 2.0+) and they have to use an additional pointer  to the beginning of a buffer in use. That means, that internally `Span` handles managed .NET objects as though they are unmanaged. Just look into the second variant of the structure: it has three fields. The first one is a reference to a manged object. The second one is the offset in bytes from the beginning of this object, used to define the beginning of the data buffer (in strings this buffer contains `char` characters while in arrays it contains the data of an array). Finally, the third field contains the quantity of elements in the buffer laid in a row. 

Let’s see how `Span` handles strings, for example:

**File [coreclr::src/System.Private.CoreLib/shared/System/MemoryExtensions.Fast.cs] (https://github.com/dotnet/coreclr/blob/2b50bba8131acca2ab535e144796941ad93487b7/src/System.Private.CoreLib/shared/System/MemoryExtensions.Fast.cs#L409-L416)**

```csharp
public static ReadOnlySpan<char> AsSpan(this string text)
{
    if (text == null)
        return default;

    return new ReadOnlySpan<char>(ref text.GetRawStringData(), text.Length);
}
```

Where `string.GetRawStringData()` looks the following way:

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

It turns out the method directly accesses the inside of the string, while the `ref char` specification allows GC to track an unmanaged reference to that inside of the string by moving it together with the string when GC is active.

The same thing is with arrays:  when `Span` is created, some internal JIT code calculates the offset for the beginning of the data array and initializes `Span` with this offset. The way you can calculate the offset for strings and arrays was discussed in the chapter about the structure of objects in memory (.\ObjectsStructure.md).

### Span\<T> as a returned value

Despite all the harmony, `Span` has some logical but unexpected constraints on its return from a method. If we look at the following code:

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

we can see it is logical and good. However, if we replace one instruction with another:

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

a compiler will prohibit it. Before I say why, I would like you to guess which problems this construct brings.

Well, I hope you thought, guessed and maybe even understood the reason. If yes, my efforts to writing a detailed chapter about a [thread stack] (./ThreadStack.md) paid off. Because when you return a reference to local variables from a method that finishes its work, you can call another method, wait until it finishes its work too, and then read values of those local variables using x[0.99].

Fortunately, when we attempt to write such code a compiler slaps on our wrists by warning: `CS8352 Cannot use local 'reff' in this context because it may expose referenced variables outside of their declaration scope`. The compiler is right because if you bypass this error, there will be a chance, while in a plug-in, to steal the passwords of others or to elevate  privileges for running our plug-in.

## Memory\<T> and ReadOnlyMemory\<T>

There are two visual differences between `Memory<T>` and `Span<T>`. The first one is that `Memory<T>` type doesn’t contain `ref` modifier in the header of the type. In other words, the `Memory<T>` type can be allocated both on the stack while being either a local variable, or a method parameter, or its returned value and on the heap, referencing some data in memory from there. However, this small difference creates a huge distinction in the behavior and capabilities of `Memory<T>` compared to `Span<T>`. Unlike `Span<T>` that is an *instrument* for some methods to use some data buffer, the `Memory<T>` type is designed to store information about the buffer, but not to handle it. Thus, there is the difference in API.

  - `Memory<T>` doesn’t have methods to access the data that it is responsible for. Instead, it has the `Span` property and the `Slice` method that return an instance of the `Span` type.
  - Additionally, `Memory<T>` contains the `Pin()` method used for scenarios when a stored buffer data should be passed to `unsafe` code. If this method is called when memory is allocated in .NET, the buffer will be pinned and will not move when GC is active. This method will return an instance of the `MemoryHandle` structure, which encapsulates `GCHandle` to indicate a segment of a lifetime and to pin array buffer in memory.

However, I suggest we get familiar with the whole set of classes. First, let’s look at the `Memory<T>` structure itself (here I show only those type members that I found most important):

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

As we see the structure contains the constructor based on arrays, but stores data in the object. This is to additionally reference  strings that don’t have a constructor designed for them, but can be used with the `AsMemory()` `string` method, it returns `ReadOnlyMemory`. However, as both types should be binary similar, `Object` is the type of the `_object` field.

Next, we see two constructors based on `MemoryManager`. We will talk about them later. The properties of obtaining `Length` (size) and `IsEmpty` check for an empty set. Also, there is the `Slice` method for getting a subset as well as `CopyTo` and `TryCopyTo` methods of copying.

Talking about `Memory` I want to describe two methods of this type in detail:  the `Span` property and the `Pin` method.

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

Namely, the lines that handle strings management. They say that if we convert `ReadOnlyMemory<T>` to `Memory<T>` (these things are the same in binary representation and there is even a comment these types must coincide in a binary way as one is produced from another by calling `Unsafe.As`) we will get an ~access to a secret chamber~ with an opportunity to change strings. This is an extremely dangerous mechanism:

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

This mechanism combined with string interning can produce dire consequences.

### Memory\<T>.Pin

The second method that draws strong attention is `Pin`:

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

It is also an important instrument for unification  because if we want to pass a buffer to unmanaged code, we just need to call the `Pin()` method and pass a pointer to this code no matter what type of data `Memory<T>` refers to. This pointer will be stored in the property of a resulting structure.

```csharp
void PinSample(Memory<byte> memory)
{
    using(var handle = memory.Pin())
    {
        WinApi.SomeApiMethod(handle.Pointer);
    }
}
```

It doesn’t matter what `Pin()` was called for in this code: it can be `Memory` that represents either `T[]`, or a `string` or a buffer of unmanaged memory. Merely arrays and string will get a real `GCHandle.Alloc(array, GCHandleType.Pinned)` and in case of unmanaged memory nothing will happen.

## MemoryManager, IMemoryOwner, MemoryPool

Besides indicating structure fields, I want to note that there are two other `internal` type constructors based on an other entity – `MemoryManager`. This is not a classic memory manager that you might have thought of and we are going to talk about it later. classic memory manager that you might have thought of and we are going to talk about it later. Like `Span`, `Memory` has a reference to a navigated object, an offset, and a size of an internal buffer. Note that you can use the `new` operator to create `Memory` from an array only. Or, you can use extension methods to create `Memory` from a string, an array or `ArraySegment`. I mean it is not designed to be created from unmanaged memory manually. However, we can see that there is an internal method to create this structure using `MemoryManager`.

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

This structure indicates the owner of a memory range. In other words, `Span` is an instrument to work with memory, `Memory` is a tool to store the information about a particular memory range and `MemoryManager` is a tool to control the lifetime of this range, i.e. its owner. For example, we can look at `NativeMemoryManager<T>` type. Although it is used for tests, this type clearly represents the concept of “ownership”.

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

That means the class allows for nested calls of the `Pin()` method, thus counting generated references from the `unsafe` world.

Another entity closely tied with `Memory` is `MemoryPool` that pools `MemoryManager` instances (`IMemoryOwner` in fact):

**File [MemoryPool.cs](https://github.com/dotnet/corefx/blob/f592e887e2349ed52af6a83070c42adb9d26408c/src/System.Memory/src/System/Buffers/MemoryPool.cs)**

```csharp
public abstract class MemoryPool<T> : IDisposable
{
    public static MemoryPool<T> Shared => s_shared;

    public abstract IMemoryOwner<T> Rent(int minBufferSize = -1);

    public void Dispose() { ... }
}
```

It is used to lease buffers of a necessary size for temporary use. The leased instances with implemented `IMemoryOwner<T>` interface have the `Dispose()` method to return the leased array back to the pool of arrays. By default, you can use the shareable pool of buffers built on `ArrayMemoryPool`:

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

Based on this architecture, we have the following picture:

  – `Span` data type should be used as a method parameter if you want to read data (`ReadOnlySpan`) or read and write data (`Span`). However, it is not supposed to be stored in a field of a class for future use.
  – If you need to store a reference from a field of a class to a data buffer, you need to use `Memory<T>` or `ReadOnlyMemory<T>` depending on your goals.
  – `MemoryManager<T>` is the owner of a data buffer (optional ). It may be necessary if you need to count `Pin()` calls for example. Or, if you need to know how to release memory.
  – If `Memory` is built around an unmanaged memory range, `Pin()` can do nothing. However, this uniforms working with different types of buffers:  for both managed and unmanaged code the interaction interface will be the same.  
  – Every type has public constructors. That means you can use `Span` directly or get its instance from `Memory`. For `Memory` as such, you can create it individually or you can create a memory range owned by `IMemoryOwner` and referenced by `Memory`. Any type based on `MemoryManger` can be regarded as a specific case which it owns some local memory range (e.g. accompanied by counting the references from the `unsafe` world). In addition, if you need to pool such buffers (the frequent traffic of almost equally sized buffers is expected) you can use the `MemoryPool` type.
  – If you intend to work with `unsafe` code by passing a data buffer there, you should use the `Memory` type which has the `Pin()` method that automatically pins a buffer on the .NET heap if it was created there.
  – If you have some traffic of buffers (for example you parse a text of a program or DSL), it is better to use the `MemoryPool` type. You can properly implement it to output the buffers of a necessary size from a pool (for example a slightly bigger buffer if there is no suitable one, but using `originalMemory.Slice(requiredSize)` to avoid pool fragmentation).

## Performance

To measure the performance of new data types I decided to use a library that has already become standard [BenchmarkDotNet](https://github.com/dotnet/BenchmarkDotNet):

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

Now, let’s see the results.

![Performance chart](./imgs/Span/Performance.png)

Looking at them we can get the following information:

  - `ArraySegment` is awful. But if you wrap it in `Span` you can make it less awful. In this case, performance will increase 7 times.
  - If we consider .NET Framework 4.7.1 (the same thing is for 4.5), the use of `Span` will significantly lower the performance when working with data buffers. It will decrease by 30–35 %.
  - However, if we look at .NET Core 2.1+ the performance remains similar or even increases given that `Span` can use a part of a data buffer, creating the context.  The same functionality can be found in `ArraySegment`, but it works very slowly.

Thus, we can draw simple conclusions regarding the use of these data types:

  - for `.NET Framework 4.5+` и `.NET Core` they have the only advantage:  they are faster than `ArraySegment` when dealing with a subset of an original array;
  - in `.NET Core 2.1+` their use gives an undeniable advantage over both `ArraySegment` and any manual implementation of `Slice`;
  - all three ways are as productive as possible and that cannot be achieved with any tool to unify arrays.

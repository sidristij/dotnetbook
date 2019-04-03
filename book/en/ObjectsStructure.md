# The structure of objects in memory

> [A link to the discussion](https://github.com/sidristij/dotnetbook/issues/56)

In previous chapters, we talked about the difference between value and reference types from a developer’s point of view. We have never looked at how they are designed in reality and what techniques are implemented in them at the level of the CLR. In fact, we were looking at the final result and described the work of these types in terms of a black box. However, to get a deeper insight and to reject any idea about any magic inside the CLR, we should look into its internals and study the algorithms that rule the work of the type system.

## The internal structure of the type instances

Before we start talking about the structure of control blocks in the type system, let’s look at an object itself, i.e. an instance of any class. If we create an instance of any reference type in memory, whether it is a class, or a encapsulated structure, it will consist of three fields: `SyncBlockIndex` (which is in fact not only it), a pointer to a type descriptor and data. The data region can contain a lot of fields. That said, we however give the below examples as if they have only one field in data. So, this is how our visualization of this structure would look like:

**System.Object**

```

  ----------------------------------------------
  |  SyncBlkIndx |   VMT_Ptr    |     Data     |
  ----------------------------------------------
  |  4 / 8 byte  |  4 / 8 byte  |  4 / 8 byte  |
  ----------------------------------------------
  |  0xFFF..FFF  |  0xXXX..XXX  |      0       |
  ----------------------------------------------
                 ^
                 | Here is the place indicated by a reference to an object. I mean not the beginning, but the VMT.

  Sum size = 12 (x86) | 24 (x64)
```

Thus, the size of type instances in fact depends on the platform an app will work on.

Next, let’s follow the `VMT_Ptr` and see what kind of data structure is at this address. This pointer is the most important for the whole type system as it is used for the inheritance, interface implementation, type casting and so on. This pointer is a reference to the .NET CLR type system, a kind of type ID used by CLR to cast types or to measure the size of memory allocated to an object. It is this pointer that allows GC to go through an object graph so swiftly and to determine which addresses are owned by the pointers to objects and which – by values. Also, with this pointer you can learn everything about an object and make the CLR handle it differently. That’s why we will study this pointer in detail.

### Virtual Method Table structure

The description of the table itself is available at [GitHub CoreCLR](https://github.com/dotnet/coreclr/blob/master/src/vm/methodtable.h). Leaving aside unnecessary details (and there are 4,381 lines there), [it looks this way](https://github.com/dotnet/coreclr/blob/master/src/vm/methodtable.h#L4099-L4114): 

> This is the version from CoreCLR. If we look at the structure of fields in .NET Framework, it will be different in the order of fields and the order of system information bits from `m_wFlags` and `m_wFlags2` bit fields.

 ```cpp
    // Low WORD is component size for array and string types (HasComponentSize() returns true).
    // Used for flags otherwise.
    DWORD m_dwFlags;

    // Base size of instance of this class when allocated on the heap
    DWORD m_BaseSize;

    WORD  m_wFlags2;

    // Class token if it fits into 16-bits. If this is (WORD)-1, the class token is stored in the TokenOverflow optional member.
    WORD  m_wToken;

    // <NICE> In the normal cases we shouldn't need a full word for each of these </NICE>
    WORD  m_wNumVirtuals;
    WORD  m_wNumInterfaces;
 ```

 Admit, this looks somewhat scary. However, it is scary not because we see here 6 fields only (where are the rest?), but because you need to skip 4,100 lines of code to get to these fields. Personally, I expected to see here something ready-made that you don’t need to calculate additionally. However, it is nowhere near as easy: since any type can contain a different number of methods and interfaces, the VMT can have a variable size. It means that to fill it you need to calculate where the rest of the fields are. However, let’s stay positive and try to benefit from what we already have: we still don’t know what is meant by other fields (except the last two), but the `m_BaseSize` field looks interesting. As a commentary suggests, this is the actual size of an instance of a type. We have just found `sizeof` for classes! It is time to try it in practice.

 We can follow two ways to get a VMT address. The difficult one is to obtain an address of an object and hence the VMT:

 ```csharp
class Program
{
    public static unsafe void Main()
    {
        Union x = new Union();
        x.Reference.Value = "Hello!";

        // The first field contains a pointer to the location of VMT pointer.
        // - (IntPtr*)x.Value.Value - here we casted the number into the pointer (we changed the type for the compiler)
        // - *(IntPtr*)x.Value.Value - here we get the address of the VMT using the address of the object
        // - (void *)*(IntPtr*)x.Value.Value - here we casted it into the pointer
        void *vmt = (void *)*(IntPtr*)x.Value.Value;

        // here we output the VMT address to the console;
        Console.WriteLine((ulong)vmt);
    }

    [StructLayout(LayoutKind.Explicit)]
    public class Union
    {
        public Union()
        {
            Value = new Holder<IntPtr>();
            Reference = new Holder<object>();
        }

        [FieldOffset(0)]
        public Holder<IntPtr> Value;

        [FieldOffset(0)]
        public Holder<object> Reference;
    }

    public class Holder<T>
    {
        public T Value;
    }
}
 ```

Or, we can follow the simple way and use .NET FCL API:

```csharp
    var vmt = typeof(string).TypeHandle.Value;
```

The second way is simpler (though works slower). However, knowing the first one is very important to understand the structure of a type instance. Using the second way provides the sense of confidence: we comply with documented approach of handling VMT by calling an API method, and we are incompliant by using a pointer. However, we shouldn't forget that storing a `VMT *` is generally a standard in practically every OOP language and .NET platform: the reference is always at the same place as it is the most frequently used field of a class. And this most frequently used field of a class should go first, so that addressing would take place without offsetting and, as a result, quicker. Thus, we make a conclusion that for classes the order of fields will not impact the speed. However in structures the most frequently used field should go first. Although this mostly won’t influence .NET applications as this platform was developed for other tasks in mind.

Let’s study the structure of types with respect to the size of their instances. We won’t learn them in theory (it is boring), instead we will try to get the benefits that we will be unable to get in a usual way.

> **Why there is sizeof for a Value Type, but not for a Reference Type?** Actually, this is an open question as no one stops you from calculating the size of a reference type. The only thing you can stumble on is the variable size of two reference types: `Array` and `String`. As well as `Generic` group, that fully depends on particular variants. That means we cannot do without the `sizeof(..)` operator as we need to work with particular instances. However, no one stops the CLR team from implementing the method like `static int System.Object.SizeOf(object obj)` form that would return us what we need in a simple way. Why haven’t Microsoft implemented this method? I guess they think the .NET is not the platform where a developer will worry much about particular bytes. If necessary, you can always add more memory modules to your mother board. Especially, since most data types we implement, don’t take such big volumes of memory.

However, let’s go back to our issue. So, to get the size of a class instance (with a fixed size), you just need to write the following code:

```csharp
unsafe int SizeOf(Type type)
{
    MethodTable *pvmt = (MethodTable *)type.TypeHandle.Value.ToPointer();
    return pvmt->Size;
}

[StructLayout(LayoutKind.Explicit)]
public struct MethodTable
{
    [FieldOffset(4)]
    public int Size;
}

class Sample
{
    int x;
}

// ...

Console.WriteLine(SizeOf(typeof(Sample)));
```

So, what have we done? Initially, we got the pointer to the VMT. Next, we read the size and get `12` that is the aggregate size of `SyncBlockIndex + VMT_Ptr + x` fields for a 32-bit platform. If we play with different types, we will get something like the following table for x86:

A type or its definition | Size   | Comment
------------------------|-----------|--------------
Object | 12 | SyncBlk + VMT + empty field
Int16 | 12 | Boxed Int16: SyncBlk + VMT + data (4 bytes aligned)
Int32 | 12 | Boxed Int32: SyncBlk + VMT + data
Int64 | 16 | Boxed Int64: SyncBlk + VMT + data
Char | 12 |  Boxed Char: SyncBlk + VMT + data (4 bytes aligned)
Double | 16 | Boxed Double: SyncBlk + VMT + data
IEnumerable | 0 | The interface doesn’t have a size: we should use obj.GetType()
List\<T> | 24 | It doesn’t matter how many elements are there in List<T>. It will require the same size of memory as it stores data in the array that is not counted.
GenericSample\<int> | 12 | As you see, generics are perfectly counted. The size hasn’t changed as the data is in the same place as in case of `boxed int`. The result: SyncBlk + VMT + data
GenericSample\<Int64> | 16 | Likewise
GenericSample\<IEnumerable> | 12 | Likewise
GenericSample\<DateTime> | 16 | Likewise
string | 14 | This value will be returned for any string as the real size should be calculated dynamically. However, it is suitable for the empty string size. Note that the size is not aligned with number of bits: in fact this field is not intended for use.
int[]{1} | 24554 | For arrays, completely different data is stored here. In addition, their size is not fixed and should be calculated separately.

As you see, when the system stores data about the size of an instance, in fact it stores data for a reference version of this type (i.e. including the reference version of a value type). Let’s make some conclusions:

  1. If you want to know how much memory a value type will take as a value, use `sizeof(TType)`.
  1. If you want to calculate the cost of boxing, you can round `sizeof(TType)` up to the size of a processor word (4 or 8 bytes) and add 2 more words (`2 * sizeof(IntPtr)`). Or, you can get this value from the `VMT` of a type.
  1. The calculation of the memory amount allocated on the heap is represented for the following types:
     1. The usual fixed size reference type: we can get the size of an instance from a `VMT`;
     1. For a string, you should calculate its size manually (it is rarely required, but, you should admit, it is interesting).
     1. The size of an array is calculated separately based on the size and quantity of its elements. This task could be more useful as arrays are most likely to get into `LOH`.

### System.String

We will talk about strings in terms of practice separately: this relatively small class is worth a whole chapter. And in the chapter about the design of a VMT we will talk about the low-level design of strings. Strings are stored based on UTF16. It means that each character takes 2 bytes. Additionally each string ends with a null terminator — a special value that indicates the end of a string. An instance also stores the length of a string as a `Int32` number to avoid counting the length each time when necessary (we will talk about encoding separately). The diagram below represents the information about memory allocated for a string:

```
  // For.NET Framework 3.5 and earlier
  ----------------------------------------------------------------------------------------------------
  |  SyncBlkIndx |    VMTPtr     |  ArrayLength   |     Length     |   char   |   char   |   Term    |
  ----------------------------------------------------------------------------------------------------
  |  4 / 8 bytes  |  4 / 8 bytes   |    4 bytes     |    4 bytes    |  2 bytes |  2 bytes |  2 bytes |
  ----------------------------------------------------------------------------------------------------
  |      -1      |  0xXXXXXXXX   |        3       |        2       |     a    |     b    |   <nil>   |
  ----------------------------------------------------------------------------------------------------

  Term – null terminator
  Sum size = (8|16) + 2 * 4 + Count * 2 + 2 -> round up based on the bytes alignment. (24 bytes in example)
  Count is the number of characters in a string, excluding a terminator character.
  
  // For .NET Framework 4 and later
  -----------------------------------------------------------------------------------
  |  SyncBlkIndx |    VMTPtr     |     Length     |   char   |   char   |   Term    |
  -----------------------------------------------------------------------------------
  |  4 / 8 bytes  |  4 / 8 bytes   |    4 bytes     |    2 bytes |  2 bytes |  2 bytes |
  -----------------------------------------------------------------------------------
  |      -1      |  0xXXXXXXXX   |        2       |     a    |     b    |   <nil>   |
  -----------------------------------------------------------------------------------
  Term – null terminator
  Sum size = (8|16) + 4 + Count * 2 + 2) -> round up based on the bytes alignment. (20 bytes in example)
  Count is the number of characters in a string, excluding a terminator character.
 ```
Let’s rewrite our method so it could calculate the size of strings:

 ```csharp
unsafe int SizeOf(object obj)
{
    var majorNetVersion = Environment.Version.Major;
    var type = obj.GetType();
    var href = Union.GetRef(obj).ToInt64();
    var DWORD = sizeof(IntPtr);
    var baseSize = 3 * DWORD;

    if (type == typeof(string))
    {
        if (majorNetVersion >= 4)
        {
            var length = (int)*(int*)(href + DWORD /* skip vmt */);
            return DWORD * ((baseSize + 2 + 2 * length + (DWORD-1)) / DWORD);
        }
        else
        {
            // on 1.0 -> 3.5 string have additional RealLength field
            var arrlength = *(int*)(href + DWORD /* skip vmt */);
            var length = *(int*)(href + DWORD /* skip vmt */ + 4 /* skip length */);
            return DWORD * ((baseSize + 4 + 2 * length + (DWORD-1)) / DWORD);
        }
    }
    else
    if (type.BaseType == typeof(Array) || type == typeof(Array))
    {
        return ((ArrayInfo*)href)->SizeOf();
    }
    return SizeOf(type);
}
```

Where `SizeOf(type)` will call the previous implementation for fixed size reference types.

Let’s check the code in practice.

```csharp
    Action<string> stringWriter = (arg) =>
    {
        Console.WriteLine($"Length of `{arg}` string: {SizeOf(arg)}");
    };

    stringWriter("a");
    stringWriter("ab");
    stringWriter("abc");
    stringWriter("abcd");
    stringWriter("abcde");
    stringWriter("abcdef");
}

-----

Length of `a` string: 16
Length of `ab` string: 20
Length of `abc` string: 20
Length of `abcd` string: 24
Length of `abcde` string: 24
Length of `abcdef` string: 28
```

Calculations indicate that the size of a string increases not at a linear rate but incrementally, by every two characters. This happens because the size of each character is 2 bytes and the ultimate size of string should be aligned by bytes according to CPU architecture (x86 in this case). That is why a string is aligned by 2 bytes. The result of our work is excellent: we can calculate the cost of any string. The last thing we need to know is how to calculate the size of arrays in memory.

### Arrays

The structure of arrays is a little more complicated as it has several variants:

  1. They can store both value and reference types.
  1. They can be both single-dimensional and multi-dimensional.
  1. Each dimension can start with `0` as well as with any other number (which is, in my opinion, a very questionable option designed to eliminate the need to write `arr[i - startIndex]` at FCL level). This is done for a sort of compatibility with other languages. For example in Pascal array indexing can start with any number and not only from `0`. However, I think it is unnecessary.

It produces some confusion in the implementation of arrays and inability to predict the size of a resulting array exactly: it is not enough to multiply the number of elements by their sizes. However for the majority of cases that will be enough, of course. The size becomes important if we afraid of getting into LOH. But even here we have some variants: we can add some constant value (for example 100) to a size that we calculated in a quick way to understand whether we have passed the threshold of 85,000 or not. However, in terms of this chapter our task is different — to understand the structure of types. Let’s go to it:

```
  // Header
  ----------------------------------------------------------------------------------------
  |   SBI   |  VMT_Ptr |  Total  |  Len_1  |  Len_2  | .. |  Len_N  |  Term   | VMT_Child |
  ----------------------------------opt-------opt------------opt-------opt--------opt-----
  |  4 / 8  |  4 / 8   |    4    |    4    |    4    |    |    4    |    4    |    4/8    |
  ----------------------------------------------------------------------------------------
  | 0xFF.FF | 0xXX.XX  |    ?    |    ?    |    ?    |    |    ?    | 0x00.00 | 0xXX.XX  |
  ----------------------------------------------------------------------------------------

  - opt: optional
  - SBI: Sync Block Index
  - VMT_Child: present only if an array stores the data of a reference type
  - Total: present for optimization. The total number of array elements with all dimensions taken in account.
  - Len_2..Len_N, Term: present only if the dimension of an array is more than 1 (regulated by bits in VMT->Flags)
```

As we can see, the header of a type contains the information about array dimensions the number of which can be from 1 to many. In fact, their size is limited only by a null terminator, meaning that enumeration is over. The following example is fully available at [GettingInstanceSize](./samples/GettingInstanceSize.linq). Below, I will put only its most important part: 

```csharp
public int SizeOf()
{
    var total = 0;
    int elementsize;

    fixed (void* entity = &MethodTable)
    {
        var arr = Union.GetObj<Array>((IntPtr)entity);
        var elementType = arr.GetType().GetElementType();

        if (elementType.IsValueType)
        {
            var typecode = Type.GetTypeCode(elementType);

            switch (typecode)
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Boolean:
                    elementsize = 1;
                    break;
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Char:
                    elementsize = 2;
                    break;
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Single:
                    elementsize = 4;
                    break;
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Double:
                    elementsize = 8;
                    break;
                case TypeCode.Decimal:
                    elementsize = 12;
                    break;
                default:
                    var info = (MethodTable*)elementType.TypeHandle.Value;
                    elementsize = info->Size - 2 * sizeof(IntPtr); // sync blk + vmt ptr
                    break;
            }
        }
        else
        {
            elementsize = IntPtr.Size;
        }

        // Header
        total += 3 * sizeof(IntPtr); // sync blk + vmt ptr + total length
        total += elementType.IsValueType ? 0 : sizeof(IntPtr); // MethodsTable for refTypes
        total += IsMultidimensional ? Dimensions * sizeof(int) : 0;
    }

    // Contents
    total += (int)TotalLength * elementsize;

    // align size to IntPtr
    if ((total % sizeof(IntPtr)) != 0)
    {
        total += sizeof(IntPtr) - total % (sizeof(IntPtr));
    }
    return total;
}
```

This code takes into account all the variants of array types and can be used to calculate the size of an array:

```csharp
Console.WriteLine($"size of int[]{{1,2}}: {SizeOf(new int[2])}");
Console.WriteLine($"size of int[2,1]{{1,2}}: {SizeOf(new int[1.2])}");
Console.WriteLine($"size of int[2,3,4,5]{{...}}: {SizeOf(new int[2, 3, 4, 5])}");

---
size of int[]{1,2}: 20
size of int[2,1]{1,2}: 32
size of int[2,3,4,5]{...}: 512
```

### Conclusions to a section

At this point we have learned important things. Firstly, we divided reference types into three groups: fixed size, variable size and generic ones. We also learned the way to understand the structure of a final instance of any type (I’m not talking about the structure of a VMT now. For now, we have entirely understood just one field, a big achievement, by the way). It can be a fixed size reference type (everything is pretty simple there) or a reference type with an undefined size: an array or a string. I mean undefined because its size will be defined once this type is created. Generic types are quite simple: each particular generic type has its own VMT, generated for this type, indicating its particular size.

## Design of Virtual Method Table (VMT)

Explaining how a Method Table works is mainly a academic exercise: stepping in that maze is like digging your own grave. On the one hand, this labyrinth contains something exciting and interesting, some data that reveal even more about what is going on. However, on the other hand we understand that Microsoft doesn’t guarantee they will keep the runtime without changes and, for example, will not move the method table one field forward. That’s why let’s make things clear:

> the information presented in this section is given only for you to understand how a CLR-based application works and that manual interference with its work doesn’t guarantee anything. However, it is so interesting, that I cannot talk you out. On the contrary, I suggest playing with these data structures and maybe you will get one of the most memorable experiences in software development.

OK, I warned you. Now, let’s plunge into the mirror world. Because, until now stepping into the mirror world meant knowing the structure of objects, which we are supposed to now at least approximately. This knowledge is not a mirror-world itself, but the entrance into it. That’s why let’s get back to the structure of a ```MethodTable```, [described in CoreCLR](https://github.com/dotnet/coreclr/blob/master/src/vm/methodtable.h#L4099-L4114):

 ```cpp
    // Low WORD is component size for array and string types (HasComponentSize() returns true).
    // Used for flags otherwise.
    DWORD m_dwFlags;

    // Base size of instance of this class when allocated on the heap
    DWORD m_BaseSize;

    WORD  m_wFlags2;

    // Class token if it fits into 16-bits. If this is (WORD)-1, the class token is stored in the TokenOverflow optional member.
    WORD  m_wToken;

    // <NICE> In the normal cases we shouldn't need a full word for each of these </NICE>
    WORD  m_wNumVirtuals;
    WORD  m_wNumInterfaces;
 ```

 In particular, let’s turn to `m_wNumVirtuals` and `m_wNumInterfaces` fields. They define the answer to the question "How many virtual methods and interfaces does a type have?". This structure doesn’t contain any information about usual methods, fields and properties (which unite methods). I mean this structure **has nothing to do with reflection**. In its sense and purpose this structure is made to provide functioning of method calls in the CLR (in any OOP actually, whether it is Java, C++, Ruby or something else, however the arrangement of fields will be different). Let’s examine some code:

 ```csharp
 public class Sample
 {
     public int _x;

     public void ChangeTo(int newValue)
     {
         _x = newValue;
     }

     public virtual int GetValue()
     {
         return _x;
     }
 }
 
 public class OverriddenSample : Sample
 {
     public override GetValue()
     {
         return 666;
     }
 }
 
 ```

No matter how meaningless these classes seems to be, they are fit for a description in a VMT. And for that, we should understand what’s the difference between a base type and an inherited type in terms of `ChangeTo` and `GetValue` methods.

The `ChangeTo` method is present in both types and cannot be overridden. That means we can rewrite it in the following way and nothing will change:

```csharp
 public class Sample
 {
     public int _x;

     public static void ChangeTo(Sample self, int newValue)
     {
         self._x = newValue;
     }

     // ...
 }

// Or, if it was a struct
 public struct Sample
 {
     public int _x;

     public static void ChangeTo(ref Sample self, int newValue)
     {
         self._x = newValue;
     }

     // ...
 }
```

This code will change only in terms of architecture. Trust me, when compiled both variants will work in the same way as `this` in instance methods is just the first method parameter, that is passed to us implicitly.

> I should explain in advance why all considerations are based on examples with static methods: all methods are static in their essence,  including instance ones. Memory doesn’t contain compiled methods for every single instance of a class. This would need a huge amount of memory: it is much easier that a reference to an instance of a structure or a class is passed each time to a method that works with that structure or the class.

Things work different for the `GetValue` method. We cannot just override a method by overriding *static* `GetValue` in an inherited type: a new method will get only those regions of code, that deal with a variable as with `OverriddenSample`. If you work with a variable as with `Sample` base type variable, you can only call `GetValue` of a base type, as you don’t know the ultimate type of an object. To understand the type of a variable and which method is called, we can do the following:

```csharp
void Main()
{
    var sample = new Sample();
    var overridden = new OverridedSample();

    Console.WriteLine(sample.Virtuals[Sample.GetValuePosition].DynamicInvoke(sample));
    Console.WriteLine(overridden.Virtuals[Sample.GetValuePosition].DynamicInvoke(sample));
}

public class Sample
{
    public const int GetValuePosition = 0;

    public Delegate[] Virtuals;

    public int _x;

    public Sample()
    {
        Virtuals = new Delegate[1] { 
            new Func<Sample, int>(GetValue) 
        };
    }

    public static void ChangeTo(Sample self, int newValue)
    {
        self._x = newValue;
    }

    public static int GetValue(Sample self)
    {
        return self._x;
    }
}

public class OverriddenSample : Sample
{
    public OverriddenSample() : base()
    {
        Virtuals[0] = new Func<Sample, int>(GetValue);
    }

    public static new int GetValue(Sample self)
    {
        return 666;
    }
}
```

In this example we, in fact, build a VMT manually and call methods based on their position in this table. If you got the idea of this example, you in fact got the idea of how inheritance is built at the level of compiled code: methods are called based on their indexes in a VMT. Just when you create an instance of an inherited type, the places in its VMT, where a base type has virtual methods, will be filled by a compiler with pointers to overridden methods. To do this, the compiler copies pointers to methods which weren’t overridden from the base type. Thus, the only difference between our example and a real VMT is that when a compiler builds this table, it preemptively knows the right size and contents of that table. In our example, we will have to struggle a lot to build a table for types, that will make it larger by adding new methods. But we our goal is different, so we won’t deal with such a nonsense.

The second question is why there are interfaces in a `VMT` when it is all clear with methods. If we think logically, interfaces are not included in the structure of direct inheritance. They are something on the side, indicating that some types must implement a certain set of methods, i.e. must have a protocol for interaction. Although these interfaces are *on the side* of direct inheritance, you still can call methods. Note, that if you use a variable of an interface type, beyond this variable there may be any classes; and in some cases the only common base class of these classes may be `System.Object` only. That means that methods in a virtual method table that implement an interface can be located anywhere. So how do method calls work in this case?

## Virtual Stub Dispatch (VSD) [In Progress]

To address this issue, it is necessary to remember that we can implement an interface in two ways: we can choose either `implicit` or `explicit` implementation. Moreover, you can do it partially: some methods will be `implicit`, the other – `explicit`. In fact, this opportunity is the consequence of implementation and probably wasn’t initially planned: by implementing an interface you explicitly or implicitly show what’s inside that interface. Some class methods may not be included into an interface, some methods within an interface may not exist in a class (of course they exist in a class, but syntax shows that they don’t belong to that class architecturally): a class and an interface are parallel hierarchies of types in some sense. Also, an interface is a separate type. It means that every interface has its own VMT so that anybody could call the methods of an interface.

Let’s look at the table to see how VMTs of different types could be like:

| interface IFoo  |  class A : IFoo  | class B : IFoo |
------------------|------------------|-----------------
| -> GetValue()   |  SampleMethod()  | RunProcess()   |
| -> SetValue()   |  Go()            | -> GetValue()  |
|                 | -> GetValue()    | -> SetValue()  |
|                 | -> SetValue()    | LookToMoon()   |

The VMTs of all three types contain the necessary `GetValue` and `SetValue` methods. However, they are located at different indexes: their index cannot be the same globally as it would cause a collision between indexes of other interfaces of a class. Actually, every interface duplicated each time it’s implemented by a class. So, if we have 633 implementations of `IDisposable` in FCL/BCL classes,  we also have 633 duplicates of `IDisposable` interfaces to support the VMT to VMT translation across every class + a entry in each class with a reference to its interface implementation. We will call such interfaces **private interfaces**. That is each class has its own **private interfaces** that are "system” and act as proxy types to non-virtual interfaces.

Thus, interfaces, just like classes, inherit virtual *interface* methods. However, this inheritance works not only when we inherit one interface from an other but also when a class implements an interface. When a class implements an interface, an additional interface is created that specifies which methods of a parent interface should be inherited from and referenced to in the target class. By calling a method through an interface identifier, you similarly call a method through the index from a VMT as it was in case of classes. However, for this interface implementation you use a slot ID to choose a slot from an *inherited* private interface, through which the original `IDisposable` interface is connected with our class that implements the interface.

The dispatch of virtual methods by stubs or **Virtual Stub Dispatch (VSD)** was developed in 2006 as a replacement for VMTs, specifically in interfaces. This approach simplifies generation of code and method calls as the initial implementation of interfaces with VMT would be very cumbersome and need a huge amount of time and working set to build all structures of all interfaces. The code of dispatching is in four files containing 6,400 lines, and we don’t intend to understand the whole thing. We will try to understand the process in this code in general terms.

The whole logic of VSD can be divided into two big sections: dispatch and the mechanism of stubs that caches the addresses of called methods based on a key pair — [typeID; slot No] — that identifies them.

To fully understand the design of VSD processes, let’s look at them at a very high level first, and then go into the depth. The dispatch mechanism postpones building of methods because of: 
- logical parallelism in the hierarchy of interface types, 
- eventual multitude of methods, 
- most methods will never be compiled by JIT (because if types are designed in Framework it doesn’t mean their instances will be required). On the other hand, using traditional VMTs for *private interfaces* would cause VMT compilation by JIT for every *private interface* from startup. That means the creation of each type would double degradation. The main class that provides dispatching is `DispatchMap`. It encapsulates a table of interface types, each of which consists of a method table, included in these interfaces. Each method can have one of the four states based on the stage in its life cycle:
- a stub in the state of “the method has never been called, so it should be compiled, and the old stub must be back patched with a new one”, 
- a stub in the state of "the method should be found dynamically each time, as it cannot be uniformly defined",
- a stub in the state of "the method is available at known address and can be called without lookup", or 
- a fully-fledged body of the method. 

Let’s revise these structures in respect to their evolution and required the data structures.

### DispatchMap

DispatchMap is a dynamically built main data structure that on which the work of interfaces in the CLR is built. Its structure is the following:

```
    DWORD: The number of types = N
    DWORD: Type 1
    DWORD: The number of slots for type 1 = M1
    DWORD: bool: there may be negative offsets
    DWORD: Slot 1
    DWORD: Target slot 1
    ...
    DWORD: Slot M1
    DWORD: Target slot M1
    ...
    DWORD: Type N
    DWORD: The number of slots for the type 1 = MN
    DWORD: bool: there may be negative offsets
    DWORD: Slot 1
    DWORD: Target slot 1
    ...
    DWORD: Slot MN
    DWORD: Target slot MN
```

Thus, initially, the VSD mechanism writes the number of interfaces implemented by certain types. Then, it writes for every interface the corresponding type, the quantity of slots implemented by that type (for navigation in a table), and then, the information about every slot, including corresponding target slot in a *private interface* that contains the method implementations of a current type.

For iterative navigation in this data structure there is the `EncodedMapIterator` class,  so this `DispatchMap` may be handled via `foreach` only. Moreover, the slot No’s. are returned as the delta between the non-virtual slot No. and the previously encoded virtual slot No. It means you can get the slot No. in the middle of a table only by looking through the whole structure from the beginning. This raises a lot of performance issues on method calls via interfaces — if we have an array of objects implementing an interface, we will have to look through the whole table of implementations to understand which method to call  or, essentially, to find the necessary one. Each iteration will result in `DispatchMapEntry` that will show where the target method is located: in this type or not, and if not, what slot No. must be returned to get the target method.

```csharp
// DispatchMapTypeID allows the relative addressing of methods. That means it indicates where the target method is located  
// in relation to a this type. Is it in this type or in some other?
//
// For dispatch map, Type ID is used to store one of the following data types:
//   - special value indicating this.class
//   - special value indicating that this interface type is not yet implemented by a class
//   - an index in InterfaceMap
class DispatchMapTypeID
{
private:
    static const UINT32 const_nFirstInterfaceIndex = 1;

    UINT32 m_typeIDVal;

    // ...
}

struct DispatchMapEntry
{
private:
    DispatchMapTypeID m_typeID;
    UINT16            m_slotNumber;
    UINT16            m_targetSlotNumber;

    enum
    {
        e_IS_VALID = 0x1
    };

    UINT16 m_flags;

    // ...
}
```

#### TypeID Map

Any method in the interface-based addressing is coded by the <TypeId; Slot No> pair. TypeId is obviously the type identifier. This field indicates where this identifier comes from and how it relates to a non-virtual type.
The `TypeIDMap` class stores a map of types as a representation of specific TypeID’s in `MethodTable`, and works visa versa.  This is solely for the performance. These HashMaps are built dynamically: once invoked, FatId or plain Id is returned in response to query for TypeID from PTR_MethodTable. You should just remember: FatId and Id are just two kinds of TypeId. To some extent, it is a "pointer” to MethodTable as it uniquely identifies.

> **TypeId is an identifier of MethodTable**. It can have two variants: Id and FatId. It is a usual number in its essence.

```csharp
class TypeIDMap
{
protected:
    HashMap             m_idMap;  // Stores map TypeID -> PTR_MethodTable
    HashMap             m_mtMap;  // Stores map PTR_MethodTable -> TypeID1
    Crst                m_lock;
    TypeIDProvider      m_idProvider;
    BOOL                m_fUseFatIdsForUniqueness;
    UINT32              m_entryCount;

    // ...
}
```

However, JIT easily handles all these challenges by inserting method calls when possible into the interface dispatch call sites. If JIT understands that nothing else can be called, it just puts in the call of a particular method. This is a very strong capability of the JIT compiler implementing this wonderful optimization for us.

### Conclusions

The thing that became natural for C# developers and rooted in our minds so deeply that we don’t even understand how to split an application into classes and interfaces is sometimes implemented so intricately that we need weeks for source texts analyses to determine all the dependencies and logic of what is going on. Something that is so trivial in use and produces no doubts during implementation can actually hide these difficulties of implementation. This means the engineers that put these ideas in action approached these problems with a lot of intelligence, by analyzing each step carefully. 

The description given here is actually quite short and superficial: it is very high-level. In comparison with any .NET book we have gone very deep into the description of VSD and VMT design, but the description is still very high-level. Indeed, the amount of code in the files describing these two structures takes about 20,000 lines. This doesn’t take into account some parts about Generics.

However, this lets us make some conclusions:

  - There is almost no difference between calling static and instance methods. This means we shouldn’t worry that the work with instance methods will somehow affect the performance. The performance of both methods is absolutely identical in equal conditions.
  - Though virtual methods are called via a VMT, there is only one additional pointer dereference for each call, as indexes are known in advance. In most cases this doesn’t affect anything: the decrease in performance (if I can say like this) will be so insignificant that it can be neglected.
  - Talking about interfaces we should remember about dispatch and understand that working via interfaces hugely complicates the implementation of type subsystem at a low level bringing ***possible*** decreases in performance when there is often no certainty which method of which class should be called with an interface identifier. However, in many cases the "intelligence” of the JIT compiler allows to call an interface method directly rather than through a dispatch.
  - For generic types, there appears another layer of abstraction which makes the method lookup more complicated for generic interfaces.

## Questions related to the topic.

> Question: if every class can implement an interface, why cannot we get a particular implementation of an interface from an object?

The answer is simple: this opportunity wasn’t covered in the CLR during the language design. However, the CLR doesn’t restrict this.  Moreover, this function will likely be added in future versions of C#, which are released quite often. Let’s have a look at the example.

```csharp
void Main()
{
    var foo = new Foo();
    var boo = new Boo();

    ((IDisposable)foo).Dispose();
    foo.Dispose();
    ((IDisposable)boo).Dispose();
    boo.Dispose();
}

class Foo : IDisposable
{
    void IDisposable.Dispose()
    {
        Console.WriteLine("Foo.IDisposable::Dispose");
    }

    public void Dispose()
    {
        Console.WriteLine("Foo::Dispose()");
    }
}

class Boo : Foo, IDisposable
{
    void IDisposable.Dispose()
    {
        Console.WriteLine("Boo.IDisposable::Dispose");
    }

    public new void Dispose()
    {
        Console.WriteLine("Boo::Dispose()");
    }
}
```

Here we call four different methods and the result will be the following:

```csharp
Foo.IDisposable::Dispose
Foo::Dispose()
Boo.IDisposable::Dispose
Boo::Dispose()
```

Despite we have the *explicit* implementation of an interface in both classes, you cannot get the implementation of the `IDisposable` interface for `Foo` in the `Boo` class. Even if we write it the following way:

```csharp
((IDisposable)(Foo)boo).Dispose();
```

We will get the same result:

```csharp
Boo.IDisposable::Dispose
```

## Why are implicit and multiple implementations of interfaces bad?

We can demonstrate the following code as the example of "interface inheritance” which is similar to the inheritance of classes.

```vb
    Class Foo
        Implements IDisposable

        Public Overridable Sub DisposeImp() Implements IDisposable.Dispose
            Console.WriteLine("Foo.IDisposable::Dispose")
        End Sub

        Public Sub Dispose()
            Console.WriteLine("Foo::Dispose()")
        End Sub

    End Class

    Class Boo
        Inherits Foo
        Implements IDisposable

        Public Sub DisposeImp() Implements IDisposable.Dispose
            Console.WriteLine("Boo.IDisposable::Dispose")
        End Sub

        Public Shadows Sub Dispose()
            Console.WriteLine("Boo::Dispose()")
        End Sub

    End Class

    ''' <summary>
    ''' Implements the interface implicitly
    ''' </summary>
    Class Doo
        Inherits Foo

        ''' <summary>
        ''' Overriding the explicit implementation
        ''' </summary>
        Public Overrides Sub DisposeImp()
            Console.WriteLine("Doo.IDisposable::Dispose")
        End Sub

        ''' <summary>
        ''’ Implicit overlapping
        ''' </summary>
        Public Sub Dispose()
            Console.WriteLine("Doo::Dispose()")
        End Sub

    End Class

    Sub Main()
        Dim foo As New Foo
        Dim boo As New Boo
        Dim doo As New Doo

        CType(foo, IDisposable).Dispose()
        foo.Dispose()
        CType(boo, IDisposable).Dispose()
        boo.Dispose()
        CType(doo, IDisposable).Dispose()
        doo.Dispose()
    End Sub
```

We can see that `Doo`, inherited from `Foo`, implicitly implement `IDisposable`, while overriding explicit implementation of `IDisposable.Dispose`, that leads to a call of overriding when calling via an interface, indicating the "inheritance of interfaces” of `Foo` and `Doo` classes.

On the one hand it is not a problem: if C# + CLR allowed such tricks it would break the consistency in type structure. Just think, you’ve created a wonderful architecture and everything is cool. But somebody calls methods in the way you didn’t plan. This would be horrible. However a similar opportunity exists in C++ and nobody really complains. Why this can be added to C#? Because the functionality that is not less horrible [is already being discussed](https://github.com/dotnet/csharplang/issues/52) and should look something like this:

```csharp
interface IA
{
    void M() { WriteLine("IA.M"); }
}

interface IB : IA
{
    override void IA.M() { WriteLine("IB.M"); } // explicitly named
}

interface IC : IA
{
    override void M() { WriteLine("IC.M"); } // implicitly named
}
```

Why is it horrible? This actually brings a whole new class of opportunities. Now we don’t need to implement each time some interface methods that were implemented similarly everywhere. Sounds great. However, not. Because an interface is actually a protocol for interaction. Protocol means a set of rules, some limits. It shouldn’t allow implementations. Here we deal with the direct infringement of this principle and introduction of another one: multiple inheritance. In fact, I object to such refinements a lot, but... I think I got carried away.

[DispatchMap::CreateEncodedMapping](https://github.com/dotnet/coreclr/blob/master/src/vm/contractimpl.cpp#L295-L460)

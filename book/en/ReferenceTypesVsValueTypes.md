# Reference Types vs Value Types

> [A link to the discussion](https://github.com/sidristij/dotnetbook/issues/57)

First, let’s talk about Reference Types and Value Types. I think people don’t really understand the differences and benefits of both. They usually say reference types store content on the heap and value types store content on the stack, which is wrong.

Let’s discuss the real differences:

- *A value type*: its value is **an entire structure**. The value of a reference type is **a reference** to an object. – A structure in memory: value types contain only the data you indicated. Reference types also contain two system fields. The first one stores 'SyncBlockIndex', the  second one stores the information about a type, including the information about a Virtual Methods Table (VMT).
- Reference types can have methods that are overridden when inherited. Value types cannot be inherited.
- You should allocate space on the heap for an instance of a reference type. A value type *can* be allocated on the stack, or it becomes the part of a reference type. This sufficiently increases the performance of some algorithms.

However, there are common features:

  - Both subclasses can inherit the object type and become its representatives.

Let’s look closer at each feature.

## Copying

The main difference between the two types is as follows:

  - Each variable, class or structure fields or method parameters that take a reference type store **a reference** to a value;
  - But each variable, class or structure fields or method parameters that take a value type store a value exactly, i.e. an entire structure.

This means that assigning or passing a parameter to a method will copy the value. Even if you change the copy, the original will stay the same. However, if you change reference type fields, this will “affect” all parts with a reference to an instance of a type. Let’s look at the
example:

```csharp
DateTime dt = DateTime.Now;   // Here, we allocate space for DateTime variable when calling a method,
                              // but it will contain zeros. Next, let’s copy all 
                              // values of the Now property to dt variable
DateTime dt2 = dt;            // Here, we copy the value once again

object obj = new object();    // Here, we create an object by allocating memory on the Small Object Heap,
                              // and put a pointer to the object in obj variable
object obj2 = obj;            // Here, we copy a reference to this object. Finally, 
                              // we have one object and two references.
```

It seems this property produces ambiguous code constructs. One of them is values change in collections:

```csharp
// Let’s declare a structure
struct ValueHolder
{
    public int Data;
}

// Let’s create an array of such structures and initialize the Data field = 5
var array = new [] { new ValueHolder { Data = 5 } };

// Let’s use an index to get the structure and put 4 in the Data field
array[0].Data = 4;

// Let’s check the value
Console.WriteLine(array[0].Data);
```

There is a small trick in this code: it looks as if we first get a copy of the structure and then set a new value to the `Data` field of this copy. If this were the case we would get the original number `5` when we try to read the value next time. However, this doesn't happen. MSIL has a separate instruction for setting the values of fields in the structures of an array, which increases performance. The code will work as intended: the program will output `4` to the console.

Let’s see what will happen if we change this code:

```csharp
// Let’s declare a structure
struct ValueHolder
{
    public int Data;
}

// Let’s create a list of such structures and initialize the Data field = 5
var list = new List<ValueHolder> { new ValueHolder { Data = 5 } };

// Let’s use an index to get the structure and put 4 in the Data field
list[0].Data = 4;

// Let’s check the value
Console.WriteLine(list[0].Data);
```

The compilation of this code will fail, because when you write `list[0].Data = 4` you get the copy of the structure first. In fact, you are calling an instance method of the `List<T>` type that underlies the access by an index. It takes the copy of a structure from an internal array (`List<T>` stores data in arrays) and returns this copy to you from the access method using an index. Next, you try to modify the copy, which is not used further along. This code is just pointless and the compiler prohibits such behavior knowing that people tend to misuse value types. We should rewrite this example in the following way:

```csharp
// Let’s declare a structure
struct ValueHolder
{
    public int Data;
}

// Let’s create a list of such structures and initialize the Data field = 5
var list = new List<ValueHolder> { new ValueHolder { Data = 5 } };

// Let’s use an index to get the structure and put 4 in the Data field. Then, let’s save it again.
var copy = list[0];
copy.Data = 4;
list[0] = copy;

// Let’s check the value
Console.WriteLine(list[0].Data);
```

This code is correct despite its apparent redundancy. The program will output `4` to the console.

The next example shows what I mean by _“the value of a structure is an entire structure”_:

```csharp
// Variant 1
struct PersonInfo
{
    public int Height;
    public int Width;
    public int HairColor;
}

int x = 5;
PersonInfo person;
int y = 6;

// Variant 2

int x = 5;
int Height;
int Width;
int HairColor;
int y = 6;
```

Both examples are identical in terms of their data location in memory since the value of a structure is an entire structure. In other words, the memory allocated for a structure is identical to the memory allocated for its fields (as if these fields were not wrapped by the structure syntax).

The next examples are also identical in terms of the elements’ location in memory as the structure takes place where it is defined among other class fields:

```csharp
// Variant 1
struct PersonInfo
{
    public int Height;
    public int Width;
    public int HairColor;
}

class Employee
{
    public int x;
    public PersonInfo person;
    public int y;
}

// Variant 2
class Employee
{
    public int x;
    public int Height;
    public int Width;
    public int HairColor;
    public int y;
}
```

I am not saying that it is absolutely identical in every sense, only in terms of placement in memory.

Of course, this is not the case of reference types. A reference type instance itself is in the unreachable Small Object Heap (SOH) or the Large Object Heap (LOH). A type class variable contains only a value (32-bit or 64-bit integer) that is a pointer to a specific instance of the class in memory.

Let’s look at the last example to close the issue.

```csharp
// Variant 1
struct PersonInfo
{
    public int Height;
    public int Width;
    public int HairColor;
}

void Method(int x, PersonInfo person, int y);

// Variant 2
void Method(int x, int HairColor, int Width, int Height, int y);
```

In terms of memory both variants of code will work in a similar way, but not in terms of architecture. It is not just a replacement of a variable number of arguments. The order of the structure variables is different because method parameters are put on the stack in the order they are declared, however, the stack grows from higher to lower addresses which means the order of pushing a structure piece by piece will be different from pushing it as a whole.

## Overridable methods and inheritance

The next big difference between the two types is the lack of virtual
methods table in structures. This means that:

  1. You cannot describe and override virtual methods in structures.
  2. A structure cannot inherit another one. The only way to emulate inheritance is to put a base type structure in the first field. The fields of an “inherited” structure will go after the fields of a “base” structure and it will create logical inheritance. The fields of both structures will coincide based on the offset.
  3. You can pass structures to unmanaged code. However, you will lose the information about methods. This is because a structure is just space in memory, filled with data without the information about a type. You can pass it to unmanaged methods, for example, written in C++, without changes.

The lack of a virtual methods table subtracts a certain part of inheritance “magic” from structures but gives them other advantages. The first one is that we can pass instances of such a structure to external environments (outside .NET Framework). Remember, this is just a memory
range! We can also take a memory range from unmanaged code and cast a type to our structure to make its fields more accessible. You cannot do this with classes as they have two inaccessible fields. These are SyncBlockIndex and a virtual methods table address. If those two fields pass to unmanaged code, it will be dangerous. Using a virtual methods table one can access any type and change it to attack an application.

Let’s show it is just a memory range without additional logic.

```csharp
unsafe void Main()
{
    int secret = 666;
    HeightHolder hh;
    hh.Height = 5;
    WidthHolder wh;
    unsafe
    {
        // This cast wouldn’t work if structures had the information about a type.
        // The CLR would check a hierarchy before casting a type and if it didn’t find WidthHolder,
        // it would output an InvalidCastException exception. But since a structure is a memory range,
        // you can interpret it as any kind of structure.
        wh = *(WidthHolder*)&hh;
   }
   Console.WriteLine("Width: " + wh.Width);
   Console.WriteLine("Secret:" + wh.Secret);
}

struct WidthHolder
{
    public int Width;
    public int Secret;
}

struct HeightHolder
{
    public int Height;
}
```

Here, we perform the operation that is impossible in strong typing. We cast one type to another incompatible one that contains one extra field. We introduce an additional variable inside the Main method. In theory, its value is secret. However, the example code will output the value of a variable, not found in any of the structures inside the `Main()` method. You might consider it a breach in security, but things are not so simple. You cannot get rid of unmanaged code in a program. The main reason is the structure of the thread stack. One can use it to access unmanaged code and play with local variables. You can defend your code from these attacks by randomizing the size of a stack frame. Or, you can delete the information about `EBP` register to complicate the return of a stack frame. However, this doesn't matter for us now. What we are interested in this example is the following. The "secret" variable goes **before** the definition of hh variable and **after** it in WidthHolder structure (in different places, actually). So why did we easily get its value? The answer is that stack grows from right to left. The variables declared first will have much higher addresses, and those declared later will have lower addresses.

## The behavior when calling instance methods

Both data types have another feature which is not plain to see and can explain the structure of both types. It deals with calling instance methods.

```csharp

// The example with a reference type
class FooClass
{
    private int x;

    public void ChangeTo(int val)
    {
        x = val;
    }
}

// The example with a value type
struct FooStruct
{
    private int x;
    public void ChangeTo(int val)
    {
        x = val;
    }
}

FooClass klass = new FooClass();
FooStruct strukt = new FooStruct();

klass.ChangeTo(10);
strukt.ChangeTo(10);
```

Logically, we can decide that the method has one compiled body. In other words, there is no instance of a type that has its own compiled set of methods, similar to the sets of other instances. However, the called method knows which instance it belongs to as a reference to the instance of a type is the first parameter. We can rewrite our example and it will be identical to what we said before. I’m not using an example with virtual methods deliberately, as they have another procedure.

```csharp

// An example with a reference type
class FooClass
{
    public int x;
}

// An example with a value type
struct FooStruct
{
    public int x;
}
public void ChangeTo(FooClass klass, int val)
{
    klass.x = val;
}

public void ChangeTo(ref FooStruct strukt, int val)
{
    strukt.x = val;
}

FooClass klass = new FooClass();
FooStruct strukt = new FooStruct();

ChangeTo(klass, 10);
ChangeTo(ref strukt, 10);
```

I should explain the use of the ref keyword. If I didn’t use it, I would get a **copy** of the structure as a method parameter instead of the original. Then I would change it, but the original would stay the same. I would have to return a changed copy from a method to a caller (another copying), and the caller would save this value back in the variable (one more copying). Instead, an instance method gets a pointer and use it for changing the original straight away. Using a pointer doesn’t influence performance as any processor-level operations use pointers. Ref is a part of the C# world, no more.

## The capability to point to the position of elements.

Both structures and classes have another capability to point to the offset of a particular field in respect to the beginning of a structure in memory. This serves several purposes:

  - to work with external APIs in the unmanaged world without having to insert unused fields before a necessary one;
  - to instruct a compiler to locate a field right at the beginning of the (`[FieldOffset(0)]`) type. It will make the work with this type faster. If it is a frequently used field, we can increase application's performance. However, this is true only for value types. In reference types the field with a zero offset contains the address of a virtual methods table, which takes 1 machine word. Even if you address the first field of a class, it will use complex addressing (address + offset). This is because the most used class field is the address of a virtual methods table. The table is necessary to call all the virtual methods;
  - to point to several fields using one address. In this case, the same value is interpreted as different data types. In C++ this data type is called a union;
  - not to bother to declare anything: a compiler will allocate fields optimally. Thus, the final order of fields may be different.

**General remarks**

  - **Auto**: the run-time environment automatically chooses a location and a packing for all class or structure fields. The defined structures that are marked by a member of this enumeration cannot pass into unmanaged code. The attempt to do it will produce an exception;
  - **Explicit**: a programmer explicitly controls the exact location of each field of a type with the FieldOffsetAttribute;
  - **Sequential**: type members come in a sequential order, defined during type design. The StructLayoutAttribute.Pack value of a packing step indicates their location.

**Using FieldOffset to skip unused structure fields**

The structures coming from the unmanaged world can contain reserved fields. One can use them in a future version of a library. In C/C++ we fill these gaps by adding fields, e.g. reserved1, reserved2, ... However, in .NET we just offset to the beginning of a field by using the FieldOffsetAttribute attribute and `[StructLayout(LayoutKind.Explicit)]`.

```csharp
[StructLayout(LayoutKind.Explicit)]
public struct SYSTEM_INFO
{
    [FieldOffset(0)] public ulong OemId;

    // 92 bytes reserved
    [FieldOffset(100)] public ulong PageSize;
    [FieldOffset(108)] public ulong ActiveProcessorMask;
    [FieldOffset(116)] public ulong NumberOfProcessors;
    [FieldOffset(124)] public ulong ProcessorType;
}
```

A gap is occupied but unused space. The structure will have the size equal to 132 and not 40 bytes as it may seem from the beginning.

**Union**

Using the FieldOffsetAttribute you can emulate the C/C++ type called a union. It allows to access the same data as entities of
different types. Let’s look at the example:

```csharp
// If we read the RGBA.Value, we will get an Int32 value accumulating all
// other fields.
// However, if we try to read the RGBA.R, RGBA.G, RGBA.B, RGBA.Alpha, we 
// will get separate components of Int32.
[StructLayout(LayoutKind.Explicit)]
public struct RGBA
{
    [FieldOffset(0)] public uint Value;
    [FieldOffset(0)] public byte R;
    [FieldOffset(1)] public byte G;
    [FieldOffset(2)] public byte B;
    [FieldOffset(3)] public byte Alpha;
}
```

You might say such behavior is possible only for value types. However, you can simulate it for reference types, using one address for overlapping two reference types or one reference type and one value type:

```csharp
class Program
{
    public static void Main()
    {
        Union x = new Union();
        x.Reference.Value = "Hello!";
        Console.WriteLine(x.Value.Value);
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

I used a generic type for overlapping on purpose. If I used usual
overlapping, this type would cause the TypeLoadException when loaded in an application domain. It might look like a security breach in theory (especially, when talking about application **plug-ins**), but if we try to run this code using a protected domain, we will get the same `TypeLoadException`.

## The difference in allocation

Another feature that differentiates both types is memory allocation for objects or structures. The CLR must decide on several things before allocating memory for an object. What is the size of an object? Is it more or less than 85K? If it is less, then is there enough free space on the SOH to allocate this object? If it is more, the CLR activates Garbage Collector. It goes through an object graph, compacts the objects by moving them to cleared space. If there is still no space on the SOH, the allocation of additional virtual memory pages will start. It is only then that an object gets allocated space, cleared from garbage. Afterwards, the CLR lays out SyncBlockIndex and VirtualMethodsTable. Finally, the reference to an object returns to a user.

If an allocated object is bigger than 85K, it goes to the Large Objects Heap (LOH). This is the case of large strings and arrays. Here, we must find the most suitable space in memory from the list of unoccupied ranges or allocate a new one. It is not quick, but we are going to deal with the objects of such size carefully. Also, we are not going to talk about them here.

There are several possible scenarios for RefTypes:

  - RefType < 85K, there is space on the SOH: quick memory allocation;
  - RefType < 85K, the space on the SOH is running out: very slow memory allocation;
  - RefType > 85K, slow memory allocation.

Such operations are rare and can’t compete with ValTypes. The algorithm of memory allocation for value types doesn’t exist. The allocation of memory for value types costs nothing. The only thing that happens when allocating memory for this type is setting fields to null. Let’s see why this happens: 1. When one declares a variable in the body of a method, the time of memory allocation for a structure is close to zero. That is because the time of allocation for local variables doesn’t depend on their number; 2. If ValTypes are allocated as fields, Reftypes will increase the size of the fields. A Value type is allocated entirely, becoming its part; 3. As in case of copying, if ValTypes are passed as method parameters, there appears a difference, depending on the size and location of a parameter.

However, that doesn’t take more time than copying one variable into another.

## The choice between a class or a structure

Let’s discuss the advantages and disadvantages of both types and decide on their use scenarios. A classic principle says we should choose a value type if it is not larger than 16 bytes, stays unchanged during its life and is not inherited. However, choosing the right type means reviewing it from different perspectives basing on scenarios of future use. I propose three groups of criteria:

  - based on type system architecture, in which your type will interact;
  - based on your approach as a system programmer to choosing a type with optimal performance;
  - when there is no other choice.

Each designed feature should reflect its purpose. This doesn’t deal with its name or interaction interface (methods, properties) only. One can use architectural considerations to choose between value and reference types. Let’s think why a structure and not a class might be chosen from the type system architecture's point of view.

  1.  If your designed type is agnostic to its state, this will mean its state reflects a process or is a value of something. In other words, an instance of a type is constant and unchangeable by nature. We can create another instance of a type based on this constant by indicating some offset. Or, we can create a new instance by indicating its properties. However, we mustn’t change it. I don’t mean that structure is an immutable type. You can change its field values. Moreover, you can pass a reference to a structure into a method using the ref parameter and you will get changed fields after exiting the method. What I talk here about is architectural sense. I will give several examples.

      - DateTime is a structure which encapsulates the concept of a moment in time. It stores this data as a uint but gives access to separate characteristics of a moment in time: year, month, day, hour, minutes, seconds, milliseconds and even processor ticks. However, it is unchangeable, basing on what it encapsulates. We cannot change a moment in time. I cannot live the next minute as if it was my best birthday in the childhood. Thus, if we choose a data type, we can choose a class with a readonly interface, which produces a new instance for each change of properties. Or, we can choose a structure, which can but shouldn’t change the fields of its instances: its *value* is the description of a moment in time, like a number. You cannot access the structure of a number and change it. If you want to get another moment in time, which differs for one day from original, you will just get a new instance of a structure.
      - `KeyValuePair<TKey, TValue>` is a structure that encapsulates the concept of a connected key–value pair. This structure is only to output the content of a dictionary during enumeration. From the architectural point of view a key and a value are inseparable concepts in `Dictionary<T>`. However, inside we have a complex structure, where a key lies separately from a value. For a user a key-value pair is an inseparable concept in terms of interface and the meaning of a data structure. It is an entire *value* itself. If one assigns another value for a key, the whole pair will change. Thus, they represent a single entity. This makes a structure an ideal variant in this case.

  2. If your designed type is an inseparable part of an external type but is integral structurally. That means it is incorrect to say the external type refers to an instance of an encapsulated type. However, it is correct to say that an encapsulated type is a part of an external together with all its properties. This is useful when designing a structure which is a part of another structure.

      - For example, if we take a structure of a file header it will be inappropriate to pass a reference from one file to another, e.g. some header.txt file. This would be appropriate when inserting a document in another, not by embedding a file but using a reference in a file system. A good example is shortcut files in Windows OS. However, if we talk about a file header (for example JPEG file header containing metadata about an image size, compression methods, photography parameters, GPS coordinates and other), then we should use structures to design types for parsing the header. If you describe all the headers in structures, you will get the same position of fields in memory as it is in a file. Using simple unsafe `*(Header *)readedBuffer` transformation without deserialization you will get fully filled data structures.

3. Neither example shows the inheritance of behavior. They show that there is no need to inherit the behavior of these entities. They are self-contained. However, if we take the effectiveness of code into consideration, we will see the choice from another side:
4. If we need to take some structured data from unmanaged code, we should choose structures. We can also pass data structure to an unsafe method. A reference type is not suitable for this at all.
5. A structure is your choice if a type passes the data in method calls (as returned values or as a method parameter) and there is no need to refer to the same value from different places. The perfect example is tuples. If a method returns several values using tuples, it will return a ValueTuple, declared as a structure. The method won’t allocate space on the heap, but will use the stack of the thread, where memory allocation costs nothing.
6. If you design a system that creates big traffic of instances that have small size and lifetime, using reference types will lead either to a pool of objects or, if without the pool of objects, to an uncontrolled garbage accumulation on the heap. Some objects will turn into older generations, increasing the load on GC. Using value types in such places (if it’s possible) will give an increase in performance because nothing will pass to the SOH. This will lessen the load on GC and the algorithm will work faster;

Basing on what I’ve said, here is some advice on using structures:

  1. When choosing collections you should avoid big arrays storing big structures. This includes data structures based on arrays. This can lead to a transition to the Large Objects Heap and its fragmentation. It is wrong to think that if our structure has 4 fields of the byte type, it will take 4 bytes. We should understand that in 32-bit systems each structure field is aligned on 4 bytes boundaries (each address field should be divided exactly by 4) and in 64-bit systems — on 8 bytes boundaries. The size of an array should depend on the size of a structure and a platform, running a program. In our example with 4 bytes – 85K / (from 4 to 8 bytes per field * the number of fields = 4) minus the size of an array header equals to about 2 600 elements per array depending on the platform (this should be rounded down). That is not very much. It may have seemed that we could easily reach a magic constant of 20 000 elements
  2. Sometimes you use a big size structure as a source of data and place it as a field in a class, while having one copy replicated to produce a thousand of instances. Then you expand each instance of a class for the size of a structure. It will lead to the swelling of generation zero and transition to generation one and even two. If the instances of a class have a short life period and you think the GC will collect them at generation zero – for 1 ms, you will be disappointed. They are already in generation one and even two. This makes the difference. If the GC collects generation zero for 1 ms, the generations one and two are collected very slowly that will lead to a decrease in efficiency;
  3. For the same reason you should avoid passing big structures through a series of method calls. If all elements call each other, these calls will take more space on the stack and bring your application to death by StackOverflowException. The next reason is performance. The more copies there are the more slowly everything works.

That’s why the choice of a data type is not an obvious process. Often, this can refer to a premature optimization, which is not recommended. However, if you know your situation falls within above stated principles, you can easily choose a value type.

## The Object base type and implementation of interfaces. Boxing

It seems we came through hell and high water and can nail any interview, even the one for .NET CLR team. However, let's not rush to  microsoft.com and search for vacancies. Now, we need to understand how value types inherit an object if they contain neither a reference to SyncBlockIndex, not a pointer to a virtual methods table. This will completely explain our system of types and all pieces of a puzzle will find their places. However, we will need more than one sentence.

Now, let's remember again how value types are allocated in memory. They get the place in memory right where they are. Reference types get allocation on the heap of small and large objects. They always give a reference to the place on the heap where the object is. Each value type has such methods as ToString, Equals and GetHashCode. They are virtual and overridable, but don’t allow to inherit a value type by overriding methods. If value types used overridable methods, they would need a virtual methods table to route calls. This would lead to the problems of passing structures to unmanaged world: extra fields would go there. As a result, there are descriptions of value type methods somewhere, but you cannot access them directly via a virtual methods table.

This may bring the idea that the lack of inheritance is artificial:

- there is inheritance from an object, but not direct; 
- there are ToString, Equals and GetHashCode inside a base type. In value types these methods have their own behavior. This means, that methods are overridden in relation to an `object`;
- moreover, if you cast a type to an `object`, you have the full right to call ToString, Equals and GetHashCode;
- when calling an instance method for a value type, the method gets another structure that is a copy of an original. That means calling an instance method is like calling a static method: `Method(ref structInstance, newInternalFieldValue)`. Indeed, this call passes `this`, with one exception, however. A JIT should compile the body of a method, so it would be unnecessary to offset structure fields, jumping over the pointer to a virtual methods table, which doesn’t exist in the structure. *It exists for value types in another place*.

Types are different in behavior, but this difference is not so big on the level of implementation in the CLR. We will talk about it a little later.

Let's write the following line in our program:

```csharp
var obj = (object)10;
```

It will allow us to deal with number 10 using a base class. This is called boxing. That means we have a VMT to call such virtual methods as ToString(), Equals and GetHashCode. In reality boxing creates a copy of a value type, but not a pointer to an original. This is because we can store the original value everywhere: on the stack or as a field of a class. If we cast it to an object type, we can store a reference to this value as long as we want. When boxing happens:

- the CLR allocates space on the heap for a structure + SyncBlockIndex + VMT of a value type (to call ToString, GetHashCode, Equals);
- it copies an instance of a value type there.

Now, we’ve got a reference variant of a value type. A structure has got **absolutely the same set of system fields as a reference type**,
becoming a fully-fledged reference type after boxing. The structure became a class. Let’s call it a .NET somersault. This is a fair name.

Just look at what happens if you use a structure which implements an interface using the same interface.

```csharp

struct Foo : IBoo
{
    int x;
    void Boo()
    {
        x = 666;
    }
}

IBoo boo = new Foo();

boo.Boo();
```

When we create the Foo instance, its value goes to the stack in fact. Then we put this variable into an interface type variable and the structure into a reference type variable. Next, there is boxing and we have the object type as an output. But it is an interface type variable. That means we need type conversion. So, the call happens in a way like this:

```csharp
IBoo boo = (IBoo)(box_to_object)new Foo();
boo.Boo();
```

Writing such code is not effective. You will have to change a copy instead of an original:

```csharp
void Main()
{
    var foo = new Foo();
    foo.a = 1;
    Console.WriteLite(foo.a);   // -> 1

    IBoo boo = foo;
    boo.Boo();                  // looks like changing foo.a to 10
    Console.WriteLite(foo.a);   // -> 1
}

struct Foo: IBoo
{
    public int a;
    public void Boo()
    {
        a = 10;
    }
}

interface IBoo
{
    void Boo();
}
```

The first time we look at the code, we don’t have to know what we deal with in the code *other than our own* and see a cast to IBoo interface. This makes us think Foo is a class and not a structure. Then there is no visual division in structures and classes, which makes us think the
interface modification results must get into foo, which doesn’t happen as boo is a copy of foo. That is misleading. In my opinion, this code should get comments, so other developers could deal with it.

The second thing relates to the previous thoughts that we can cast a type from an object to IBoo. This is another proof that a boxed value type is a reference variant of a value type. Or, all types in a system of types are reference types. We can just work with structures as with value types, passing their value entirely. Dereferencing a pointer to an object as you would say in the world of C++.

You can object that if it was true, it would look like this:

```csharp
var referenceToInteger = (IInt32)10;
```

We would get not just an object, but a typed reference for a boxed value type. It would destroy the whole idea of value types (i.e. integrity of their value) allowing for great optimization, based on their properties. Let’s take down this idea!

```csharp
public sealed class Boxed<T>
{
    public T Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
     public override bool Equals(object obj)
     {
         return Value.Equals(obj);
     }

     [MethodImpl(MethodImplOptions.AggressiveInlining)]
     public override string ToString()

     {
         return Value.ToString();
     }

     [MethodImpl(MethodImplOptions.AggressiveInlining)]
     public override int GetHashCode()
     {
         return Value.GetHashCode();
     }
}
```

We’ve got a complete analog of boxing. However, we can change its contents by calling instance methods. These changes will affect all parts with a reference to this data structure.

```csharp
var typedBoxing = new Boxed<int> { Value = 10 };
var pureBoxing = (object)10;
```

The first variant isn’t very attractive. Instead of casting a type we create nonsense. The second line is much better, but the two lines are almost identical. The only difference is that there is no memory cleaning with zeros during the usual boxing after allocating memory on the heap. The necessary structure takes the memory straight away whereas the first variant needs cleaning. This makes it work longer than the usual boxing by 10%.

Instead, we can call some methods for our boxed value.

```csharp
struct Foo
{
    public int x;

    public void ChangeTo(int newx)
    {
        x = newx;
    }
}
var boxed = new Boxed<Foo> { Value = new Foo { x = 5 } };
boxed.Value.ChangeTo(10);
var unboxed = boxed.Value;
```

We’ve got a new instrument. Let's think what we can do with it.

- Our `Boxed<T>` type does the same as the usual type: allocates memory on the heap, passes a value there and allows to get it, by doing a kind of unbox;
- If you lose a reference to a boxed structure, the GC will collect it;
- However, we can now work with a boxed type, i.e. calling its methods;
- Also, we can replace an instance of a value type in the SOH/LOH for another one. We couldn’t do it before, as we would have to do unboxing, change structure to another one and do boxing back, giving a new reference to customers.

The main problem of boxing is creating traffic in memory. The traffic of unknown number of objects, the part of which can survive up to generation one, where we get problems with garbage collection. There will be a lot of garbage and we could have avoided it. But when we have the traffic of short-lived objects, the first solution is pooling. This is an ideal end of .NET somersault.

```csharp
var pool = new Pool<Boxed<Foo>>(maxCount:1000);
var boxed = pool.Box(10);
boxed.Value=70;

// use boxed value here

pool.Free(boxed);
```

Now boxing can work using a pool, which eliminates memory traffic while boxing. We can even make objects go back to life in finalization method and put themselves back into the pool. This might be useful when a boxed structure goes to asynchronous code other than yours and you cannot understand when it became unnecessary. In this case, it will return itself back to pool during GC.

Let’s conclude:

- If boxing is accidental and shouldn’t happen, don’t make it happen. It can lead to problems with performance.
- If boxing is necessary for the architecture of a system, there may be variants. If the traffic of boxed structures is small and almost invisible, you can use boxing. If the traffic is visible, you might want to do the pooling of boxing, using one of the solutions stated above. It spends some resources, but makes GC work without overload;

Ultimately let’s look at a totally impractical code:

```csharp
static unsafe void Main()
{
    // here we create boxed int
    object boxed = 10;

    // here we get the address of a pointer to a VMT
    var address = (void**)EntityPtr.ToPointerWithOffset(boxed);

    unsafe
    {
        // here we get a Virtual Methods Table address
        var structVmt = typeof(SimpleIntHolder).TypeHandle.Value.ToPointer();

       // change the VMT address of the integer passed to Heap into a VMT SimpleIntHolder, turning Int into a structure
       *address = structVmt;
    }

    var structure = (IGetterByInterface)boxed;

    Console.WriteLine(structure.GetByInterface());
}

interface IGetterByInterface
{
    int GetByInterface();
}

struct SimpleIntHolder : IGetterByInterface
{
    public int value;

    int IGetterByInterface.GetByInterface()
    {
        return value;
    }
}
```

The code uses a small function, which can get a pointer from a reference to an object. The library is available at [github address](https://github.com/mumusan/dotnetex/blob/master/libs/). This example shows that usual boxing turns int into a typed reference type. Let’s
look at the steps in the process:

1. Do boxing for an integer.
2. Get the address of an obtained object (the address of Int32 VMT)
3. Get the VMT of a SimpleIntHolder
4. Replace the VMT of a boxed integer to the VMT of a structure.
5. Make unboxing into a structure type
6. Display the field value on screen, getting the Int32, that was
    boxed.

I do it via the interface on purpose as I want to show that it will work
that way.

### Nullable\<T\>

It is worth mentioning about the behavior of boxing with Nullable value types. This feature of Nullable value types is very attractive as the boxing of a value type which is a sort of null returns null.

```csharp
int? x = 5;
int? y = null;

var boxedX = (object)x; // -> 5
var boxedY = (object)y; // -> null
```

This leads us to a peculiar conclusion: as null doesn’t have a type, the
only way to get a type, different from the boxed one is the following:

```csharp
int? x = null;
var pseudoBoxed = (object)x;
double? y = (double?)pseudoBoxed;
```

The code works just because you can cast a type to anything you like
with null.

## Going deeper in boxing

As a final bit, I would like to tell you about [System.Enum type](http://referencesource.microsoft.com/#mscorlib/system/enum.cs,36729210e317a805). Logically this should be a value type as it’s a usual enumeration: aliasing numbers to names in a programming language. However, System.Enum is a reference type. All the enum data types, defined in your field as well as in .NET Framework are inherited from System.Enum. It’s a class data type. Moreover, it’s an abstract class, inherited from `System.ValueType`.

```csharp
    [Serializable]
    [System.Runtime.InteropServices.ComVisible(true)]
    public abstract class Enum : ValueType, IComparable, IFormattable, IConvertible
    {
        // ...
    }
```

Does it mean that all enumerations are allocated on the SOH and when we use them, we overload the heap and GC? Actually no, as we just use them. Then, we suppose that there is a pool of enumerations somewhere and we just get their instances. No, again. You can use enumerations in structures while marshaling. Enumerations are usual numbers.

The truth is that CLR hacks data type structure when forming it if there is enum [turning a class into a value type](https://github.com/dotnet/coreclr/blob/4b49e4330441db903e6a5b6efab3e1dbb5b64ff3/src/vm/methodtablebuilder.cpp#L1425-L1445):

```csharp
// Check to see if the class is a valuetype; but we don't want to mark System.Enum
// as a ValueType. To accomplish this, the check takes advantage of the fact
// that System.ValueType and System.Enum are loaded one immediately after the
// other in that order, and so if the parent MethodTable is System.ValueType and
// the System.Enum MethodTable is unset, then we must be building System.Enum and
// so we don't mark it as a ValueType.
if(HasParent() &&
    ((g_pEnumClass != NULL && GetParentMethodTable() == g_pValueTypeClass) ||
    GetParentMethodTable() == g_pEnumClass))
{
    bmtProp->fIsValueClass = true;
    HRESULT hr = GetMDImport()->GetCustomAttributeByName(bmtInternal->pType->GetTypeDefToken(),
                                                            g_CompilerServicesUnsafeValueTypeAttribute,
                                                            NULL, NULL);

    IfFailThrow(hr);
    if (hr == S_OK)
    {
        SetUnsafeValueClass();
    }
}
```

Why doing this? In particular, because the idea of inheritance — to do a customized enum, you, for example, need to specify the names of possible values. However, it is impossible to inherit value types. So, developers designed it to be a reference type that can turn it into a value type when compiled.

## What if you want to see boxing personally?

Fortunately, you don’t have to use a disassembler and get into the code jungle. We have the texts of the whole .NET platform core and many of them are identical in terms of .NET Framework CLR and CoreCLR. You can click the links below and see the implementation of boxing right away:

-   There is a separate group of optimizations each of which uses a
    specific type of a processor:
    -   *[JIT\_BoxFastMP\_InlineGetThread](https://github.com/dotnet/coreclr/blob/master/src/vm/amd64/JitHelpers_InlineGetThread.asm#L86-L148)*
        (AMD64 - multiprocessor or Server GC, implicit Thread Local Storage)
    -   *[JIT\_BoxFastMP](https://github.com/dotnet/coreclr/blob/8cc7e35dd0a625a3b883703387291739a148e8c8/src/vm/amd64/JitHelpers_Slow.asm#L201-L271)*
        (AMD64 - multiprocessor or Server GC)
    -   *[JIT\_BoxFastUP](https://github.com/dotnet/coreclr/blob/8cc7e35dd0a625a3b883703387291739a148e8c8/src/vm/amd64/JitHelpers_Slow.asm#L485-L554)*
        (AMD64 - single processor or Workstation GC)
    -   *[JIT\_TrialAlloc::GenBox(..)](https://github.com/dotnet/coreclr/blob/38a2a69c786e4273eb1339d7a75f939c410afd69/src/vm/i386/jitinterfacex86.cpp#L756-L886)*
        (x86) connected through JitHelpers
-   In general cases a JIT inlines a call of a helper function
    [Compiler::impImportAndPushBox(..)](https://github.com/dotnet/coreclr/blob/a14608efbad1bcb4e9d36a418e1e5ac267c083fb/src/jit/importer.cpp#L5212-L5221)
-   Generic-version uses less optimized
    [MethodTable::Box(..)](https://github.com/dotnet/coreclr/blob/master/src/vm/methodtable.cpp#L3734-L3783)
    -   Finally, [CopyValueClassUnchecked(..)] is called
        (https://github.com/dotnet/coreclr/blob/master/src/vm/object.cpp#L1514-L1581).
        Its code shows why it’s better to choose structures with the size up to 8 bytes included.

Here, the only method is used for unboxing:
*[JIT\_Unbox(..)](https://github.com/dotnet/coreclr/blob/03bec77fb4efaa397248a2b9a35c547522221447/src/vm/jithelpers.cpp#L3603-L3626)*, which is a wrapper around *[JIT\_Unbox\_Helper(..)](https://github.com/dotnet/coreclr/blob/03bec77fb4efaa397248a2b9a35c547522221447/src/vm/jithelpers.cpp#L3574-L3600)*.

Also, it is interesting that (https://stackoverflow.com/questions/3743762/unboxing-does-not-create-a-copy-of-the-value-is-this-right), unboxing doesn’t mean copying data to the heap. Boxing means passing a pointer to the heap while testing the compatibility of types. The IL opcode following unboxing will define the actions with this address. The data might be copied to a local variable or the stack for calling a method. Otherwise, we would have a double copying; first when copying from the heap to somewhere, and then copying to the destination place.

### The ref keyword

> TODO

### **makeref, **reftype, **refvalue, **arglist

> TODO

### Implicit boxing

> TODO

## Questions

### Why .NET CLR can’t do pooling for boxing itself?

If we talk to any Java developer, we will know two things:

  - All value types in Java are boxed, meaning they are not essentially value types. Integers are also boxed.
  - For the reason of optimization all integers from -128 to 127 are taken from the pool of objects.

So, why this doesn’t happen in .NET CLR during boxing? It is simple. Because we can change the content of a boxed value type, that is we can do the following:

```csharp
object x = 1;
x.GetType().GetField("m_value", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(x, 138);
Console.WriteLine(x); // -> 138
```

Or like this (С++/CLI):

```cpp
void ChangeValue(Object^ obj)
{
    Int32^ i = (Int32^)obj;
    *i = 138;
}
```

If we dealt with pooling, then we would change all ones in application to 138, which is not good.

The next is the essence of value types in .NET. They deal with value, meaning they work faster. Boxing is rare and addition of boxed numbers belongs to the world of fantasy and bad architecture. This is not useful at all.

### Why it is not possible to do boxing on stack instead of the heap, when you call a method that takes an object type, which is a value type in fact?

If the value type boxing is done on the stack and the reference will go to the heap, the reference inside the method can go somewhere else, for example a method can put the reference in the field of a class. The method will then stop, and the method that made boxing will also stop. As a result, the reference will point to a dead space on the stack.

### Why it is not possible to use Value Type as a field?

Sometimes we want to use a structure as a field of another structure which uses the first one. Or simpler: use structure as a structure field. Don't ask me why this can be useful. It cannot. If you use a structure as its field or through dependence with another structure, you create recursion, which means infinite size structure. However, .NET Framework has some places where you can do it. An example is `System.Char`, [which contains itself](http://referencesource.microsoft.com/#mscorlib/system/char.cs,02f2b1a33b09362d):

```csharp
public struct Char : IComparable, IConvertible
{

    // Member Variables

    internal char m_value;

    //...
}
```

All CLR primitive types are designed this way. We, mere mortals, cannot implement this behavior. Moreover, we don't need this: it is done to give primitive types a spirit of OOP in CLR.

## References

- [The library to get a pointer to an object](https://github.com/mumusan/dotnetex/blob/master/libs/)

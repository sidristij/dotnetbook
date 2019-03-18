# Reference Types vs Value Types

> [讨论链接](https://github.com/sidristij/dotnetbook/issues/57)

首先，我们来谈谈参考类型和价值类型。我认为人们并不真正理解两者的差异和好处。他们通常说引用类型在堆上存储内容，值类型在堆栈上存储内容，这是错误的。

让我们讨论真正的差异：

  -  *值类型*：其值为**整个结构**。引用类型的值是**对象的引用**。
  -  内存中的结构：值类型仅包含您指示的数据。引用类型还包含两个系统字段。第一个存储`SyncBlockIndex`，第二个存储有关类型的信息，包括有关虚拟方法表(VMT)的信息。
  - 引用类型可以包含在继承时被覆盖的方法。值类型不能继承。
  - 您应该在堆上为引用类型的实例分配空间。值类型*可以*在堆栈上分配，或者它成为引用类型的一部分。这足以提高某些算法的性能。

但是，有一些共同的特点：

  - 两个子类都可以继承对象类型并成为其代表。

让我们仔细看看每个功能。

## 正在复制

这两种类型的主要区别如下：

  - 采用引用类型的每个变量，类或结构字段或方法参数将引用**存储为值;
  - 但是采用值类型的每个变量，类或结构字段或方法参数都精确地存储了一个值，即整个结构。

这意味着将参数分配或传递给方法将复制该值。即使您更改副本，原件也将保持不变。但是，如果更改引用类型字段，则会通过引用类型实例来“影响”所有部分。我们来看看吧例：

```csharp
DateTime dt = DateTime.Now;   // 这里，我们在调用方法时为DateTime变量分配空间，
                              // 但它将包含零。接下来，让我们复制所有
                              // Now属性的值为dt变量
DateTime dt2 = dt;            // 在这里，我们再次复制该值

object obj = new object();    // 在这里，我们通过在Small Object Heap上分配内存来创建一个对象
                              // 并在obj变量中放置一个指向对象的指针
object obj2 = obj;            // 在这里，我们复制对该对象的引用。最后
                              // 我们有一个对象和两个引用。
```

看起来这个属性产生了模糊的代码结构，例如集合中代码的变化：

```csharp
// 让我们宣布一个结构
struct ValueHolder
{
    public int Data;
}

// 让我们创建一个这样的结构数组并初始化Data field = 5
var array = new [] { new ValueHolder { Data = 5 } };

// 让我们使用索引来获取结构并将4放在数据字段中
array[0].Data = 4;

// 让我们检查一下这个值
Console.WriteLine(array[0].Data);
```

这段代码中有一个小技巧。看起来我们首先得到结构实例，然后为副本的Data字段分配一个新值。这意味着我们在检查值时应该再次得到`5`。但是，这不会发生。MSIL有一个单独的指令，用于设置数组结构中字段的值，从而提高性能。代码将按预期工作：程序将
输出`4`到控制台。

让我们看看如果我们更改此代码会发生什么：

```csharp
// 让我们宣布一个结构
struct ValueHolder
{
    public int Data;
}

// 让我们创建一个这样的结构列表并初始化Data field = 5
var list = new List<ValueHolder> { new ValueHolder { Data = 5 } };

// 让我们使用索引来获取结构并将4放在数据字段中
list[0].Data = 4;

// 让我们检查一下这个值
Console.WriteLine(list[0].Data);
```

这段代码的编译将失败，因为当你编写`list [0] .Data = 4`时，你首先得到结构的副本。实际上，您正在调用`List <T>`类型的实例方法，该方法是索引访问的基础。它从内部数组中获取结构的副本(`List <T>`在数组中存储数据)并使用索引从访问方法返回此副本。接下来，您尝试修改副本，该副本未进一步使用。这段代码毫无意义。知道人们滥用价值类型，编译器会禁止此类行为。我们应该通过以下方式重写这个例子：

```csharp
// 让我们宣布一个结构
struct ValueHolder
{
    public int Data;
}

// 让我们创建一个这样的结构列表并初始化Data field = 5
var list = new List<ValueHolder> { new ValueHolder { Data = 5 } };

// 让我们使用索引来获取结构并将4放在数据字段中。然后，让我们再次保存它。
var copy = list[0];
copy.Data = 4;
list[0] = copy;

// 让我们检查一下这个值
Console.WriteLine(list[0].Data);
```

尽管有明显的冗余，但该代码是正确的。程序将“4”输出到控制台。

下一个例子显示我的意思是“结构的价值是一个整个结构“

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

两个示例在存储器中的数据位置方面类似，因为结构的值是整个结构。它为自己分配内存。

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

这些示例在元素在内存中的位置方面也类似，因为结构在类字段中占据了一个定义的位置。我不是说它们完全相似，因为你可以使用结构方法操作结构域。

当然，这不是参考类型的情况。实例本身位于无法访问的小对象堆(SOH)或大对象堆(LOH)上。类字段仅包含指向实例的指针的值：32位或64位数字。

让我们看一下最后一个关闭问题的例子。

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

在内存方面，两种代码变体都以类似的方式工作，但不是在体系结构方面。它不仅仅是可变数量参数的替代品。顺序更改，因为方法参数是一个接一个地声明。它们以类似的方式放在堆栈上。

但是，堆栈从较高地址增长到较低地址。这意味着逐个推动结构的顺序将不同于整体推动结构。

##可重写的方法和继承

两种类型之间的下一个巨大差异是缺乏虚拟
结构中的方法表。这意味着：

  1. 您无法在结构中描述和覆盖虚拟方法。
  2. 结构不能继承另一个结构。模拟继承的唯一方法是在第一个字段中放置基类型结构。“继承”结构的字段将位于“基础”结构的字段之后，它将创建逻辑继承。两个结构的字段将基于偏移重合。
  3.您可以将结构传递给非托管代码。但是，您将丢失有关方法的信息。这是因为结构只是内存中的空间，填充了没有类型信息的数据。您可以将其传递给非托管方法，例如，使用C ++编写，无需更改。

缺少虚拟方法表会从结构中减去继承“魔法”的某一部分，但会给它们带来其他优势。第一个是我们可以将这种结构的实例传递给外部环境(.NET Framework之外)。记住，这只是一个记忆
范围！我们还可以从非托管代码中获取内存范围，并将类型转换为我们的结构，以使其字段更易于访问。您不能对类执行此操作，因为它们有两个不可访问的字段。这些是SyncBlockIndex和虚方法表地址。如果这两个字段传递给非托管代码，那将是危险的。使用虚拟方法表，可以访问任何类型并更改它以攻击应用程序。

让我们看看它只是一个没有额外逻辑的内存范围。

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

在这里，我们执行强类型中不可能执行的操作。我们将一种类型转换为另一种包含一个额外字段的不兼容类型。我们在Main方法中引入了一个额外的变量。从理论上讲，它的价值是秘密的。但是，示例代码将输出变量的值，而不是在`Main()`方法中的任何结构中找到的。您可能会认为这是安全漏洞，但事情并非如此简单。你无法摆脱程序中的非托管代码。主要原因是线程堆栈的结构。可以使用它来访问非托管代码并使用局部变量。您可以通过随机化堆栈帧的大小来保护代码免受这些攻击。或者，您可以删除有关`EBP`寄存器的信息，以使堆栈帧的返回复杂化。但是，这对我们来说无关紧要。我们对这个例子感兴趣的是以下内容。“秘密”变量在**之前变为** hh变量的定义，**在** WidthHolder结构之后**(实际上在不同的地方)。那么为什么我们很容易获得它的价值呢？答案是堆栈从右向左增长。首先声明的变量将具有更高的地址，而稍后声明的变量将具有更低的地址。

##调用实例方法时的行为

这两种数据类型都具有另一个不易看到的特征，可以解释这两种类型的结构。它处理调用实例方法。

```csharp

// 带引用类型的示例
class FooClass
{
    private int x;

    public void ChangeTo(int val)
    {
        x = val;
    }
}

// 具有值类型的示例
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

从逻辑上讲，我们可以确定该方法有一个编译体。换句话说，没有类型的实例具有自己的编译方法集，类似于其他实例的集合。但是，被调用的方法知道它属于哪个实例作为对类型实例的引用是第一个参数。我们可以改写我们的例子，它将与我们之前所说的相同。我没有故意使用虚拟方法的示例，因为它们有另一个过程。

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

我应该解释一下ref关键字的用法。如果我没有使用它，我会获得结构的**副本**作为方法参数而不是原始参数。然后我会改变它，但原来会保持不变。我必须将一个已更改的副本从一个方法返回给一个调用者(另一个复制)，然后调用者将该值保存回变量中(再复制一次)。相反，实例方法获取指针并使用它直接更改原始指针。使用指针不会影响性能，因为任何处理器级操作都使用指针。Ref是C＃世界的一部分，不再是。

## 指向元素位置的能力。

结构和类都有另一种能力指向特定字段相对于内存中结构开头的偏移量。这有几个目的：

  - 在非托管环境中使用外部API，而无需在必要的字段之前插入未使用的字段;
  - 指示编译器在(`[FieldOffset(0)]`)类型的开头找到一个字段。它将使这种类型的工作更快。如果它是一个经常使用的字段，我们可以提高应用程序的性能。但是，这仅适用于值类型。在引用类型中，具有零偏移的字段包含虚拟方法表的地址，该表占用1个机器字。即使您处理类的第一个字段，它也将使用复杂的寻址(地址+偏移)。这是因为最常用的类字段是虚方法表的地址。该表是调用所有虚拟方法所必需的;
  - 使用一个地址指向多个字段。在这种情况下，相同的值被解释为不同的数据类型。在C ++中，这种数据类型称为联合;
  - 不要费心声明任何事情：编译器将以最佳方式分配字段。因此，字段的最终顺序可能不同。

**一般评论**

  - **Auto**：运行时环境自动为所有类或结构字段选择位置和包装。由此枚举的成员标记的已定义结构无法传递到非托管代码。尝试这样做会产生例外;
  - **显式**：程序员使用FieldOffsetAttribute显式控制类型的每个字段的确切位置;
  - **顺序**：类型成员按顺序排列，在类型设计期间定义。打包步骤的StructLayoutAttribute.Pack值表示其位置。

**使用FieldOffset跳过未使用的结构字段**

来自非托管世界的结构可以包含保留字段。可以在未来版本的库中使用它们。在C/C++中，我们通过添加字段来填充这些空白，例如reserved1，reserved2，...但是，在.NET中，我们只是通过使用FieldOffsetAttribute属性和`[StructLayout(LayoutKind.Explicit)]`来偏移到字段的开头。

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

间隙占用但未使用的空间。该结构的大小将等于132，而不是从一开始就看起来像40字节。

**联盟**

使用FieldOffsetAttribute可以模拟称为union的C/C++类型。它允许访问与实体相同的数据
不同种类。我们来看看这个例子：

```csharp
// 如果我们读取RGBA.Value，我们将得到一个积累所有的Int32值
// 其他领域
// 但是，如果我们尝试读取RGBA.R，RGBA.G，RGBA.B，RGBA.Alpha，我们 
// 将获得Int32的单独组件
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

您可能会说这种行为仅适​​用于值类型。但是，您可以将其模拟为引用类型，使用一个地址重叠两个引用类型或一个引用类型和一个值类型：

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

我故意使用泛型类型进行重叠。如果我平常使用
重叠，此类型将在应用程序域中加载时导致TypeLoadException。理论上它可能看起来像安全漏洞(特别是在讨论应用程序**插件时)，但如果我们尝试使用受保护的域运行此代码，我们将得到相同的`TypeLoadException`。

## 分配的差异

区分这两种类型的另一个特征是对象或结构的内存分配。在为对象分配内存之前，CLR必须决定几件事。物体的大小是多少？是或多于或少于85K？如果它更少，那么SOH上是否有足够的可用空间来分配这个对象？如果更多，CLR将激活垃圾收集器。它通过一个对象图，通过将对象移动到已清除的空间来压缩对象。如果SOH上仍然没有空间，则将开始分配额外的虚拟内存页面。只有这样，一个对象才能获得分配的空间，从垃圾中清除。之后，CLR列出了SyncBlockIndex和VirtualMethodsTable。最后，对对象的引用返回给用户。

如果分配的对象大于85K，则转到大对象堆(LOH)。这是大字符串和数组的情况。在这里，我们必须从未占用范围列表中找到最合适的内存空间或分配一个新空间。它不是很快，但我们会仔细处理这么大的物体。此外，我们不打算在这里讨论它们。

RefTypes有几种可能的场景：

  - RefType <85K，SOH上有空间：快速内存分配;
  - RefType <85K，SOH上的空间不足：内存分配很慢;
  - RefType> 85K，内存分配缓慢。

此类操作很少见，无法与ValTypes竞争。值类型的内存分配算法不存在。为值类型分配内存不需要任何费用。为此类型分配内存时唯一发生的事情是将字段设置为null。让我们看看为什么会发生这种情况：1。当一个人在方法体中声明一个变量时，结构的内存分配时间接近于零。那是因为局部变量的分配时间并不取决于它们的数量; 2.如果ValTypes被分配为字段，Reftypes将增加字段的大小。价值类型完全分配，成为其中的一部分; 3.如同复制一样，如果ValTypes作为方法参数传递，则会出现差异，具体取决于参数的大小和位置。

但是，这并不比将一个变量复制到另一个变量花费更多时间。

## 在类或结构之间进行选择

让我们讨论两种类型的优缺点，并决定它们的使用场景。一个经典原则说，如果值类型不大于16个字节，我们应该选择一个值类型，在其生命周期内保持不变并且不会继承。但是，选择正确的类型意味着根据未来使用情况从不同角度对其进行审核。我提出三组标准：

  - 基于类型系统架构，您的类型将在其中进行交互;
  - 基于您作为系统程序员的方法来选择具有最佳性能的类型;
  - 当没有其他选择时。

每个设计的功能都应该反映其目的。这不仅仅涉及其名称或交互界面(方法，属性)。可以使用体系结构考虑因素在值和引用类型之间进行选择。让我们想一想为什么可以从类型系统架构的角度选择结构而不是类。

  1. 如果您的设计类型与其状态无关，这将意味着其状态反映了某个过程或某事物的价值。换句话说，类型的实例本质上是不变的且不可改变的。我们可以通过指示一些偏移量来创建基于此常量的另一个类型实例。或者，我们可以通过指示其属性来创建新实例。但是，我们不能改变它。我并不是说结构是不可变的类型。您可以更改其字段值。此外，您可以使用ref参数将结构引用传递给方法，退出方法后将获得更改的字段。我在这里谈到的是建筑意义。我将举几个例子。

    -  DateTime是一个封装了时刻概念的结构。它将这些数据存储为uint，但可以访问时刻的独立特征：年，月，日，小时，分钟，秒，毫秒甚至处理器滴答。但是，它是不可更改的，基于它封装的内容。我们无法改变时刻。我不能活在下一分钟，好像这是我童年时代最好的生日。因此，如果我们选择数据类型，我们可以选择一个具有只读接口的类，它为每个属性更改生成一个新实例。或者，我们可以选择一个结构，它可以但不应该更改其实例的字段：它的*值*是一个时刻的描述，就像一个数字。您无法访问数字的结构并进行更改。如果你想得到另一个时刻，
    - `KeyValuePair <TKey，TValue>`是一种封装连接键值对概念的结构。此结构仅用于在枚举期间输出字典的内容。从架构的角度来看，键和值是`Dictionary <T>`中不可分割的概念。但是，我们内部有一个复杂的结构，其中一个键与一个值分开。对于用户而言，键值对在接口和数据结构的含义方面是不可分割的概念。这是一个完整的*值*本身。如果为键指定另一个值，则整个对将发生变化。因此，它们代表一个单一的实体。在这种情况下，这使得结构成为理想的变体。

  2. 如果您的设计类型是外部类型的不可分割的部分，但在结构上是完整的。这意味着说外部类型是指封装类型的实例是不正确的。但是，正确的说封装类型是外部的一部分及其所有属性。当设计作为另一结构的一部分的结构时，这是有用的。

      - 例如，如果我们采用文件头的结构，将引用从一个文件传递到另一个文件是不合适的，例如一些header.txt文件。这在将文档插入另一个文档时是合适的，而不是通过嵌入文件而是在文件系统中使用引用。一个很好的例子是Windows OS中的快捷方式文件。但是，如果我们谈论文件头(例如包含有关图像大小，压缩方法，摄影参数，GPS坐标等的元数据的JPEG文件头)，那么我们应该使用结构来设计用于解析头的类型。如果您描述结构中的所有标题，您将在内存中获得与文件中​​相同的字段位置。使用简单的不安全的`*(Header *)readedBuffer`转换而不进行反序列化，您将获得完全填充的数据结构。

  3. 这两个例子都没有显示行为的继承。它们表明不需要继承这些实体的行为。它们是独立的。但是，如果我们考虑代码的有效性，我们将从另一方面看到选择：
  4. 如果我们需要从非托管代码中获取一些结构化数据，我们应该选择结构。我们还可以将数据结构传递给不安全的方法。参考类型根本不适用于此。
  5. 如果类型在方法调用中传递数据(作为返回值或作为方法参数)，并且不需要从不同位置引用相同的值，则可以选择结构。完美的例子是元组。如果方法使用元组返回多个值，它将返回一个声明为结构的ValueTuple。该方法不会在堆上分配空间，但会使用线程的堆栈，其中内存分配不需要任何费用。
  6. 如果您设计的系统可以创建具有较小大小和生命周期的实例的大流量，则使用引用类型将导致对象池，或者，如果没有对象池，则会导致堆上的不受控制的垃圾堆积。有些对象会变成老一代，增加了GC的负担。在这些地方使用价值类型(如果可能的话)将提高性能，因为没有任何东西会传递给SOH。这将减轻GC的负担，算法将更快地工作;

基于我所说的，这里有一些关于使用结构的建议：

  1. 选择集合时，应避免使用存储大型结构的大型数组。这包括基于数组的数据结构。这可能导致转换为大对象堆及其碎片。如果我们的结构有4个字节类型的字段，则需要4个字节，这是错误的。我们应该理解，在32位系统中，每个结构字段在4个字节边界上对齐(每个地址字段应该精确地除以4)，在64位系统中，在8个字节边界上对齐。数组的大小应取决于运行程序的结构和平台的大小。在我们的示例中，4个字节 -  85K /(每个字段4到8个字节*字段数= 4)减去数组头的大小等于每个阵列大约2 600个元素，具体取决于平台(这应该向下舍入) )。那不是很多。
  2. 有时您使用大尺寸结构作为数据源并将其作为字段放在类中，同时复制一个副本以生成数千个实例。然后，根据结构的大小展开类的每个实例。它将导致第0代的膨胀并过渡到第一代甚至两代。如果一个类的实例具有较短的生命周期，并且您认为GC将在第0代收集它们 - 持续1毫秒，您将会感到失望。他们已经在第一代甚至第二代。这有所不同。如果GC收集零点1毫秒，则第一代和第二代收集非常缓慢，这将导致效率降低;
  3. 出于同样的原因，你应该避免通过一系列方法调用传递大型结构。如果所有元素互相调用，这些调用将占用堆栈上的更多空间，并通过StackOverflowException使应用程序死机。下一个原因是表现。副本越多，一切都越慢。

这就是为什么选择数据类型不是一个明显的过程。通常，这可以指过早优化，这是不推荐的。但是，如果您知道您的情况符合上述原则，则可以轻松选择值类型。

## Object基类型和接口的实现。Boxing

看起来我们来自地狱和高水，并且可以指出任何采访，甚至是.NET CLR团队的采访。但是，我们不要急于访问microsoft.com并寻找空缺。现在，我们需要了解值类型如果它们既不包含对SyncBlockIndex的引用，又不包含指向虚方法表的指针，那么它是如何继承对象的。这将完全解释我们的类型系统和拼图的所有部分将找到他们的位置。但是，我们需要不止一个句子。

现在，让我们再次记住如何在内存中分配值类型。他们在记忆中占有一席之地。引用类型在小型和大型对象的堆上进行分配。它们总是引用对象所在堆上的位置。每种值类型都有ToString，Equals和GetHashCode等方法。它们是虚拟和可覆盖的，但不允许通过重写方法继承值类型。如果值类型使用了可覆盖的方法，则它们需要一个虚拟方法表来路由调用。这将导致将结构传递到非托管世界的问题：额外的字段会去那里。因此，某些地方存在值类型方法的描述，但您无法通过虚拟方法表直接访问它们。

这可能会导致缺乏继承的想法是人为的：

- 有一个对象的继承，但不是直接的; 
- 基类型中有ToString，Equals和GetHashCode。在值类型中，这些方法有自己的行为。这意味着，相对于`object`重写了这些方法;
- 此外，如果将类型转换为`object`，则可以完全调用ToString，Equals和GetHashCode;
- 在为值类型调用实例方法时，该方法获取另一个原始副本的结构。这意味着调用实例方法就像调用静态方法：`Method(ref structInstance，newInternalFieldValue)`。实际上，这个调用通过了`this`，但有一个例外。JIT应该编译方法的主体，因此没有必要偏移结构字段，跳过指向结构中不存在的虚方法表的指针。*它存在于另一个地方的价值类型*。

类型在行为上是不同的，但这种差异在CLR的实现级别上并没有那么大。我们稍后会讨论它。

让我们在程序中写下以下行：

```csharp
var obj = (object)10;
```

它允许我们使用基类处理数字10。这叫拳击。这意味着我们有一个VMT来调用ToString()，Equals和GetHashCode等虚拟方法。实际上，boxing会创建值类型的副本，但不会创建指向原始的指针。这是因为我们可以在任何地方存储原始值：在堆栈上或作为类的字段。如果我们将它转​​换为对象类型，我们可以根据需要存储对该值的引用。拳击发生时：

-  CLR在堆上为结构+ SyncBlockIndex + VMT分配值类型的空间(调用ToString，GetHashCode，Equals);
- 它在那里复制值类型的实例。

现在，我们有一个值类型的引用变体。一个结构与参考类型**具有绝对相同的系统字段集，
拳击后成为一个完全成熟的参考类型。结构变成了一个阶级。我们称之为.NET翻筋斗。这是一个公平的名字。

看看如果使用一个使用相同接口实现接口的结构会发生什么。

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

当我们创建Foo实例时，它的值实际上会进入堆栈。然后我们将这个变量放入一个接口类型变量，并将该结构放入一个引用类型变量中。接下来，有拳击，我们有对象类型作为输出。但它是一个接口类型变量。这意味着我们需要类型转换。所以，调用以这样的方式发生：

```csharp
IBoo boo = (IBoo)(box_to_object)new Foo();
boo.Boo();
```

编写此类代码无效。您将不得不更改副本而不是原件：

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

我们第一次查看代码时，除了我们自己的*之外，我们不必知道我们在代码中处理的是什么，并且看到了对IBoo接口的强制转换。这让我们觉得Foo是一个阶级，而不是一个结构。然后在结构和类中没有视觉分裂，这使我们认为
接口修改结果必须进入foo，这不会发生，因为boo是foo的副本。这是误导。在我看来，这段代码应该得到评论，所以其他开发人员可以处理它。

第二件事与先前的想法有关，我们可以将一个类型从一个对象转换为IBoo。这是盒装值类型是值类型的引用变体的另一个证明。或者，类型系统中的所有类型都是引用类型。我们可以像使用值类型一样使用结构，完全传递它们的值。正如您在C ++世界中所说的那样，取消引用指向对象的指针。

您可以反对，如果它是真的，它将如下所示：

```csharp
var referenceToInteger = (IInt32)10;
```

我们不仅会获得一个对象，还会获得一个盒装值类型的类型引用。它会破坏价值类型的整体概念(即价值的完整性)，从而允许基于其属性进行大量优化。让我们记下这个想法吧！

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

我们有一个完整的拳击模拟。但是，我们可以通过调用实例方法来更改其内容。这些更改将影响所有引用此数据结构的部分。

```csharp
var typedBoxing = new Boxed<int> { Value = 10 };
var pureBoxing = (object)10;
```

第一个变种不是很吸引人。而不是铸造类型，我们创造废话。第二行更好，但两条线几乎相同。唯一的区别是在堆上分配内存后，在通常的装箱期间没有使用零清除内存。必要的结构直接占用记忆，而第一个变种需要清洁。这使得它比通常的拳击工作时间长10％。

相反，我们可以为我们的盒装值调用一些方法。

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

我们有一种新仪器。让我们思考一下我们可以用它做些什么。

- 我们的`Boxed <T>`类型与通常的类型相同：在堆上分配内存，在那里传递一个值并允许通过执行一种unbox来获取它;
- 如果您丢失对盒装结构的引用，GC将收集它;
- 但是，我们现在可以使用盒装类型，即调用其方法;
- 此外，我们可以在SOH/LOH中替换另一个值类型的实例。我们之前无法做到，因为我们必须进行拆箱，将结构更改为另一个并进行反击，为客户提供新的参考。

拳击的主要问题是在内存中创建流量。未知数量的对象的流量，其中一部分可以存活到第一代，在那里我们遇到垃圾收集问题。会有很多垃圾，我们可以避免它。但是当我们拥有短期对象的流量时，第一个解决方案就是汇集。这是.NET翻筋斗的理想结束。

```csharp
var pool = new Pool<Boxed<Foo>>(maxCount:1000);
var boxed = pool.Box(10);
boxed.Value=70;

// use boxed value here

pool.Free(boxed);
```

现在拳击可以使用池，这可以消除拳击时的内存流量。我们甚至可以在终结方法中使对象恢复生命并将自己放回池中。当盒装结构转到除您之外的异步代码并且无法理解何时变得不必要时，这可能很有用。在这种情况下，它会在GC期间将自身返回池中。

让我们总结一下：

- 如果拳击是偶然的，不应该发生，不要让它发生。它可能导致性能问题。
- 如果系统架构需要装箱，则可能存在变型。如果盒装结构的流量很小且几乎不可见，则可以使用装箱。如果流量可见，您可能希望使用上述解决方案之一进行装箱。它花费了一些资源，但使GC工作没有过载;

最后让我们来看一个完全不切实际的代码：

```csharp
static unsafe void Main()
{
    // 这里我们创建了盒装的 int
    object boxed = 10;

    // 这里我们得到一个指向VMT的指针的地址
    var address = (void**)EntityPtr.ToPointerWithOffset(boxed);

    unsafe
    {
        // 这里我们得到一个虚拟方法表地址
        var structVmt = typeof(SimpleIntHolder).TypeHandle.Value.ToPointer();

       // 将传递给Heap的整数的VMT地址更改为VMT SimpleIntHolder，将Int转换为结构
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

代码使用一个小函数，它可以从引用到对象获取指针。该库可在[github address](https://github.com/mumusan/dotnetex/blob/master/libs/)上找到。此示例显示通常的装箱将int转换为类型化的引用类型。让我们看看过程中的步骤：

  1. 拳击整数。
  2. 获取获取对象的地址(Int32 VMT的地址)
  3. 获取SimpleIntHolder的VMT
  4. 将盒装整数的VMT替换为结构的VMT。
  5. 将拆箱拆分为结构类型
  6. 在屏幕上显示字段值，获取Int32，即盒装。

我是故意通过界面来做的，因为我想表明它会起作用那样。

### Nullable\<T\>

值得一提的是拳击与Nullable值类型的行为。Nullable值类型的这个特性是非常有吸引力的，因为值类型的装箱是null类型返回null。

```csharp
int? x = 5;
int? y = null;

var boxedX = (object)x; // -> 5
var boxedY = (object)y; // -> null
```

这导致我们得出一个特殊的结论：由于null没有类型，获取类型的唯一方法与盒装类型不同，如下所示：

```csharp
int? x = null;
var pseudoBoxed = (object)x;
double? y = (double?)pseudoBoxed;
```

代码的工作原理只是因为您可以使用null将类型转换为您喜欢的任何类型。

## 深入拳击

最后一点，我想告诉你[System.Enum type](http://referencesource.microsoft.com/#mscorlib/system/enum.cs,36729210e317a805)。从逻辑上讲，这应该是一个值类型，因为它是通常的枚举：在编程语言中将数字别名化为名称。但是，System.Enum是一种引用类型。在您的字段和.NET Framework中定义的所有枚举数据类型都继承自System.Enum。它是一种类数据类型。而且，它是一个抽象类，继承自`System.ValueType`。

```csharp
    [Serializable]
    [System.Runtime.InteropServices.ComVisible(true)]
    public abstract class Enum : ValueType, IComparable, IFormattable, IConvertible
    {
        // ...
    }
```

是否意味着所有枚举都在SOH上分配，当我们使用它们时，我们会重载堆和GC？实际上没有，因为我们只是使用它们。然后，我们假设在某处有一个枚举池，我们只是获取它们的实例。不，再说一次。您可以在编组时使用结构中的枚举。枚举是通常的数字。

事实是，CLR形成它的时候，如果有枚举[转动一类成值类型](https://github.com/dotnet/coreclr/blob/4b49e4330441db903e6a5b6efab3e1dbb5b64ff3/src/vm/methodtablebuilder.cpp#L1425-L1445):

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

为什么这样做？特别是，因为继承的想法 - 例如，要进行自定义枚举，您需要指定可能值的名称。但是，继承值类型是不可能的。因此，开发人员将其设计为一种引用类型，可以在编译时将其转换为值类型。

## 如果你想亲自看拳击怎么办？

幸运的是，您不必使用反汇编程序进入代码丛林。我们有整个.NET平台核心的文本，其中许多在.NET Framework CLR和CoreCLR方面是相同的。您可以点击下面的链接，立即查看拳击的实施：

- 有一组独立的优化，每组都使用a
    特定类型的处理器：
    -   *[JIT\_BoxFastMP\_InlineGetThread](https://github.com/dotnet/coreclr/blob/master/src/vm/amd64/JitHelpers_InlineGetThread.asm#L86-L148)*
        (AMD64 - multiprocessor or Server GC, implicit Thread Local Storage)
    -   *[JIT\_BoxFastMP](https://github.com/dotnet/coreclr/blob/8cc7e35dd0a625a3b883703387291739a148e8c8/src/vm/amd64/JitHelpers_Slow.asm#L201-L271)*
        (AMD64 - multiprocessor or Server GC)
    -   *[JIT\_BoxFastUP](https://github.com/dotnet/coreclr/blob/8cc7e35dd0a625a3b883703387291739a148e8c8/src/vm/amd64/JitHelpers_Slow.asm#L485-L554)*
        (AMD64 - single processor or Workstation GC)
    -   *[JIT\_TrialAlloc::GenBox(..)](https://github.com/dotnet/coreclr/blob/38a2a69c786e4273eb1339d7a75f939c410afd69/src/vm/i386/jitinterfacex86.cpp#L756-L886)*
        (x86) connected through JitHelpers
-   在一般情况下，JIT内联调用辅助函数
    [Compiler::impImportAndPushBox(..)](https://github.com/dotnet/coreclr/blob/a14608efbad1bcb4e9d36a418e1e5ac267c083fb/src/jit/importer.cpp#L5212-L5221)
-   通用版本使用较少优化
    [MethodTable::Box(..)](https://github.com/dotnet/coreclr/blob/master/src/vm/methodtable.cpp#L3734-L3783)
    -   Finally, [CopyValueClassUnchecked(..)] is called
        (https://github.com/dotnet/coreclr/blob/master/src/vm/object.cpp#L1514-L1581).
        它的代码显示了为什么最好选择包含8字节大小的结构。

这里，唯一的方法是用于取消装箱：
*[JIT\_Unbox(..)](https://github.com/dotnet/coreclr/blob/03bec77fb4efaa397248a2b9a35c547522221447/src/vm/jithelpers.cpp#L3603-L3626)*, 是绕 *[JIT\_Unbox\_Helper(..)](https://github.com/dotnet/coreclr/blob/03bec77fb4efaa397248a2b9a35c547522221447/src/vm/jithelpers.cpp#L3574-L3600)*.

此外，有趣的是 [unboxing](https://stackoverflow.com/questions/3743762/unboxing-does-not-create-a-copy-of-the-value-is-this-right)，拆箱并不意味着复制数据到堆。拳击意味着在测试类型的兼容性时将指针传递给堆。取消装箱后的IL操作码将使用此地址定义操作。可以将数据复制到局部变量或堆栈以调用方法。否则，我们会进行双重复制; 首先从堆复制到某处，然后复制到目标位置。
### The ref keyword

> TODO

### **makeref, **reftype, **refvalue, **arglist

> TODO

### Implicit boxing

> TODO

## 问题

### 为什么.NET CLR不能为装箱本身做池？

如果我们与任何Java开发人员交谈，我们将了解两件事：

  - Java中的所有值类型都是装箱的，这意味着它们本质上不是值类型。整数也是盒装的。
  - 出于优化的原因，从-128到127的所有整数都取自对象池。

那么，为什么在装箱期间.NET CLR中不会发生这种情况呢？很简单。因为我们可以更改盒装值类型的内容，所以我们可以执行以下操作：

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

如果我们处理池化，那么我们会将应用程序中的所有应用程序更改为138，这是不好的。

接下来是.NET中值类型的本质。他们处理价值，意味着他们工作得更快。拳击是罕见的，增加盒装数字属于幻想和糟糕的建筑世界。这根本没用。

### 当你调用一个采用对象类型的方法时，为什么不能在堆栈而不是堆上进行装箱，这实际上是一个值类型？

如果在堆栈上完成值类型装箱并且引用将转到堆，则方法内的引用可以转到其他位置，例如，方法可以将引用放在类的字段中。然后该方法将停止，并且制作拳击的方法也将停止。结果，引用将指向堆栈上的死区。

### 为什么不能将Value Type用作字段？

有时我们希望将结构用作使用第一个结构的另一个结构的字段。或者更简单：使用结构作为结构域。不要问我为什么这有用。这不可以。如果使用结构作为其字段或通过依赖于另一个结构，则创建递归，这意味着无限大小结构。但是，.NET Framework在某些地方可以执行此操作。一个例子是`System.Char`，[包含它自己](http://referencesource.microsoft.com/#mscorlib/system/char.cs,02f2b1a33b09362d):

```csharp
public struct Char : IComparable, IConvertible
{

    // Member Variables

    internal char m_value;

    //...
}
```

所有CLR原语类型都是以这种方式设计的。我们凡人都无法实现这种行为。而且，我们不需要这样做：在CLR中为原始类型提供OOP精神。

## 参考文献

- [获取指向对象的指针的库](https://github.com/mumusan/dotnetex/blob/master/libs/)

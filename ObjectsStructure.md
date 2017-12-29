# Структура объектов в памяти

До сих, говоря про разницу между значимыми и ссылочными типами, мы затрагивали эту тему с высоты конечного разработчика. Т.е. мы не смотрели на то как они в реальности устроены на уровне CLR, как сделаны те или иные механики внутри каждого из них. Мы смотрели, фактически, на конечный результат. Однако, чтобы понимать суть вещей глубже и чтобы отбросить в сторону последние оставшиеся мысли о какой-либо магии, происходящей внутри CLR стоит заглянуть в самые ее потроха.

## Внутренняя структура экземпляров типов

Если говорить о классах как о типах данных, то в разговоре об их типах данных достаточно вспомнить их базовое устройство. Давайте начнем с типа `object`, который является базовым типом и формирует структуру для всех ссылочных типов:

### System.Object

 ```

  ----------------------------------------------
  |  SyncBlkIndx |    VMTPtr    |     Data     |
  ----------------------------------------------
  |  4 / 8 байт  |  4 / 8 байт  |  4 / 8 байт  |
  ----------------------------------------------
  |  0xFFF..FFF  |  0xXXX..XXX  |      0       |
  ----------------------------------------------
                 ^
                 | Сюда ведут ссылки на объект. Т.е. не в начало, а на VMT

  Sum size = 12 (x86) .. 24 (x64)
 ```

Т.е. фактически размер зависит от конечной платформы, на которой будет работать приложение.

Теперь, чтобы получить дальнейшее понимание с чем мы имеем дело, давайте проследуем по указателю `VMTPtr`. Для всей системы типов этот указатель является самым главным: именно через него работает и наследование, и реализация интерфейсов, и приведение типов, и много чего еще. Этот указатель - отсылка в систему типов .NET CLR.

### Virtual Methods Table

Описание самой таблицы доступно по адресу в [GitHub CoreCLR](https://github.com/dotnet/coreclr/blob/master/src/vm/methodtable.h), и если отбросить все лишнее (а там 4381 строка! Парни из CoreCLR team не из пугливых), [выглядит она следующим образом](https://github.com/dotnet/coreclr/blob/master/src/vm/methodtable.h#L4099-L4114):

> Это версия из CoreCLR. Если смотреть на структуру полей в .NET Framework, то она будет отличаться расположением полей.

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

 Согласитесь, выглядит пугающе. Причем пугающе не в том что тут всего 6 полей (а где все остальные?), а в том что для того чтобы до них добраться, нам надо было пропустить 4,100 строк логики. Но давайте не будем унывать и попытаемся сразу получить из этого выгоду: мы, пока что, понятия не имеем что имеется ввиду под другими полями, зато поле `m_BaseSize` выглядит заманчиво. Как подсказывает нам комментарий, это - фактический размер для экземпляра типа. Попробуем в бою?

 Чтобы получить адрес VMT мы можем пойти двумя путями: либо зайти со сложного конца, получив адрес объекта, а значит и VMT (часть этого код уже была на страницах этой книги, но не ругайте меня: я не хочу чтобы вы его искали):

 ```csharp
class Program
{
    public static unsafe void Main()
    {
        Union x = new Union();
        x.Reference.Value = "Hello!";

        // Первым полем лежит указатель на место, где лежит
        // указатель на VMT
        // - (IntPtr*)x.Value.Value - преобразовали число в указатель (сменили тип для компилятора)
        // - *(IntPtr*)x.Value.Value - взяли по адресу объекта адрес VMT
        // - (void *)*(IntPtr*)x.Value.Value - преобразовали в указатель
        void *vmt = (void *)*(IntPtr*)x.Value.Value;

        // вывели в консоль адрес VMT;
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

Либо тот же самый адрес возвращается вполне себе .NET FCL API:

```csharp
    var vmt = typeof(string).TypeHandle.Value;
```

Второй путь конечно же проще (хоть и дольше работает). Однако знание первого очень важно с точки зрения понимания структуры экземпляра типа. Пользование вторым путем хоть и добавляет чувства уверенности: если мы вызываем метод API, то вроде как пользуемся задокументированным способом работы с VMT. А если достаем через указатели, то нет. Но не стоит забывать что хранение `VMT *` - стандартно для практически любого ООП языка и для .NET платформы в целом: она всегда находится на одном и том же месте.

Давайте изучим вопрос структуры типов с точки зрения размера их экземпляра. Нам же надо не просто абстрактно изучать их (это просто-напросто скучно), но дополнительно попробуем извлечь из этого такую выгоду, какую не извлечь обычным способом.

> **Почему sizeof есть для Value Type но нет для Reference Type?** На самом деле вопрос открытый т.к. никто не мешает рассчитать размер ссылочного типа. Единственное обо что можно споткнуться - это нефиксированный размер двух ссылочных типов: `Array` и `String`. А также `Generic` группы, которая зависит целиком и полностью от конкретных вариантов. Т.е. оператором `sizeof(..)` мы обойтись не смогли бы: необходимо работать с конкретными экземплярами. Однако никто не мешает сделать метод типа `static int System.Object.SizeOf(object obj)`, который бы легко и просто возвращал бы нам то что надо. Так почему же Microsoft не реализовала этот метод? Есть мысль что платформа .NET в их понимании не та платформа, где разработчик будет сильно переживать за конкретные байты. В случае чего можно просто доставить планок в материнскую плату. Тем более что большинство типов данных, которые мы реализуем, ну занимает такие большие объемы. Однако тем, кому нужно все что нужно подсчитают все размеры так как надо. Последнее, конечно, спорно.

Но не будем отвлекаться. Итак, чтобы получить размер экземпляра любого класса, экземпляры которого имеют фиксированный размер, достаточно написать следующий код:

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

class GenericSample<T>
{
    T fld;
}

// ...

Console.WriteLine(SizeOf(typeof(Sample)));
```

Итак, что мы только что сделали? Первым шагом мы получили указатель на таблицу виртуальных методов. Далее привели тип к указателю на таблицу виртуальных методов (очень упрощенная её версия). После чего мы считываем размер и получаем `12` - это сумма размеров полей `SyncBlockIndex + VMT_Ptr + поле x` для 32-разрядной платформы. Если мы поиграемся с разными типами то получим примерно следующую таблицу:

Тип или его определение | Размер    | Комментарий
------------------------|-----------|--------------
Object | 12 | SyncBlk + VMT + пустое поле
Int16 | 12 | Boxed Int16: SyncBlk + VMT + данные (выровнено по 4 байта на x86)
Int32 | 12 | Boxed Int32: SyncBlk + VMT + данные
Int64 | 16 | Boxed Int64: SyncBlk + VMT + данные
Char | 12 |  Boxed Char: SyncBlk + VMT + данные (выровнено по 4 байта на x86)
Double | 16 | Boxed Double: SyncBlk + VMT + данные
IEnumerable | 0 | Интерфейс не имеет размера: надо брать obj.GetType()
List\<T> | 24 | Не важно сколько элементов в List<T>, занимать он будет одинаково т.к. хранит данные он в array, который не учитывается
GenericSample\<int> | 12 | Как видите, generics прекрасно считаются. Размер не поменялся, т.к. данные находятся на том же месте что и у boxed int. Итог: SyncBlk + VMT + данные = 12 байт (x86)
GenericSample\<Int64> | 16 | Аналогично
GenericSample\<IEnumerable> | 12 | Аналогично
GenericSample\<DateTime> | 16 | Аналогично
string | 14 | Это значение будет возвращено для любой строки т.к. реальный размер должен считаться динамически. Однако он подходит для размера под пустую строку. Прошу заметить что размер не выровнен по разрядности: по сути это поле использоваться не должно
int[]{1} | 24554 | Для массивов в данном месте лежат совсем другие данные плюс их размер не является фиксированным, потому его необходимо считать отдельно

Как видите, когда система хранит данные о размере экземпляра типа, то она фактически хранит данные для ссылочного типа (в том числе для ссылочного варианта значимого). Давайте сделаем некоторые выводы:

  1. Если вы хотите знать, сколько займет значимый тип как значение, используйте `sizeof(TType)`
  1. Если вы хотите рассчитать чего вам будет стоить боксинг, то вы можете округлить `sizeof(TType)` в большую сторону до размера слова процессора (4 или 8 байт) и прибавить еще 2 слова. Или же взять это значение из `VMT` типа.
  1. При необходимости понять во что нам обойдется выделение памяти в куче, у нас три варианта:
    1. Обычный ссылочный тип фиксированного размера: мы можем забрать размер экземпляра из `VMT`;
    1. Если это строка, необходимо вручную считать ее размер (это вообще редко когда может понадобиться, но, согласитесь, интересно)
    1. Если это массив, то его размер также рассчитывается отдельно: на основании размера его элементов и их количества. Эта задачка может оказаться куда более полезной: ведь именно массивы первые в очереди на попадание в `LOH`

### System.String

Про строки в вопросах практики мы поговорим отдельно: этому относительно небольшому классу можно выделить целую главу. А в рамках главы про строение VMT мы поговорим про строение строк на низком уровне. Для хранения строк применяется стандарт UTF16. Это значит что каждый символ занимает 2 байта. Дополнительно в конце каждой строки хранится null-терминатор (т.е. значение, которое идентифицирует что строка закончилась). Также хранится длина строки в виде Int32 числа - чтобы не считать длину каждый раз когда она вам понадобится. Про кодировки мы поговорим отдельно, а пока этой информации нам хватит.

```
  // Для .NET Framework 3.5 и младше
  -------------------------------------------------------------------------
  |  SyncBlkIndx |    VMTPtr     |     Length     | char  | char  | Term  |
  -------------------------------------------------------------------------
  |  4 / 8 байт  |  4 / 8 байт   |    4 байта     |  2 б. |  2 б. |  2 б. |
  -------------------------------------------------------------------------
  |      -1      |  0xXXXXXXXX   |        2       |   a   |   b   | <nil> |
  -------------------------------------------------------------------------

  Term - null terminator
  Sum size = (12 (24) + 2 + (Len*2)) -> округлить в большую сторону по разрядности. (20 байт в примере)
  // Для .NET Framework 4 и старше
  ------------------------------------------------------------------------------------------
  |  SyncBlkIndx |    VMTPtr     |  ArrayLength   |     Length     | char  | char  | Term  |
  ------------------------------------------------------------------------------------------
  |  4 / 8 байт  |  4 / 8 байт   |    4 байта     |    4 байта     |  2 б. |  2 б. |  2 б. |
  ------------------------------------------------------------------------------------------
  |      -1      |  0xXXXXXXXX   |        3       |        2       |   a   |   b   | <nil> |
  ------------------------------------------------------------------------------------------
  Term - null terminator
  Sum size = (12 (24) + 2 + (Len*2)) -> округлить в большую сторону по разрядности. (24 байта в примере)
 ```
Перепишем наш метод чтобы научить его считать размер строк:

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
            return DWORD * ((baseSize + 2 + 2 * length + (DWORD -1)) / DWORD);
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

Где `SizeOf(type)` будет вызывать старую реализацию - для фиксированных по длине ссылочных типов.

Давайте проверим код на практике:

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

Расчеты показывают что размер строки увеличивается не линейно, а ступенчато: каждые два символа. Это происходит потому что размер каждого символа - 2 байта, они следуют друг за другом. Но конечный размер должен без остатка делиться на разрядность процессора. Т.е. некоторые строки доберут еще 2 байта "вверх". Результат нашей работы прекрасен: мы можем подсчитать во что нам обошлась та или иная строка. Последним этапом нам осталось узнать как считать размер массивов в памяти, и чтобы задача стала еще более практичной давайте сделаем метод, который будет отвечать нам на вопрос: какого размера массив надо будет взять чтобы мы уместились в SOH. Может показаться что использовать свойство Length было бы разумнее и быстрее: однако, на самом деле, это будет медленнее работать: дополнительные издержки.

### Массивы

Строение массивов несколько сложнее: ведь у массивов могут быть варианты их строения:

  1. Они могут хранить значимые типы, а могут хранить ссылочные
  1. Массивы могут содержать как одно так и несколько измерений
  1. Каждое измерение может начинаться как с `0` так и с любого другого числа (это на мой взгляд очень спорная возможность: избавлять программиста от лени сделать `arr[i - startIndex]` на уровне FCL)

Отсюда и некоторая путаность в реализации массивов и невозможность точно предсказать размер конечного массива: мало перемножить количество элементов на их размер. Хотя, конечно, для большинства случаев это будет более-менее достаточным. Важным размер становится когда мы боимся попасть в LOH. Однако у нас и тут возникают варианты: мы можем просто накинуть к размеру, подсчитанному "на коленке" какую-то константу сверху (например, 100) чтобы понять, перешагнули мы границу в 85000 или нет. Однако, в рамках данного раздела задача несколько другая: понять структуру типов. На нее и посмотрим:

```
  // Заголовок
  ----------------------------------------------------------------------------------------
  |   SBI   |  VMTPtr |  Total  |  Len_1  |  Len_2  | .. |  Len_N  |  Term   | VMT_Child |
  ----------------------------------opt-------opt------------opt-------opt--------opt-----
  |  4 / 8  |  4 / 8  |    4    |    4    |    4    |    |    4    |    4    |    4/8    |
  ----------------------------------------------------------------------------------------
  | 0xFF.FF | 0xXX.XX |    ?    |    ?    |    ?    |    |    ?    | 0x00.00 | 0xXX..XX  |
  ----------------------------------------------------------------------------------------

  - opt: опционально
  - SBI: Sync Block Index
  - VMT_Child: присутствует только если массив хранит данные ссылочного типа
  - Total: присутствует для оптимизации. Общее количество элементов массива с учетом всех размерностей
  - Len_2..Len_N + Term: присутствуют только если размерность массива более 1 (регулируется битами в VMT->Flags)
```

Как мы видим, заголовок типа хранит данные об измерениях массива: их число может быть как 1 так и достаточно большим: фактически их размер ограничивается только null-терминатором, означающим что перечисление закончено. Данный пример доступен полностью в файле [GettingInstanceSize](./samples/GettingInstanceSize.linq), а ниже я приведу только его самую важную часть:

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
        total += IsMultidimentional ? Dimensions * sizeof(int) : 0;
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

Этот код учитывает все вариации типов массивов и может быть использован для расчета его размера:

```csharp
Console.WriteLine($"size of int[]{{1,2}}: {SizeOf(new int[2])}");
Console.WriteLine($"size of int[2,1]{{1,2}}: {SizeOf(new int[1,2])}");
Console.WriteLine($"size of int[2,3,4,5]{{...}}: {SizeOf(new int[2, 3, 4, 5])}");

---
size of int[]{1,2}: 20
size of int[2,1]{1,2}: 32
size of int[2,3,4,5]{...}: 512
```

### Выводы к разделу

На данном этапе мы научились нескольким достаточно важным вещам. Первое - мы разделили для себя ссылочные типы на три группы: на ссылочные типы фиксированного размера, generic типы и ссылочные типы переменного размера. Также мы научились понимать структуру конечного экземпляра любого типа (про структуру VMT я пока молчу. Мы там поняли целиком пока что только одно поле: а это тоже большое достижение). Будь то фиксированного размера ссылочный тип (там все предельно просто) или же неопределенного размера ссылочный тип: массив или строка. Неопределенного потому что его размер будет определен при создании. С generic типами на самом деле все просто: для каждого конкретного generic типа создается своя VMT, в которой будет проставлен конкретный размер.

## Virtual Methods Table (VMT)

Объяснение работы Methods Table, по большей части, академическое: ведь в такие дебри лезть - это как самому себе могилу рыть. С одной стороны такие закрома таят что-то будоражащее и интересное, хранят некие данные, которое еще больше раскрывают понимание о происходящем. Однако с другой стороны все мы понимаем что Microsoft не будет нам давать никаких гарантий что они оставят свой рантайм без изменений и, например, вдруг не передвинут таблицу методов на одно поле вперед. Поэтому, оговорюсь сразу:

> Информация, представленная в данном разделе, дана вам исключительно для того чтобы вы понимали, как работает приложение, основанное на CLR, и ручное вмешательство в ее работу не дает никаких гарантий. Однако, это настолько интересно что я не могу вас отговорить. Наоборот, мой совет - поиграйтесь с этими структурами данных и, возможно, вы получите один из самых запоминающихся опытов в разработке ПО. 

Ну все, предупредил. Теперь давайте окунемся в мир как говорится зазеркалья. Ведь до сих пор всё зазеркалье сводилось к знаниям структуры объектов: а её по-идее мы и так должны знать хотя бы примерно. И по своей сути эти знания зазеркальем не являются, а являются скорее входом в зазеркалье. А потому вернемся к структуре ```MethodTable```, [описанной в CoreCLR](https://github.com/dotnet/coreclr/blob/master/src/vm/methodtable.h#L4099-L4114):

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

 А именно к полям `m_wNumVirtuals` и `m_wNumInterfaces`. Эти два поля определяют ответ на вопрос "сколько виртуальных методов и интерфейсов существует у типа?". В этой структуре нет никакой информации об обычных методах, полях, свойствах (которые объединяют методы). Т.е. эта структура **никак не связана с рефлексией**. По своей сути и назначению эту структура создана для работы вызова методов в CLR (и на самом деле в любом ООП: будь то Java, C++, Ruby или же что-то еще. Просто расположение полей будет несколько другим). Давайте рассмотрим код:

 ```csharp
 public class Sample
 {
     public int _x;

     public void ChangeTo(int newValue)
     {
         _x = newValue;
     }

     public virtual GetValue()
     {
         return _x;
     }
 }
 
 public class OverridedSample : Sample
 {
     public override GetValue()
     {
         return 666;
     }
 }
 
 ```

Какими бы бессмысленными не казались эти классы, они нам вполне сгодятся для описания их VMT. А для этого мы должны понять чем отличаются базовый тип и унаследованный в вопросе методов `ChangeTo` и `GetValue`.

Метод `ChangeTo` присутствует в обоих типах: при этом его нельзя переопределять. Это значит что он может быть переписан так и ничего не поменяется:

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

// Либо в случае если бы он был struct
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

И при этом кроме архитектурного смысла ничего не изменится: поверьте, при компиляции оба варианта будут работать одинаково, т.к. у экземплярных методов `this` - это всего лишь первый параметр метода, который передается нам неявно.

> Заранее поясню, почему все объяснения вокруг наследования строятся вокруг примеров на статических методах: по сути все методы - статические. И экземплярные и нет. В памяти нет поэкземплярно скомпилированных методов для каждого экземпляра класса. Это занимало бы огромное количество памяти: проще одному и тому же методу каждый раз передавать ссылку на экземпляр той структуры или класса, с которыми он работает.

Для метода `GetValue` все обстоит совершенно по-другому. Мы не можем просто взять и переопределить метод переопределением *статического* `GetValue` в унаследованном типе: новый метод получат только те участки кода, которые работают с переменной как с `OverridedSample`, а если с переменной работать как с переменной базового типа `Sample` вызвать вы можете только `GetValue` базового типа поскольку вы понятия не имеете какого типа на самом деле объект. Для того чтобы понимать какого типа является переменная и как результат - какой конкретно метод вызывается, мы можем поступить следующим образом:

```csharp
void Main()
{
    var sample = new Sample();
    var overrided = new OverridedSample();

    Console.WriteLine(sample.Virtuals[Sample.GetValuePosition].DynamicInvoke(sample));
    Console.WriteLine(overrided.Virtuals[Sample.GetValuePosition].DynamicInvoke(sample));
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

public class OverridedSample : Sample
{
    public OverridedSample() : base()
    {
        Virtuals[0] = new Func<Sample, int>(GetValue);
    }

    public static new int GetValue(Sample self)
    {
        return 666;
    }
}
```

В этом примере мы фактически строим таблицу виртуальных методов вручную, а вызовы делаем по позиции метода в этой таблице. Если вы поняли суть примера, то вы фактически поняли как строится наследование на уровне скомпилированного кода: методы вызываются по своему индексу в таблице виртуальных методов. Просто когда вы создаете экземпляр некоторого унаследованного типа, то по местам где у базового типа находятся виртуальные методы компилятор расположит указатели на переопределенные методы. Таким образом, отличие нашего примера от реальной VMT заключается только в том, что когда компилятор строит эту таблицу, он заранее знает с чем имеет дело и создает таблицу правильного размера: в нашем примере чтобы построить таблицу для типов, которые будут делать таблицу более крупной за счет добавления новых методов придется изрядно попотеть. Но наша задача заключается в другом, а потому такими извращениями мы заниматься не станем.

Второй вопрос, который возникает сразу после ответа на первый: если с методами теперь все ясно, то зачем тогда в `MethodInfo` находятся интерфейсы? Интерфейсы, если размышлять логически, не входят в структуру прямого наследования. Они находятся как-бы сбоку, указывая что те или иные типы обязаны реализовывать некоторый набор методов. Иметь по сути некоторый протокол взаимодействия. Однако хоть интерфейсы и находятся *сбоку* от прямого наследования, вызывать методы все равно можно. Причем, заметьте: если вы используете переменную интерфейсного типа, то за ней могут cкрываться какие угодно классы, базовый тип у которых может быть разве что `System.Object`. Т.е. методы в таблице виртуальных методов, которые реализуют интерфейс могут находиться совершенно по разным местам. Как же вызов методов работает в этом случае?

## Virtual Stub Dispatch (VSD) [In Progress]

Чтобы разобраться в этом вопросе необходимо дополнительно вспомнить что реализовать интерфейс можно двумя путями: сделать можно либо `implicit` реализацию, либо `explicit`. Причем сделать это можно частично: часть методов сделать `implicit`, а часть - `explicit`. Эта на самом деле - следствие реализации, а не заранее продуманная возможность: реализуя интерфейс, вы показываете явно или неявно что в него входит. Часть методов класса может не входить в интерфейс, а методы, существующие в интерфейсе, могут не существовать в классе (они, конечно, существуют в классе, но синтаксис показывает что архитектурно частью класса они не являются): класс и интерфейс - это, в некотором смысле, - параллельные иерархии типов. Также, в плюс к этому, интерфейс - это отдельный тип, а значит у каждого интерфейса есть собственная таблица виртуальных методов: чтобы каждый смог вызывать методы интерфейса.

Давайте взглянем на таблицу: как бы могли выглядеть VMT различных типов:

| interface IFoo  |  class A : IFoo  | class B : IFoo |
------------------|------------------|-----------------
| -> GetValue()   |  SampleMethod()  | RunProcess()   |
| -> SetValue()   |  Go()            | -> GetValue()  |
|                 | -> GetValue()    | -> SetValue()  |
|                 | -> SetValue()    | LookToMoon()   |

VMT всех трех типов содержат необходимые методы `GetValue` и `SetValue`, однако они находятся по разным индексам: они не могут везде быть по одним и тем же индексам поскольку была бы конкуренция за индексы с другими интерфейсами класса. На самом деле для каждого интерфейса создается интерфейс - дубль - для каждой его реализации в каждом классе. Имеем 633 реализации `IDisposable` в классах FCL/BCL? Значит имеем 633 дополнительных `IDisposable` интерфейса чтобы поддержать VMT to VMT трансляцию для каждого из классов + запись в каждом классе с ссылкой на его реализацию интерфейсом. Назовем такие интерфейсы **частными интерфейсами**. Т.е. каждый класс имеет свои собственные, **частные интерфейсы**, которые являются "системными" и являются прокси типами до реального интерфейса.

Единственное, о чем я пока не буду говорить, так это о структуре места, где хранится эти *частные интерфейсы*. Просто скажу что это некоторая таблица.

Таким образом получается следующее: у интерфейсов также как и у классов есть наследование виртуальных *интерфейсных* методов, однако наследование это работает не только при наследовании одного интерфейса от другого, но и при реализации интерфейса классом. Когда класс реализует некий интерфейс, то создается дополнительный интерфейс, уточняющий какие методы интерфейса-родителя на какие методы конечного класса должны отображаться. Вызывая метод по интерфейсной переменной, вы точно также вызываете метод по индексу из массива VMT как это делалось в случае с классами, однако для данной конкретно реализации интерфейса вы по индексу выберите слот из *унаследованного*, невидимого интерфейса, связывающего оригинальный интерфейс `IDisposable` с нашим классом, интерфейс реализующем.

Диспетчеризация виртуальных методов через заглушки или Virtual Stub Dispatch (VSD) была разработана еще в 2006 году как замена таблицам виртуальных методов в интерфейсах. Основная идея этого подхода состоит в упрощении кодогенерации и последующего упрощения вызова методов, т.к. первичная реализация интерфейсов на VMT была очень громоздкой и требовала большого количества работы и времени для построения всех структур всех интерфейсов. Сам код диспетчеризации находится по сути в четырех файлах общим весом примерно в 6400 строк и мы не строим целей понять его весь. Мы попытаемся в общих словах понять суть происходящих в этом коде процессов. Всю логику VSD диспетчеризации можно разбить на два больших раздела: это сама диспетчеризация и механизм заглушек (stubs), обеспечивающих кэширование адресов вызываемых методов по паре значений [тип;номер слота], которые их идентифицируют.

### Dispatch Token

Токены диспетчеризации - это по своей сути 4 или 8 - байтовые структуры данных (в зависимости от платфоры), которые строятся во время построения структур данных, обеспечивающих вызов методов конкретных типов и внутри себя являются комбинацией пары <интерфейсный тип, номер слота метода>

Также, чтобы облегчить интеграцию с рантаймом реализация подразумевает что порядок номеров слотов будет совпадать с порядком классической v-table. Единственное - реальная VMT будет доступна через вызов специального метода вместо того чтобы быть доступной напрямую чтобы скрыть эту абстракцию.

Термин *слот* будет использоваться в контексте номера слота в таблице виртуальных методов, которая создана и интерпретируется механизмом отображения (mapping).

Базовые структуры, [приведенные самой компанией Microsoft](https://github.com/dotnet/coreclr/blob/32f0f9721afb584b4a14d69135bea7ddc129f755/src/vm/contractimpl.h):

```csharp
struct DispatchToken
{
private:
    // IMPORTANT: This is the ONLY member of this class.
    UINT_PTR     m_token;

#ifndef _WIN64
    static const UINT_PTR MASK_TYPE_ID       = 0x00007FFF;                // Маска TypeId в m_token
    static const UINT_PTR MASK_SLOT_NUMBER   = 0x0000FFFF;                // Маска SlotNumber в m_token
    static const UINT_PTR SHIFT_TYPE_ID      = 0x10;                      // Позиция TypeId в m_token
    static const UINT_PTR SHIFT_SLOT_NUMBER  = 0x0;                       // Позиция SlotNumber в m_token
    static const UINT_PTR INVALID_TOKEN      = 0x7FFFFFFF;
#else //_WIN64
    static const UINT_PTR MASK_TYPE_ID       = UI64(0x000000007FFFFFFF);  // Маска TypeId в m_token
    static const UINT_PTR MASK_SLOT_NUMBER   = UI64(0x000000000000FFFF);  // Маска SlotNumber в m_token

    static const UINT_PTR SHIFT_TYPE_ID      = 0x20;                      // Позиция TypeId в m_token
    static const UINT_PTR SHIFT_SLOT_NUMBER  = 0x0;                       // Позиция SlotNumber в m_token
    static const UINT_PTR INVALID_TOKEN      = 0x7FFFFFFFFFFFFFFF;
#endif //_WIN64

    // ...

}
```

#### MethodTable

##### Таблица реализаций

Таблица реализаций представляет собой массив, который для каждого метода, включенного в некий тип содержит указатель на точку входа в метод. Записи этой таблицы построены в следующем порядке:

  - Новые виртуальные методы
  - Новые не виртуальные методы
  - Переопределенные методы

Такой формат был выбран исходя из желания сделать естественное расширение формата таблицы виртуальных методов. В результате такого размещения любую запись можно вычислить исходя из порядка размещения и количества записей в каждой группе.

В том случае если VSD для данного типа отключена, то данная таблица у типа отсутствует и заменяется обычной таблицей виртуальных методов, которую мы рассмотрели ранее.

##### Карта слотов

Slots map или карта слотов - это таблица, которая содержит набор записей следующего содержания: тип, слот, область применимости (scope),

```csharp
// Идентификатор типа (Type ID) используется в карте диспетчеризиции и хранит внутри себя один из следующих типов данных:
//   - специальное значение, говорящее что это - "this" class
//   - специальное значение, показывающее что это - тип интерфейса, не реализованный классом
//   - индекс в InterfaceMap
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

struct DispatchSlot
{
protected:
    PCODE m_slot;
    // ...
}

```

#### Type ID Map

Этот класс хранит карту типов как отражение некоторого TypeId на конкретный тип а также - дополнительно - в обратную сторону. Сделано это исключительно из соображений производительности. Построение этих хэш таблиц происходит динамически: по запросу TypeId относительно PTR_MethodTable возвращается либо FatId либо просто Id.

```csharp
class TypeIDMap
{
protected:
    HashMap             m_idMap;  // Хранит map TypeID -> PTR_MethodTable
    HashMap             m_mtMap;  // Хранит map PTR_MethodTable -> TypeID
    Crst                m_lock;
    TypeIDProvider      m_idProvider;
    BOOL                m_fUseFatIdsForUniqueness;
    UINT32              m_entryCount;

    // ...
}
```

## Почему?

> Вопрос: почему если каждый класс может реализовать интерфейс, то нельзя вытащить конкретную реализацию интерфейса у объекта?

Ответ прост: это непокрытая возможность CLR при проектировании языка, CLR этот вопрос никак не ограничивает. Мало того, это с высокой долей вероятности будет добавлено в ближайших версиях C#, благо они выходят достаточно быстро. Рассмотрим пример:

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

Здесь мы вызываем четыре различных метода и результат их вызова будет таким:

```csharp
Foo.IDisposable::Dispose
Foo::Dispose()
Boo.IDisposable::Dispose
Boo::Dispose()
```

Причем несмотря на то что мы имеем *explicit* реализацию интерфейса в обоих классах, в классе `Boo` *explicit* реализацию интерфейса `IDisposable` для `Foo` получить не получится. Даже если мы напишем так:

```csharp
((IDisposable)(Foo)boo).Dispose();
```

Все равно мы получим на экране все то же результат:

```csharp
Boo.IDisposable::Dispose
```

С одной стороны это вообще не проблема: если бы C# + CLR позволяли бы такие шалости, мы бы в некотором смысле получили бы нарушение консистентности в строении типов. Сами подумайте: вы сделали крутую архитектуру, все хорошо. Но кто-то почему-то вызывает методы не так как вы задумали. Это было бы ужасно. С другой стороны в C++ похожая возможность существует и там не сильно жалуются на это. Почему я говорю что это может быть добавлено в C#? Потому что не менее ужасный функционал [уже обсуждается](https://github.com/dotnet/csharplang/issues/52) и выглядеть он должен примерно так:

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

Почему это ужасно? Ведь на самом деле это порождает целый класс возможностей. Теперь нам не нужно будет каждый раз реализовывать какие-то методы интерфейсов, которые везде реализовывались одинаково. Звучит прекрасно. Но только звучит. Ведь интерфейс - это протокол взаимодействия. Протокол - это набор правил, рамки. В нем нельзя допускать существование реализаций. Здесь же идет прямое нарушение этого принципа и введение еще одного: множественного наследования. Я, честно, против таких доработок. Но.. Я что-то ушел в сторону.

[DispatchMap::CreateEncodedMapping](https://github.com/dotnet/coreclr/blob/master/src/vm/contractimpl.cpp#L295-L460)
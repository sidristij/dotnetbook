# Memory\<T> и Span\<T>

Как язык, так и платформа существуют уже много лет: и все это время существовало множество средств для работы с неуправляемым кодом. Так почему же сейчас выходит очередной API для работы с неуправляемым кодом если по сути он существовал уже много-много лет? Для того чтобы ответить на этот вопрос достаточно понять чего не хватало нам раньше.

Разработчики платформы и раньше пытались нам помочь скрасить будни разработки с использованием неуправляемых ресурсов: это и автоматические врапперы для импортируемых методов. И маршаллинг, который в большинстве случаев работатет автоматически. Это также инструкция `stackallloc`, о которой говорится в главе про стек потока. Однако, как по мне если ранние разработчики с использованием языка C# приходили из мира C++ (как сделал это и я), то сейчас они приходят из более высокоуровневых языков (я, например, знаю разработчика, который пришел из JavaScript). А что это означает? Это означает что люди со все большим подозрением начинают относиться к неуправляемым ресурсам и конструкциям, близким по духу к C/C++ и уж тем более - к языку Ассемблера.

Как результат такого отношения - все меньшее и меньшее содержание unsafe кода в проектах и все большее доверие к API самой платформы. Это легко проверяется если поискать использование конструкции `stackalloc` по открытым репозиториям: оно ничтожно мало. Но если взять любой код, который его использует:

**Класс Interop.ReadDir**
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

Становится понятна причина непопулярности. Посмотрите не вчитываясь на код и ответьте для себя на один вопрос: доверяете ли вы ему? Могу предположить что ответом будет "нет". Тогда ответьте на другой: почему? Ответ будет очевиден: помимо того что мы видим слово `Dangerous`, которое как-бы намекает что что-то может пойти не так, второй фактор, влияющий на наше отношение - это строчка `byte* buffer = stackalloc byte[s_readBufferSize];`, а если еще конкретнее - `byte*`. Эта запись - триггер для любого чтобы в голове появилась мысль: "а что, по-другому сделать нельзя было что-ли?". Тогда давайте еще чуть-чуть разберемся с психоанализом: отчего может возникнуть подобная мысль? С одной стороны мы пользуемся конструкциями языка и предложенный здесь синтаксис далек от, например, C++/CLI, который позволяет делать вообще все что угодно (в том числе делать вставки на чистом Assembler), а с другой он выглядит непривычно.

Так в чем же вопрос? Как вернуть разработчиков обратно в лоно неуправляемого кода? Необходимо дать им чувство спокойствия что они не могут сделать ошибку случайно, по незнанию. Итак, для чего же введены `типы Span<T>` и `Memory<T>`?

## Span\<T>, ReadOnlySpan\<T>

Тип `Span` олицетворяет собой часть некоторого массива данных, поддиапазон его значений. При этом позволяя как и в случае массива работать с элементами этого диапазона как на запись, так и на чтение. Однако, давайте для разгона и общего понимания сравним типы данных, для которых сделана реализация типа `Span` и посмотрим на возможные цели его введения.

Первый тип данных, о котором хочется завести речь - это обычный массив. Для массивов работа со Span будет выглядеть следующим образом:

```csharp
    var array = new [] {1,2,3,4,5,6};
    var span = new Span<int>(array, 1, 3);
    var position = span.BinarySearch(3);
    Console.WriteLine(span[position]);  // -> 3
```

Как мы видим в данном примере, для начала мы создаем некий массив данных. После этого мы создаем `Span` (или подмножество), который ссылаясь на сам массив, разрешает его использующему коду доступ только в тот диапазон значений, который был указан при инициализации.

Тут мы видим первое свойство этого типа данных: это создание некоторого контекста. Давайте разовьем нашу идею с контекстами:

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

Как мы видим, `Span<T>` вводит абстракцию доступа к некоторому участку памяти как на чтение так и на запись. Что нам это дает? Если вспомнить, на основе чего еще может быть сделан `Span`, то мы вспомним как про неуправляемые ресурсы, так и про строки:

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
if (TryParseInt32(arrSpan, out var res2))
{
    Console.WriteLine(res2);
}

// void *
Span<char> buf = stackalloc char[6];
buf[0] = '1'; buf[1] = '2'; buf[2] = '3';
buf[3] = '4'; buf[4] = '5'; buf[5] = '6';

if (TryParseInt32(arrSpan, out var res3))
{
    Console.WriteLine(res3);
}

-----
234
234
234
```

Т.е., получается, что `Span<T>` - это средство унификации по работе с памятью: управляемой и неуправляемой, которое гарантирует безопасность в работе с такого рода данными во время Garbage Collection: если участки памяти с управляемыми массивами начнут двигаться, то для нас это будет безопасно.

Однако, стоит ли так сильно радоваться? Можно ли было всего этого добиться и раньше? Например, если говорить об управляемых массивах, то тут даже сомневаться не приходится: достаточно просто обернуть массив в еще один класс, предоставив аналогичный интерфейс и все готово. Мало того, аналогичную операцию можно проделать и со строками: они обладают необходимыми методами. Опять же, достаточно строку завернуть в точно такой же тип и предоставить методы по работе с ней. Другое дело что для того чтобы хранить в одном типе строку, буфер или массив, придется сильно повозиться:

```csharp
public readonly ref struct OurSpan<T>
{
    private T[] _array;
    private string _str;
    private T * _buffer;

    // ...
}
```

Обеспечивая единую работу для одних и тех же методов для разных внутренностей. Получается, что для того чтобы сделать `managed` средство единого интерфейса между этими типами данных, сохранив при этом максимальную производительность, отличного от `Span<T>` пути не существует.

Далее, если продолжить рассуждения, то что такое `ref struct` в понятиях `Span`? Это именно те самые "структуры, они только на стеке", о которых мы так часто слышим на собеседованиях. А это значит, что этот тип данных может идти только через стек и не имеет права уходить в кучу. А потому `Span`, будучи ref структурой, является контекстным типом данных, обеспечивающим работу методов, но не объектов в памяти. От этого для его понимания и надо отталкиваться.

Отсюда мы можем сформулировать определение типа Span и связанного с ним readonly типа ReadOnlySpan:

> Span - это тип данных, обеспечивающий единый интерфейс работы с разнородными типами массивов данных а также возможность передать в другой метод подмножество этого массива таким образом чтобы вне зависимости от глубины взятия контекста скорость доступа к исходному массиву была константной и максимально высокой.

И действительно: если мы имеем примерно такой код:

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

то скорость доступа к исходному буферу будет максимально высокой: вы работаете не с managed объектом, а с managed указателем. Т.е. не с .NET managed типом, а с unsafe типом, заключенным в managed оболочку.

### Span\<T> на примерах

Человек так устроен что зачастую пока он не получит определенного опыта, то конечного понимания, для чего необходим инструмент часто не приходит. А потому, поскольку нам нужен некий опыт, давайте обратимся к примерам.

#### ValueStringBuilder

Одним из самых алогитмически интересных примеров является тип `ValueStringBuilder`, который прикопан где-то в недрах `mscorlib` и почему-то как и многие другие интереснейшие типы данных помечен модификатором `internal`, что означает что если бы не исследование исходного кода mscorlib, о таком замечательном способе оптимизации мы бы никогда не узнали.

Каков основной минус системного типа StringBuilder? Это конечно же его суть: как он сам, так и то, на чем он основан (а это массив символов `char[]`) - являются типами ссылочными. А это значит как минимум две вещи: мы все равно (хоть и немного) нагружаем кучу и второе - увеличиваем шансы промаха по кэшам процессора.

Еще один вопрос, который у меня возникал к StringBuilder - это формирование маленьких строк. Т.е. когда результирующая строка "зуб даю" будет короткой: например, менее 100 символов. Когда мы имеем достаточно короткие форматирования, к производительности возникают вопросы:

```csharp
    $"{x} is in range [{min};{max}]"
```

Насколько эта запись хуже чем ручное формирование через StringBuilder? Ответ далеко не всегда очевиден: все сильно зависит от места формирования: как часто будет вызван данный метод. Ведь сначала `string.Format` выделяет память под внутренний `StringBuilder`, который создаст массив символов (SourceString.Length + args.Length * 8) и если в процессе формирования массива выяснится, что длина не была угадана, то для формирования продолжения будет создан еще один `StringBuilder`, формируя тем самым односвязный список. А в результате - необходимо будет вернуть сформированную строку: а это еще одно копирование. Транжирство и расточительство. Вот если бы избавиться от размещения в куче первого массива формируемой строки, было бы замечательно: от одной проблемы мы бы точно избавились.

Взглянем на тип из недр `mscorlib`:

**Класс ValueStringBuilder**
[/src/mscorlib/shared/System/Text/ValueStringBuilder](https://github.com/dotnet/coreclr/blob/efebb38f3c18425c57f94ff910a50e038d13c848/src/mscorlib/shared/System/Text/ValueStringBuilder.cs)

```csharp
    internal ref struct ValueStringBuilder
    {
        // это поле будет активно если у нас слишком много символов
        private char[] _arrayToReturnToPool;
        // это поле будет основным
        private Span<char> _chars;
        private int _pos;
        // тип принимает буфер извне, делигируя выбор его размера вызывающей стороне
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

        // Получение строки - копирование символов из массива в массив
        public override string ToString()
        {
            var s = new string(_chars.Slice(0, _pos));
            Clear();
            return s;
        }

        // Вставка в середину сопровождается развиганием символов
        // исходной строки чтобы вставить необходимый: путем копирования
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

        // Если исходного массива, переданного конструктором не хватило
        // мы выделяем массив из пула свободных необходимого размера
        // На самом деле идеально было бы если бы алгоритм дополнительно создавал
        // дискретность в размерах массивов чтобы пул не был бы фрагментированным
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

        // Пропущенные методы: с ними и так все ясно
        private void AppendSlow(string s);
        public bool TryCopyTo(Span<char> destination, out int charsWritten);
        public void Append(string s);
        public void Append(char c, int count);
        public unsafe void Append(char* value, int length);
        public Span<char> AppendSpan(int length);
    }
```

Этот класс по своему функционалу сходен со своим старшим собратом `StringBuilder`, обладая при этом одной интересной и очень важной особенностью: он является значимым  типом. Т.е. хранится и передается целиком по значению. А новейший модификатор типа `ref`, который приписан к сигнатуре объявления типа говорит нам о том что данный тип обладает дополнительным ограничением: он имеет право находиться только на стеке. Т.е. вывод его экмепляров в поля классов приведет к ошибке. К чему все эти приседания? Для ответа на этот вопрос достаточно посмотреть на класс `StringBuilder`, суть котоого мы только что описали:

**Класс StringBuilder** [/src/mscorlib/src/System/Text/StringBuilder.cs](https://github.com/dotnet/coreclr/blob/68f72dd2587c3365a9fe74d1991f93612c3bc62a/src/mscorlib/src/System/Text/StringBuilder.cs#L47-L62)

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

StringBuilder - это класс, внутри которого находится ссылка на массив символов. Т.е. когда вы создаете его то по сути создается как минимум два объекта: сам StringBuilder и массив символов в как минимум 16 символов (кстати именно поэтому так важно задавать предполагаемую длину строки: ее построение будет идти через генерацию односвязного списка 16-символьных массивов. Согласитесь, расточительство). Что это значит в контексте нашего разговора о типе ValueStringBuilder: capacity по-умолчанию отсутствует, т.к. он заимствует память извне плюс он сам является значимым типом и заставляет пользователя расположить буфер для символов на стеке. Как итог весь экземпляр типа ложится на стек вместе с его содержимым и вопрос оптимизации здесь становится решенным. Нет выделения памяти в куче? Нет проблем с проседанием производительности по куче. Но вы мне скажите: почему тогда не пользоваться ValueStringBuilder (или его самописной версией: сам он internal и нам не доступен) всегда? Ответ такой: надо смотреть на задачу, которая вами решается. Будет ли результирующая строка известного размера? Будет ли она иметь некий известный максимум по длине? Если ответ "да" и если при этом размер строки не выходит за некоторые разумные границы, то можно использовать значимую версию StringBuilder. Иначе, если мы ожидаем длинные строки, переходим на использование обычной версии.

#### ValueListBuilder

Второй тип данный, который хочется особенно - отметить - это тип `ValueListBuilder`. Создан он для ситуаций, когда необходимо на короткое время создать коллекцию элементов и тут же отдать ее в обработку. Причем, такую обработку, когда после вызова некоторого метода коллекция эта более не будет необходима.

Согласитесь: задача очень похожа на задачу `ValueStringBuilder`. Да и решена она очень похожим образом:

**Файл [ValueListBuilder.cs](https://github.com/dotnet/coreclr/blob/dbaf2957387c5290a680c8918779683194137b1d/src/System.Private.CoreLib/shared/System/Collections/Generic/ValueListBuilder.cs)**

### Правила и практика использования

  - Единый буфер в LOH с выдачей его частей

### Как работает Span

Дополнительно хотелось бы поговорить о том, как работает Span и что в нем такого примечательного. А поговорить есть о чем: сам тип данных делится на две версии: для .NET Core 2.0+ и для всех остальных.

**Файл [Span.Fast.cs, .NET Core 2.0](https://github.com/dotnet/coreclr/blob/38403e661a4202ca4c8a72e4bbd9a263bddeb891/src/System.Private.CoreLib/shared/System/Span.Fast.cs)**

```csharp
public readonly ref partial struct Span<T>
{
    /// Ссылка на объект .NET или чистый указатель
    internal readonly ByReference<T> _pointer;
    /// Длина буфера данных по указателю
    private readonly int _length;
    // ...
}
```

**Файл **

```csharp
public ref readonly struct Span<T>
{
    private readonly System.Pinnable<T> _pinnable;
    private readonly IntPtr _byteOffset;
    private readonly int _length;
    // ...
}
```

Все дело в том что _большой_ .NET Framework и .NET Core 1.* не имеют специальным образом измененного сборщика мусора (в отличии от версии .NET Core 2.0+) и потому вынуждены тащить за собой дополнительный указатель: на начало буфера, с которым идет работа. Т.е. если в рамках `Span` мы говорили о том, что он создает некий контекст (подмножество исходного буфера данных), то здесь . 

## Memory\<T> и ReadOnlyMemory\<T>

### MemoryManager\<T>, MemoryHandle

```csharp
private sealed class MemoryFileStreamCompletionSource : FileStreamCompletionSource
{
    private MemoryHandle _handle; // mutable struct; do not make this readonly

    internal MemoryFileStreamCompletionSource(FileStream stream, int numBufferedBytes, ReadOnlyMemory<byte> memory) :
        base(stream, numBufferedBytes, bytes: null) // this type handles the pinning, so null is passed for bytes
    {
        _handle = memory.Pin();
    }

    internal override void ReleaseNativeResource()
    {
        _handle.Dispose();
        base.ReleaseNativeResource();
    }
}
```

### Memory\<T> на примерах



### Правила и практика использования


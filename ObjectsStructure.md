# Структура объектов в памяти

До сих, говоря про разницу между значимыми и ссылочными типами, мы затрагивали эту тему с высоты конечного разработчика. Т.е. мы не смотрели на то как они в реальности устроены на уровне CLR, как сделаны те или иные механики внутри каждого из них. Мы смотрели фактически на конечный результат. Однако, чтобы понимать суть вещей глубже и чтобы отбросить в сторону последние оставшиеся мысли о какой-либо магии, происходящей внутри CLR стоит заглянуть в самые ее потроха.

## Внутренняя структура экземпляров типов

Если говорить о классах как о типах данных, то в разговоре об их типах данных достаточно вспомнить их базовое устройство. Давайте начнем с типа `object`, который является базовым типом и формирует структуру для всех ссылочных типов:

**System.Object**:
```

  ----------------------------------------------
  |  SyncBlkIndx |    VMTPtr    |     Data     |
  ----------------------------------------------
  |  4 / 8 байт  |  4 / 8 байт  |  4 / 8 байт  |
  ----------------------------------------------
  |      -1      |  0xXXXXXXXX  |      0       |
  ----------------------------------------------

  Sum size = 12 (x86) .. 24 (x64)
```

Т.е. размер фактически зависит от конечной платформы, на которой быдет работать приложение. 

Теперь чтобы получить дальнейшее понимание, с чем мы имеем дело давайте проследуем по указателю `VMTPtr`. Для всей системы типов этот указатель является самым главным: именно через него работает и наследование, и реализация интерфейсов и приведение типов и много чего еще. Этот указатель - отсылка в систему типов .NET CLR.

### Virtual Methods Table

 Описание самой таблицы доступно по адресу в [GitHub CoreCLR](https://github.com/dotnet/coreclr/blob/master/src/vm/methodtable.h) и если отбросить все лишнее (а там 4381 строка! Парни из CoreCLR team не из пугливых), [выглядит она следующим образом](https://github.com/dotnet/coreclr/blob/master/src/vm/methodtable.h#L4099-L4114):

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

 Согласитесь, выглядит пугающе. Причем пугающе не в том что тут всего 6 полей (а где все остальные?), а в том что для того чтобы до них добаться, нам надо было пропустить 4,100 строк логики. Но давайте не будем унывать и попытаемся сразу получить из этого выгоду: мы пока что понятия не имеем что имеется ввиду под другими полями, зато поле `m_BaseSize` выглядит заманчиво. Как подсказывает нам комментарий, это - фактический размер для экземпляра типа. Попробуем в бою?

 Чтобы получить адрес VMT мы можем пойти двумя путями: либо зайти со сложного конца, получив адрес объекта, а значит и VMT (этот код уже был на страницах этой книги, но не ругайте меня: я не хочу чтобы вы его искали):

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

 Но есть путь и проще: тот же самый адрес возвращается вполне себе .NET FCL API: 

```csharp
    var vmt = typeof(string).TypeHandle.Value;
```


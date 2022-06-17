## Введение

Эта книга задумана мной как максимально полное описание работы .NET CLR, и частично - .NET Framework и призвана в первую очередь заставить посмотреть читателя на его внутреннюю структуру под несколько другим углом: не так, как это делается обычно. Связано это в первую очередь с утверждением, которое может показаться многим очень спорным: любой разработчик обязан пройти школу C/C++. Почему? Да потому что из высокоуровневых эти языки наиболее близки к процессору, и программируя на них начинаешь чувствовать работу программы сильнее. Однако, понимая, что мир устроен несколько иначе и у нас зачастую нет никакого времени изучать то, чем мы не будем напрямую пользоваться, я и решил написать эту книгу, в которой объяснение всех вопросов идет с более глубокой чем обычно - позиции и с более сложными или же попросту альтернативными примерами. Которые, помимо своей стандартной миссии - на самом простом коде показать как работает тот или иной функционал, сделать реверанс в альтернативную реальность, показав что все сильно сложнее чем может показаться изначально. Зачем? Чтобы и у вас возникло чувство понимания работы CLR до последнего винтика

# Содержание

1. **Часть 1. Память**
    1. **Раздел 1.** Введение в управление памятью
        1. [Общие слова](./Memory/01-Introduction/01-00-Introduction-Intro.md)
        1. [Введение в управление памятью](./Memory/01-Introduction/01-01-Introduction-MemoryManagement-Basics.md#введение-в-управление-памятью)
            1. [Пара слов перед стартом](./Memory/01-Introduction/01-01-Introduction-MemoryManagement-Basics.md#управление-памятьюwide)
            1. [Введение в управление памятью](./Memory/01-Introduction/01-01-Introduction-MemoryManagement-Basics.md#введение-в-управление-памятью)
            1. [Возможные классификации памяти исходя из логики](./Memory/01-Introduction/01-01-Introduction-MemoryManagement-Basics.md#как-можно-классифицировать-память)
            1. [Как это работает у нас. Обоснование выбора архитекторов](./Memory/01-Introduction/01-01-Introduction-MemoryManagement-Basics.md#как-это-работает-у-нас-обоснование-выбора-архитекторов)
            1. [Выводы](./Memory01-Introduction/01-01-Introduction-MemoryManagement-Basics.md#выводы)
        1. [Стек потока](./Memory/02-MemoryManagement-Basics/02-01-MemoryManagement-ThreadStack.md)
            1. [Базовая структура, платформа x86](./Memory/02-MemoryManagement-Basics/02-01-MemoryManagement-ThreadStack.md#базовые-сведения-про-платформы-x64-amd64-in-progress)
            1. [Немного про исключения на платформе x86](./Memory/02-MemoryManagement-Basics/02-01-MemoryManagement-ThreadStack.md#исключения-на-платформах-x64-amd64-in-progress)
            1. [Совсем немного про несовершенство стека потока](./Memory/02-MemoryManagement-Basics/02-01-MemoryManagement-ThreadStack.md#совсем-немного-про-несовершенство-стека-потока)
            1. [Большой пример: клонирование потока на платформе х86](./Memory//02-MemoryManagement-Basics/02-01-MemoryManagement-ThreadStack.md#большой-пример-клонирование-потока-на-платформе-х86)
        1. [Время жизни сущности](./Memory/02-MemoryManagement-Basics/02-02-MemoryManagement-EntitiesLifetime.md)
            1. [Ссылочные типы](./Memory/02-MemoryManagement-Basics/02-02-MemoryManagement-EntitiesLifetime.md#время-жизни-ссылочных-типов)
                1. [Общий обзор](./Memory/02-MemoryManagement-Basics/02-02-MemoryManagement-EntitiesLifetime.md#общий-обзор)
                1. [В защиту текущего подхода](./Memory/02-MemoryManagement-Basics/02-02-MemoryManagement-EntitiesLifetime.md#в-защиту-текущего-подхода-платформы-net)
                1. [Предварительные выводы](./Memory/02-MemoryManagement-Basics/02-02-MemoryManagement-EntitiesLifetime.md#предварительные-выводы)
        1. [RefTypes, ValueTypes, Boxing & Unboxing](./Memory/02-MemoryManagement-Basics/02-03-MemoryManagement-RefVsValueTypes.md)
            1. [Ссылочные и значимые типы данных](./Memory/02-MemoryManagement-Basics/02-03-MemoryManagement-RefVsValueTypes.md#ссылочные-и-значимые-типы-данных)
            1. [Копирование](./Memory/02-MemoryManagement-Basics/02-03-MemoryManagement-RefVsValueTypes.md#копирование)
            1. [Переопределяемые методы и наследование](./Memory/02-MemoryManagement-Basics/02-03-MemoryManagement-RefVsValueTypes.md#переопределяемые-методы-и-наследование)
            1. [Поведение при вызове экземплярных методов](/Memory/02-MemoryManagement-Basics/02-03-MemoryManagement-RefVsValueTypes.md#поведение-при-вызове-экземплярных-методов)
            1. [Возможность указать положение элементов](/Memory/02-MemoryManagement-Basics/02-03-MemoryManagement-RefVsValueTypes.md#возможность-указать-положение-элементов)
            1. [Разница в аллокации](/Memory/02-MemoryManagement-Basics/02-03-MemoryManagement-RefVsValueTypes.md#разница-в-аллокации)
            1. [Особенности выбора между class/struct](/Memory/02-MemoryManagement-Basics/02-03-MemoryManagement-RefVsValueTypes.md#особенности-выбора-между-classstruct)
            1. [Базовый тип - Object и возможность реализации интерфейсов. Boxing](/Memory/02-MemoryManagement-Basics/02-03-MemoryManagement-RefVsValueTypes.md#базовый-тип----object-и-возможность-реализации-интерфейсов-boxing)
            1. [Nullable&lt;T&gt;](./Memory/02-MemoryManagement-Basics/02-03-MemoryManagement-RefVsValueTypes.md#nullablet)
            1. [Погружаемся в boxing ещё глубже](./Memory/02-MemoryManagement-Basics/02-03-MemoryManagement-RefVsValueTypes.md#погружаемся-в-boxing-ещё-глубже)
            1. [Что если хочется лично посмотреть как работает boxing?](./Memory/02-MemoryManagement-Basics/02-03-MemoryManagement-RefVsValueTypes.md#что-если-хочется-лично-посмотреть-как-работает-boxing)
            1. [Почему .NET CLR не делает пуллинга для боксинга самостоятельно?](./Memory/02-MemoryManagement-Basics/02-03-MemoryManagement-RefVsValueTypes.md#почему-net-clr-не-делает-пуллинга-для-боксинга-самостоятельно)
            1. [Почему при вызове метода, принимающего тип object, а по факту - значимый тип нет возможности сделать boxing на стеке, разгрузив кучу?](./Memory/02-MemoryManagement-Basics/02-03-MemoryManagement-RefVsValueTypes.md#почему-при-вызове-метода-принимающего-тип-object-а-по-факту----значимый-тип-нет-возможности-сделать-boxing-на-стеке-разгрузив-кучу)
            1. [Почему нельзя использовать в качестве поля Value Type его самого?](./Memory/02-MemoryManagement-Basics/02-03-MemoryManagement-RefVsValueTypes.md#почему-нельзя-использовать-в-качестве-поля-value-type-его-самого)
        1. [Шаблон Disposable](./Memory/02-MemoryManagement-Basics/02-04-MemoryManagement-IDisposable.md)
            1. [IDisposable](./Memory/02-MemoryManagement-Basics/02-04-MemoryManagement-IDisposable.md#idisposable)
            1. [Вариации реализации IDisposable](./Memory/02-MemoryManagement-Basics/02-04-MemoryManagement-IDisposable.md#вариации-реализации-idisposable)
            1. [SafeHandle / CriticalHandle / SafeBuffer / производные](./Memory/02-MemoryManagement-Basics/02-04-MemoryManagement-IDisposable.md#safehandle--criticalhandle--safebuffer--производные)
                1. [Срабатывание finalizer во время работы экземплярных методов](./Memory/02-MemoryManagement-Basics/02-04-MemoryManagement-IDisposable.md#срабатывание-finalizer-во-время-работы-экземплярных-методов)
            1. [Многопоточность](./Memory/02-MemoryManagement-Basics/02-04-MemoryManagement-IDisposable.md#многопоточность)
            1. [Два уровня Disposable Design Principle](./Memory/02-04-MemoryManagement-IDisposable.md#два-уровня-disposable-design-principle)
            1. [Как ещё используется Dispose](./Memory/02-MemoryManagement-Basics/02-04-MemoryManagement-IDisposable.md#как-ещё-используется-dispose)
                1. [Делегаты, events](./Memory/02-MemoryManagement-Basics/02-04-MemoryManagement-IDisposable.md#делегаты-events)
                1. [Лямбды, замыкания](./Memory/02-MemoryManagement-Basics/02-04-MemoryManagement-IDisposable.md#лямбды-замыкания)
            1. [Защита от ThreadAbort](./Memory/02-MemoryManagement-Basics/02-04-MemoryManagement-IDisposable.md#защита-от-threadabort)
            1. [Итоги](./Memory/02-MemoryManagement-Basics/02-04-MemoryManagement-IDisposable.md#итоги)
        1. [Финализация](./Memory/02-MemoryManagement-Basics/02-05-MemoryManagement-Finalizer.md)
        1. [Выводы](./Memory/02-MemoryManagement-Basics/02-08-MemoryManagement-Results.md)
    1. **Раздел 2.** Практическая
        1. [Memory, Span](./Memory/02-MemoryManagement-Basics/02-06-MemoryManagement-MemorySpan.md)
            1. [Span&lt;T&gt;, ReadOnlySpan&lt;T&gt;](./Memory/02-MemoryManagement-Basics/02-06-MemoryManagement-MemorySpan.md#spant-readonlyspant)
                1. [Span&lt;T&gt; на примерах](./Memory/02-MemoryManagement-Basics/02-06-MemoryManagement-MemorySpan.md#spant-на-примерах)
                1. [Правила и практика использования](/Memory/02-MemoryManagement-Basics/02-06-MemoryManagement-MemorySpan.md#правила-и-практика-использования)
                1. [Как работает Span](./Memory/02-MemoryManagement-Basics/02-06-MemoryManagement-MemorySpan.md#как-работает-span)
                1. [Span&lt;T&gt; как возвращаемое значение](/Memory/02-MemoryManagement-Basics/02-06-MemoryManagement-MemorySpan.md#spant-как-возвращаемое-значение)
            1. [Memory&lt;T&gt; и ReadOnlyMemory&lt;T&gt;](./Memory/02-MemoryManagement-Basics/02-06-MemoryManagement-MemorySpan.md#memoryt-и-readonlymemoryt)
                1. [Memory&lt;T&gt;.Span](/Memory/02-MemoryManagement-Basics/02-06-MemoryManagement-MemorySpan.md#memorytspan)
                1. [Memory&lt;T&gt;.Pin](/Memory/02-MemoryManagement-Basics/02-06-MemoryManagement-MemorySpan.md#memorytpin)
                1. [MemoryManager, IMemoryOwner, MemoryPool](./Memory/02-MemoryManagement-Basics/02-06-MemoryManagement-MemorySpan.md#memorymanager-imemoryowner-memorypool)
            1. [Производительность](./Memory/02-MemoryManagement-Basics/02-06-MemoryManagement-MemorySpan.md#производительность)
    1. **Раздел 3.** Подробности реализации GC
        1. [Выделение памяти под объект](./Memory/02-MemoryManagement-Basics/02-07-MemoryManagement-Allocation.md) *(Только наговорен текст)*
        1. [Введение в сборку мусора](./Memory/03-MemoryManagement-Advanced/03-01-MemoryManagement-GC-Intro.md) *(Только наговорен текст)*
        1. [Фаза маркировки](./Memory/03-MemoryManagement-Advanced/03-02-MemoryManagement-GC-Mark-Phase.md) *(Только наговорен текст)*
        1. [Фаза планирования](./Memory/03-MemoryManagement-Advanced/03-03-MemoryManagement-GC-Planning-Phase.md) *(Только наговорен текст)*
        1. [Фазы Sweep/Collect](./Memory/03-MemoryManagement-Advanced/03-04-MemoryManagement-GC-Sweep-Collect.md) *(Только наговорен текст)*
        1. [Выводы по менеджменту памяти и работе над производительностью](./Memory/03-MemoryManagement-Advanced/03-05-MemoryMenegement-GC-Results.md) *(Только наговорен текст)*
    1. **Раздел 4.** Структура объектов в памяти
        1. [Структура объектов в памяти](./Memory/03-MemoryManagement-Advanced/03-06-ObjectsStructure.md)
    1. **Раздел 5.** Вне порядка повествования
        1. [Шаблон Lifetime](./Memory/04-OptimisationPractice-Daily/04-05-Lifetime.md)
1. **Часть 2. Поток исполнения команд**
      1. Раздел 1. Потоки
          1. [Введение в потоки](./Execution/01-Threads/01-01-Threads-Introduction.md)
          1. [Планирование потоков](./Execution/01-Threads/01-02-Threads-Scheduler.md)
          1. [Thread и ThreadPool](./Execution/01-Threads/01-03-Threads-Thread-ThreadPool.md)
          1. [SynchronizationContext](./Execution/01-Threads/01-04-Threads-SynchronizationContext.md)
      1. Раздел 2. Исключительные ситуации
          1. [Введение в исключительные ситуации](./Execution/2-ExceptionalFlow/1-Exceptions-Intro.md)
          1. [Архитектура исключительной ситуации](./Execution/2-ExceptionalFlow/2-Exceptions-Architecture.md)
          1. [События об исключительных ситуациях](./Execution/2-ExceptionalFlow/3-Exceptions-Events.md)
          1. [Виды исключительных ситуаций](./Execution/2-ExceptionalFlow/4-Exceptions-Types.md)

# Лицензия

Находится в файле [LICENSE](../../LICENSE)

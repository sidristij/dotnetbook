![](../../bin/BookCover-ru.png)

# Содержание

1. Часть 1. Память
    1. Раздел 1. Введение в управление памятью
        1. [Общие слова](./Memory/01-00-MemoryManagement-Intro.md)
        1. [Введение в управление памятью](./Memory/01-02-MemoryManagement-Basics.md)
            1. [Пара слов перед стартом](./Memory/01-02-MemoryManagement-Basics.md#пара-слов-перед-стартом)
            1. [Введение в управление памятью](./Memory/01-02-MemoryManagement-Basics.md#введение-в-управление-памятью)
            1. [Возможные классификации памяти исходя из логики](./Memory/01-02-MemoryManagement-Basics.md#возможные-классификации-памяти-исходя-из-логики)
            1. [Как это работает у нас. Обоснование выбора архитекторов](./Memory/01-02-MemoryManagement-Basics.md#как-это-работает-у-нас-обоснование-выбора-архитекторов)
            1. [Как это работает у нас](./Memory/01-02-MemoryManagement-Basics.md#как-это-работает-у-нас)
        1. [Стек потока](./Memory/01-04-MemoryManagement-ThreadStack.md)
        1. [Время жизни сущности](./Memory/01-06-MemoryManagement-EntitiesLifetime.md)
        1. [RefTypes, ValueTypes, Boxing & Unboxing](./Memory/01-08-MemoryManagement-RefVsValueTypes.md)
        1. [Шаблон Disposable](./Memory/01-10-MemoryManagement-IDisposable.md)
        1. [Финализация](./Memory/01-12-MemoryManagement-Finalizer.md)
        1. [Выводы](./Memory/01-14-MemoryManagement-Results.md)
    1. Раздел 2. Практическая
        1. [Memory, Span](./Memory/02-02-MemoryManagement-MemorySpan.md)
    1. Раздел 3. Подробности реализации GC
        1. [Выделение памяти под объект](./Memory/03-02-MemoryManagement-Allocation.md) *(Только наговорен текст)*
        1. [Введение в сборку мусора](./Memory/03-04-MemoryManagement-GC-Intro.md) *(Только наговорен текст)*
        1. [Фаза маркировки](./Memory/03-06-MemoryManagement-GC-Mark-Phase.md) *(Только наговорен текст)*
        1. [Фаза планирования](./Memory/03-08-MemoryManagement-GC-Planning-Phase.md) *(Только наговорен текст)*
        1. [Фазы Sweep/Collect](./Memory/03-10-MemoryManagement-GC-Sweep-Collect.md) *(Только наговорен текст)*
        1. [Выводы по менеджменту памяти и работе над производительностью](./Memory/03-12-MemoryMenegement-GC-Results.md) *(Только наговорен текст)*
    1. Раздел 4. Структура объектов в памяти
        1. [Структура объектов в памяти](./Memory/QQ-ObjectsStructure.md)
    1. Раздел 5. Вне порядка повествования
        1. [Шаблон Lifetime](./Memory/2-Basics/4-LifetimeManagement/3-Lifetime.md)
1. Часть 2. Поток исполнения команд
      1. Раздел 1. Домены приложений
          1. [Введение в домены приложений](./Execution/A-AppDomains/1-AppDomains-Intro.md)
          1. [Изоляция](./Execution/A-AppDomains/2-AppDomains-Isolation.md)
      1. Раздел 2. Исключительные ситуации
          1. [Введение в исключительные ситуации](./Execution/2-ExceptionalFlow/1-Exceptions-Intro.md)
          1. [Архитектура исключительной ситуации](./Execution/2-ExceptionalFlow/2-Exceptions-Architecture.md)
          1. [События об исключительных ситуациях](./Execution/2-ExceptionalFlow/3-Exceptions-Events.md)
          1. [Виды исключительных ситуаций](./Execution/2-ExceptionalFlow/4-Exceptions-Types.md)

# Лицензия

Находится в файле [LICENSE](../../LICENSE)

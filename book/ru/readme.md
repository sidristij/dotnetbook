![](../../bin/BookCover-ru.png)

# Содержание

  1. Common Language Runtime
  2. Основы менеджмента памяти: пользовательский слой
      1. [Heap basics](./MemoryManagementBasics.md)
      2. [Стек потока](./ThreadStack.md)
      3. [RefTypes, ValueTypes, Boxing & Unboxing](./ReferenceTypesVsValueTypes.md)
      4. [Memory, Span](./MemorySpan.md)
      5. [Структура объектов в памяти](./ObjectsStructure.md)
      6. Small Objects Heap
      7. Large Objects Heap
      8. Garbage Collection
      9. Statics
  3. Слой управления памятью: как работает CLR
      1. Подробно про Small Objects Heap
          1. Пример: дамп памяти, влияние pinned objects на аллокацию
      2. Large Objects Heap
          1. Пример: как легко испортить кучу, как этого избегать
      3. Garbage Collection
          1. Mark & Sweep
          2. Оптимизация поколений
          3. Финализация
          4. Проблемы, связанные с GC и финализацией
          5. [Шаблон Disposable (Disposable Design Principle)](./LifetimeManagement/2-Disposable.md)
          5. [Шаблон Lifetime](./LifetimeManagement/3-Lifetime.md)
  4. Поток исполнения команд
      1. Домены приложений
          1. [Введение в домены приложений](./AppDomains/1-AppDomains-Intro.md)
          2. [Изоляция](./AppDomains/2-AppDomains-Isolation.md)
          3. [Модель безопасности](./AppDomains/3-AppDomains-Security.md)
      2. Исключительные ситуации
          1. [Введение в исключительные ситуации](./ExceptionalFlow/1-Exceptions-Intro.md)
          2. [Архитектура исключительной ситуации](./ExceptionalFlow/2-Exceptions-Architecture.md)
          3. [События об исключительных ситуациях](./ExceptionalFlow/3-Exceptions-Events.md)
          4. [Виды исключительных ситуаций](./ExceptionalFlow/4-Exceptions-Types.md)

# Лицензия

Находится в файле [LICENSE](../../LICENSE)
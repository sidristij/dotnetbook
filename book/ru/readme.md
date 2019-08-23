![](../../bin/BookCover-ru.png)

# Содержание

  1. Часть 1. Память
      1. [Общие слова](./Memory/0-MemoryManagementBasics.md)
      1. Введение в управление памятью (SOH, LOH, GC)
          1. [Введение](./Memory/1-Introduction/1-MemoryManagement-Intro.md)
      1. Основные типы CLR, время жизни экземпляра
          1. [RefTypes, ValueTypes, Boxing & Unboxing](./Memory/2-Basics/1-ReferenceTypesVsValueTypes.md)
          1. [Структура объектов в памяти](./Memory/2-Basics/2-ObjectsStructure.md)
          1. [Memory, Span](./Memory/2-Basics/3-MemorySpan.md)
          1. Время жизни экземпляра
              1. [Время жизни сущностей](./Memory/2-Basics/4-LifetimeManagement/1-EntitiesLifetime.md)
              1. [Шаблон Disposable (Disposable Design Principle)](./Memory/2-Basics/4-LifetimeManagement/2-Disposable.md)
              1. [Шаблон Lifetime](./Memory/2-Basics/4-LifetimeManagement/3-Lifetime.md)
      1. Стек потока
          1. [Введение](./Memory/3-StackMemoryArea/1-ThreadStack.md)
      1. Управление памятью
          1. [Выделение памяти под объект](./Memory/4-ReferenceTypesManagement/1-MemoryManagement-Allocation.md)
          1. [Введение в сборку мусора](./Memory/4-ReferenceTypesManagement/2-MemoryManagement-GC-Intro.md)
          1. [Фаза маркировки](./Memory/4-ReferenceTypesManagement/3-MemoryManagement-GC-Mark-Phase.md)
          1. [Фаза планирования](./Memory/4-ReferenceTypesManagement/4-MemoryManagement-GC-Planning-Phase.md)
          1. [Фазы Sweep/Collect](./Memory/4-ReferenceTypesManagement/5-MemoryManagement-GC-Sweep-Collect.md)
      1. [Выводы по менеджменту памяти и работе над производительностью](./Memory/5-Results.md)
  1. Поток исполнения команд
      1. Домены приложений
          1. [Введение в домены приложений](./Execution/A-AppDomains/1-AppDomains-Intro.md)
          1. [Изоляция](./Execution/A-AppDomains/2-AppDomains-Isolation.md)
      1. Исключительные ситуации
          1. [Введение в исключительные ситуации](./Execution/2-ExceptionalFlow/1-Exceptions-Intro.md)
          1. [Архитектура исключительной ситуации](./Execution/2-ExceptionalFlow/2-Exceptions-Architecture.md)
          1. [События об исключительных ситуациях](./Execution/2-ExceptionalFlow/3-Exceptions-Events.md)
          1. [Виды исключительных ситуаций](./Execution/2-ExceptionalFlow/4-Exceptions-Types.md)

# Лицензия

Находится в файле [LICENSE](../../LICENSE)
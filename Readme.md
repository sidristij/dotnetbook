![CLR Book](./bin/BookCover.png)

# О книге (rus)

Эта книга задумана мной как максимально полное описание работы .NET CLR, и частично - .NET Framework и призвана в первую очередь заставить посмотреть читателя на его внутреннюю структуру под несколько другим углом: не так, как это делается обычно. Связано это в первую очередь с утверждением, которое может показаться многим очень спорным: любой разработчик обязан пройти школу C/C++. Почему? Да потому что из высокоуровневых эти языки наиболее близки к процессору, и программируя на них начинаешь чувствовать работу программы сильнее. Однако, понимая, что мир устроен несколько иначе и у нас зачастую нет никакого времени изучать то, чем мы не будем напрямую пользоваться, я и решил написать эту книгу, в которой объяснение всех вопросов идет с более глубокой чем обычно - позиции и с более сложными или же попросту альтернативными примерами. Которые, помимо своей стандартной миссии - на самом простом коде показать как работает тот или иной функционал, сделать реверанс в альтернативную реальность, показав что все сильно сложнее чем может показаться изначально. Зачем? Чтобы и у вас возникло чувство понимания работы CLR до последнего винтика.

**Если вы хотите показать, что книга вам нравится или выразить благодарность, ставьте звезду проекту, делайте fork и создавайте Pull Requests!**

*автор, Станислав Сидристый*

# Table of contents

  1. Common Language Runtime
  2. Memory management basics: user layer
 1. Heap basics
      1. Thread stack
      2. [RefTypes, ValueTypes, Boxing & Unboxing](./en/ReferenceTypesVsValueTypes.md)
      3. Memory, Span
      4. Types and objects structure
      5. Small Objects Heap
      6. Large Objects Heap
      7. Garbage Collection
      8. Statics
  3. Memory management layer: how CLR work
      1. Small Objects Heap detailed
          1. Sample: getting memory dump, pinned objects
      2. Large Objects Heap
          1. Sample: getting slow heap and how to avoid this
      3. Garbage Collection
          1. Mark & Sweep
          2. Optimizations
          3. Finalization
          4. [IDisposable: Disposable Design Principle](./en/LifetimeManagement/2-Disposable.md)
  4. Commands flow
      1. Application Domains
          1. Introduction
          2. Isolation
          3. Security model
      2. Exceptional situations
          1. Introduction to exceptional situations
          2. Architecture
          3. Exceptions events
          4. Types of exceptional situations

# Содержание

  1. Common Language Runtime
  2. Основы менеджмента памяти: пользовательский слой
      1. [Heap basics](./ru/MemoryManagementBasics.md)
      2. [Стек потока](./ru/ThreadStack.md)
      3. [RefTypes, ValueTypes, Boxing & Unboxing](./ru/ReferenceTypesVsValueTypes.md)
      4. [Memory, Span](./ru/MemorySpan.md)
      5. [Структура объектов в памяти](./ru/ObjectsStructure.md)
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
          5. [Шаблон Disposable (Disposable Design Principle)](./ru/LifetimeManagement/2-Disposable.md)
          5. [Шаблон Lifetime](./ru/LifetimeManagement/3-Lifetime.md)
  4. Поток исполнения команд
      1. Домены приложений
          1. [Введение в домены приложений](./ru/AppDomains/1-AppDomains-Intro.md)
          2. [Изоляция](./ru/AppDomains/2-AppDomains-Isolation.md)
          3. [Модель безопасности](./ru/AppDomains/3-AppDomains-Security.md)
      2. Исключительные ситуации
          1. [Введение в исключительные ситуации](./ru/ExceptionalFlow/1-Exceptions-Intro.md)
          2. [Архитектура исключительной ситуации](./ru/ExceptionalFlow/2-Exceptions-Architecture.md)
          3. [События об исключительных ситуациях](./ru/ExceptionalFlow/3-Exceptions-Events.md)
          4. [Виды исключительных ситуаций](./ru/ExceptionalFlow/4-Exceptions-Types.md)

# Лицензия

Находится в файле [LICENSE](LICENSE)

# Благодарности

Благодарю всех, кто внес вклад как в виде хороших комментариев так и в виде правок ошибок в словах и грамматике. С вами книга становится более удобной и легкой для прочтения.

Также благодарю всех тех, кто делал pull requests на github а также давал хорошие комментарии и советы - на habrahabr.ru, где производилась первичная вычитка некоторых текстов.
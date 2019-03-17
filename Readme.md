Cover | From Author, Stanislav Sidristij
-------|-----
[![CLR Book](./bin/BookCover.png)](./en/readme.md) | This book is intended by me as the fullest possible description of the work of the .NET CLR, and partially - the .NET Framework, and is intended primarily to make the reader look at its internal structure from a slightly different angle: not in the way it is usually done. This is due primarily to the statement, which may seem very controversial to many: any developer must go through C / C ++ school. Why? Yes, because of the higher-level languages ​​these languages ​​are closest to the processor, and programming in them you begin to feel the work of the program stronger. However, realizing that the world is somewhat different and we often have no time to study what we will not use directly, I decided to write this book, in which the explanation of all questions comes from a deeper than usual position and with more complex or simply alternative examples. Which, in addition to their standard mission - on the simplest code, show how this or that functionality works, make a curtsy into an alternate reality, showing that everything is much more complicated than it may initially seem. What for? So that you have a sense of understanding the work of the CLR to the last screw
[![CLR Book](./bin/BookCover-ru.png)](./ru/readme.md) | Эта книга задумана мной как максимально полное описание работы .NET CLR, и частично - .NET Framework и призвана в первую очередь заставить посмотреть читателя на его внутреннюю структуру под несколько другим углом: не так, как это делается обычно. Связано это в первую очередь с утверждением, которое может показаться многим очень спорным: любой разработчик обязан пройти школу C/C++. Почему? Да потому что из высокоуровневых эти языки наиболее близки к процессору, и программируя на них начинаешь чувствовать работу программы сильнее. Однако, понимая, что мир устроен несколько иначе и у нас зачастую нет никакого времени изучать то, чем мы не будем напрямую пользоваться, я и решил написать эту книгу, в которой объяснение всех вопросов идет с более глубокой чем обычно - позиции и с более сложными или же попросту альтернативными примерами. Которые, помимо своей стандартной миссии - на самом простом коде показать как работает тот или иной функционал, сделать реверанс в альтернативную реальность, показав что все сильно сложнее чем может показаться изначально. Зачем? Чтобы и у вас возникло чувство понимания работы CLR до последнего винтика
[![CLR Book](./bin/BookCover-ch.png)](./ch/readme.md) | 本书是我对.NET CLR工作的最全面描述，部分是.NET Framework，主要是为了让读者从一个稍微不同的角度看待它的内部结构：不是通常的方式。这主要是因为这个声明对许多人来说似乎很有争议：任何开发人员都必须通过C / C ++学校。为什么呢？是的，由于更高级别的语言，这些语言最接近处理器，并且在其中编程，您开始感觉到程序的工作更强大。然而，我意识到这个世界有些不同，而且我们经常没有时间研究我们不会直接使用的东西，所以我决定写这本书，其中所有问题的解释来自比平时更深的位置，而且更复杂或者只是替代的例子。除了他们的标准任务之外 - 在最简单的代码上，展示这个或那个功能是如何工作的，对一个替代现实做出一种屈膝的态度，表明一切都比最初看起来要复杂得多。为什么呢？这样你就能理解CLR到最后一个螺丝的工作
GitHubMarkdownisntsupportwidth|

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
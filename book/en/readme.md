![](../../bin/BookCover.png)

# Table of contents

  1. Common Language Runtime
  2. Memory management basics: user layer
 1. Heap basics
      1. Thread stack
      2. [RefTypes, ValueTypes, Boxing & Unboxing](./ReferenceTypesVsValueTypes.md)
      3. [Memory, Span](./MemorySpan.md)
      4. [Types and objects structure](./ObjectsStructure.md)
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
          4. [IDisposable: Disposable Design Principle](./LifetimeManagement/2-Disposable.md)
  4. Commands flow
      1. Application Domains
          1. Introduction
          2. Isolation
          3. Security model
      2. Exceptional situations
          1. [Introduction to exceptional situations](./ExceptionalFlow/1-Exceptions-Intro.md)
          2. [Architecture](./ExceptionalFlow/2-Exceptions-Architecture.md)
          3. [Exceptions events](./ExceptionalFlow/3-Exceptions-Events.md)
          4. [Types of exceptional situations](./ExceptionalFlow/4-Exceptions-Types.md)

# License

You can find here: [LICENSE](../../LICENSE)
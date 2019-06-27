![](../../bin/BookCover-ch.png)


# 目录

  1. 公共语言运行时（Common Language Runtime）
  2. 内存管理基础：用户层
      1. 堆（Heap）基础
      2. 线程栈
      3. [引用类型，值类型，装箱&拆箱](./ReferenceTypesVsValueTypes.md)
      4. [Memory, Span](./MemorySpan.md)
      5. Types and objects structure
      6. Small Objects Heap
      7. Large Objects Heap
      8. Garbage Collection
      9. Statics
  3. 内存管理层: CLR如何工作
      1. Small Objects Heap detailed
          1. Sample: getting memory dump, pinned objects
      2. Large Objects Heap
          1. Sample: getting slow heap and how to avoid this
      3. Garbage Collection
          1. Mark & Sweep
          2. Optimizations
          3. Finalization
          4. [IDisposable: Disposable设计原则](./LifetimeManagement/2-Disposable.md)
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

# License

You can find here: [LICENSE](../../LICENSE)
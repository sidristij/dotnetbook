## The architecture of an exceptional situation

> [A link to the discussion](https://github.com/sidristij/dotnetbook/issues/51)

I guess one of the most important issues in this topic is building an exception handling architecture in your application. This is interesting for many reasons. And the main reason, I think, is an apparent simplicity, which you don’t always know what to do with. All the basic constructs such as `IEnumerable`, `IDisposable`, `IObservable`, etc. have this property and use it everywhere. On the one hand, their simplicity tempts to use these constructs in different situations. On the other hand, they are full of traps which you might not get out. It is possible that looking at the amount of information we will cover you’ve got a question: what is so special about exceptional situations?

However, to make conclusions about building the architecture of exception classes we should learn some details about their classification. Because before building a system of types that would be clear for the user of code, a programmer should determine when to choose the type of error and when to catch or skip exceptions. So, let’s classify the exceptional situations (not the types of exceptions) based on various features.

### Based on a theoretical possibility to catch a future exception.

Based on this feature we can divide exceptions into those that will be definitely caught and those that highly likely won’t be caught. Why do I say *highly likely*? Because there always be someone who will try to catch an exception while this is unnecessary.

First, let’s describe the first group of exceptions – those that should be caught.

In case of such exceptions we, on the one hand, say to our subsystem that we came to a state when there is no point in further actions with our data. On the other hand, we mean that nothing disastrous happened and we can find the way out of the situation by simply catching the exception. This property is very important as it defines the criticality of an error and gives confidence that if we catch an exception and clear resources, we can simply proceed with the code.

The second group deals with exceptions that, although it may sound strange, don’t have to be caught. They can be used only for error logging, but not for correcting a situation. The simplest example is `ArgumentException` and `NullReferenceException`. In fact, in an ordinary situation you don’t need to catch, for example, `ArgumentNullException` because in this case the source of an error is exactly you. If you catch such an exception, you admit that you made an error and passed something unacceptable to a method:

```csharp
void SomeMethod(object argument)
{
    try {
        AnotherMethod(argument);
    } catch (ArgumentNullException exception)
    {
        // Log it
    }
}
```

In this method we try to catch `ArgumentNullException`. But I think this is strange as passing right arguments to a method is entirely our concern. Reacting after the event would be incorrect: the best thing you can do in such a situation is to check the passed data in advance before calling a method or even to build such code where getting wrong parameters is impossible.

Another group of exceptional situations is fatal errors. If some cache is faulty and the work of a subsystem is anyway incorrect, then it is a fatal error and the nearest code on the stack will not catch it for sure: 

```csharp
T GetFromCacheOrCalculate()
{
    try {
        if(_cache.TryGetValue(Key, out var result))
        {
            return result;
        } else {
            T res = Strategy(Key);
            _cache[Key] = res;
            return res;
        }
    } cache (CacheCorruptedException exception)
    {
        RecreateCache();
        return GetFromCacheOrCalculate();
    }
}
```

`CacheCorruptedException` is an exception meaning that "hard drive cache is inconsistent". Then, if the cause of such an error is fatal for the cache subsystem (for example there are no cache file access rights), the following code can’t recreate cache using `RecreateCache` instruction and therefore catching this exception is an error itself.

### Based on the area where an exceptional situation is actually catched

Another issue is whether we should catch some exceptions or pass them to somebody who understands the situation better. In other words, we should establish areas of responsibility. Let’s examine the following code:

```csharp

namespace JetFinance.Strategies
{
    public class WildStrategy : StrategyBase
    {
        private Random random =  new Random();

        public void PlayRussianRoulette()
        {
            if(DateTime.Now.Second == (random.Next() % 60))
            {
                throw new StrategyException();
            }
        }
    }

    public class StrategyException : Exception { /* .. */ }
}

namespace JetFinance.Investments
{
    public class WildInvestment
    {
        WildStrategy _strategy;

        public WildInvestment(WildStrategy strategy)
        {
            _strategy = strategy;
        }

        public void DoSomethingWild()
        {
            ?try?
            {
                _strategy.PlayRussianRoulette();
            }
            catch(StrategyException exception)
            {
            }
        }
    }
}

using JetFinance.Strategies;
using JetFinance.Investments;

void Main()
{
    var foo = new WildStrategy();
    var boo = new WildInvestment(foo);

    ?try?
    {
        boo.DoSomethingWild();
    }
    catch(StrategyException exception)
    {
    }
}

```

Which of the two strategies is more appropriate? The area of responsibility is very important. Initially, it may seem that the work and consistency of `WildInvestment` fully depend on `WildStrategy`. Thus, if `WildInvestment` simply ignores this exception, it will go to the upper level and we shouldn’t do anything. However, note that in terms of architecture the `Main` method catches an exception from one level while calling the method from another. How does it look in terms of use? Well, that’s how it looks:

  - the responsibility for this exception was handed over to us;
  - the user of this class is not sure that this exception was previously passed through a set of methods on purpose;
  - we start to create new dependencies which we got rid of by calling an intermediate layer.

However, there is another conclusion resulting from this one: we should use `catch` in the `DoSomethingWild` method. And this is slightly strange for us: `WildInvestment` is sort of hardly dependent on something. I mean if `PlayRussianRoulette` didn’t work, the same will happen to `DoSomethingWild`: it doesn’t have return codes, but it has to play the roulette. So, what we can do in such a seemingly hopeless situation? The answer is actually simple: being on another level `DoSomethingWild` should throw its own exception that belongs to this level and wrap it in `InnerException` as the original source of a problem: 

```csharp

namespace JetFinance.Strategies
{
    pubilc class WildStrategy
    {
        private Random random =  new Random();

        public void PlayRussianRoulette()
        {
            if(DateTime.Now.Second == (random.Next() % 60))
            {
                throw new StrategyException();
            }
        }
    }

    public class StrategyException : Exception { /* .. */ }
}

namespace JetFinance.Investments
{
    public class WildInvestment
    {
        WildStrategy _strategy;

        public WildInvestment(WildStrategy strategy)
        {
            _strategy = strategy;
        }

        public void DoSomethingWild()
        {
            try
            {
                _strategy.PlayRussianRoulette();
            }
            catch(StrategyException exception)
            {
                throw new FailedInvestmentException("Oops", exception);
            }
        }
    }

    public class InvestmentException : Exception { /* .. */ }

    public class FailedInvestmentException : Exception { /* .. */ }
}

using JetFinance.Investments;

void Main()
{
    var foo = new WildStrategy();
    var boo = new WildInvestment(foo);

    try
    {
        boo.DoSomethingWild();
    }
    catch(FailedInvestmentException exception)
    {
    }
}
```

By wrapping one exception in another we transfer the problem form one level of application to another and make its work more predictable in terms of a consumer of this class: the `Main` method.

### Based on reuse issues

Often we feel too lazy to create a new type of exception but when we decide to do it, it is not always clear which type to base on. But it is precisely these decisions that define the whole architecture of exceptional situations. Let’s have a look at some popular solutions and make some conclusions.

When choosing the type of exception we can use a previously made solution, i.e. to find an exception with the name that contains similar sense and use it. For example, if we got an entity via a parameter and we don’t like this entity, we can throw `InvalidArgumentException`, indicating the cause of an error in Message. This scenario looks good especially since `InvalidArgumentException` is in the group of exceptions that may not be caught. However, the choice of `InvalidDataException` will be wrong if you work with some data types. It is because this type is in `System.IO` area, which is probably isn’t what you deal with. Thus, it will almost always be wrong to search for an existing type instead of developing one by yourself. There are almost no exceptions for a general range of tasks. Virtually all of them are for specific situations and if you reuse them in other cases, it will gravely violate the architecture of exceptional situations. Moreover, an exception of a particular type (for example, `System.IO.InvalidDataException`) can confuse a user: on the one hand, he will see that exception belongs to the `System.IO` namespace, while on the other hand it is thrown from a completely different namespace. If this user starts thinking about the rules of throwing this exception, he may go to [referencesource.microsoft.com](https://referencesource.microsoft.com/) and find [all the places where it is thrown](https://referencesource.microsoft.com/#System/sys/System/IO/compression/InvalidDataException.cs,2b389f14fb01ad1b,references):

  - `internal class System.IO.Compression.Inflater`

The user will understand that ~~somebody is all thumbs~~ this type of exception confused him as the method that threw this exception didn’t deal with compression.

Also, in terms of reuse, you can simply create one exception and declare the `ErrorCode` field in it. That seems like a good idea. You just throw the same exception, setting the code, and use just one `catch` to deal with exceptions, increasing the stability of an application, nothing more. However, I believe you should rethink this position. Of course, this approach makes life easier on the one hand. However, on the other hand, you dismiss the possibility to catch a subgroup of exceptions that have some common feature. For example, `ArgumentException` that unites a bunch of exceptions by inheritance. Another serious disadvantage is an excessively large and unreadable code that must arrange error code based filtering. However, introducing an encompassing type with an error code will be more appropriate when a user doesn’t have to care about specifying an error.

```csharp
public class ParserException
{
    public ParserError ErrorCode { get; }

    public ParserException(ParserError errorCode)
    {
        ErrorCode = errorCode;
    }

    public override string Message
    {
        get {
            return Resources.GetResource($"{nameof(ParserException)}{Enum.GetName(typeof(ParserError), ErrorCode)}");
        }
    }
}

public enum ParserError
{
    MissingModifier,
    MissingBracket,
    // ...
}

// Usage
throw new ParserException(ParserError.MissingModifier);
```

The code that protects the parser call doesn’t care why parsing failed: it is interested in the error as such. However, if the cause of fail becomes important after all, a user can always get the error code from the `ErrorCode` property. And you really don’t have to search for necessary words in a substring of `Message`.

If we don’t choose to reuse, we can create a type of exception for every situation. On the one hand, it sounds logical: one type of error – one type of exception. However, don’t overdo: having too many types of exceptions will cause the problem of catching them as the code of a calling method will be overloaded with `catch` blocks. Because it needs to process all types of exceptions that you want to pass to it. Another disadvantage is purely architectural. If you don’t use exceptions, you confuse those who will use these exceptions: they may have many things in common but will be caught separately.

However, there are great scenarios to introduce separate types for specific situations. For example, when the error affects not a whole entity, but a specific method. Then this error type should take such a place in the hierarchy of inheritance that no one would ever think to catch it together with something else: for example, through a separate branch of inheritance.

Also, if you combine both of these approaches, you can get a powerful set of instruments to work with a group of errors: you can introduce a common abstract type and inherit specific cases from it. The base class (our common type) must get an abstract property, designed to store an error code while inheritors will specify this code by overriding this property.

```csharp
public abstract class ParserException
{
    public abstract ParserError ErrorCode { get; }

    public override string Message
    {
        get {
            return Resources.GetResource($"{nameof(ParserException)}{Enum.GetName(typeof(ParserError), ErrorCode)}");
        }
    }
}

public enum ParserError
{
    MissingModifier,
    MissingBracket
}

public class MissingModifierParserException : ParserException
{
    public override ParserError ErrorCode { get; } => ParserError.MissingModifier;
}

public class MissingBracketParserException : ParserException
{
    public override ParserError ErrorCode { get; } => ParserError.MissingBracket;
}

// Usage
throw new MissingModifierParserException(ParserError.MissingModifier);
```

Using this approach we get some wonderful properties:

  - on the one hand, we keep catching exceptions using a base (common) type;
  - on the other hand, even catching exceptions with this base type we are still able to identify a specific situation;
  - plus, we can catch exceptions via a specific type instead of a base type without using the flat structure of classes.

I think it is very convenient.

### Based on belonging to a specific group of behavioral situations

What conclusions can we make based on the previous reasoning? Let’s try to define them.

First of all, let’s decide what means a situation? Usually, we talk about classes and objects in terms of entities with some internal state and we can perform actions on these entities. Thus, the first type of behavioral situation includes actions on some entity. Next, if we look at an object graph from the outside we will see that it is logically represented as a combination of functional groups: the first group deals with caching, the second works with databases, the third performs mathematical calculations. Different layers can go through all these groups, e.g. layers of internal states logging, process logging, and method calls’ tracing. Layers can encompass several functional groups. For example, there can be a layer of a model, a layer of controllers and a presentation layer. These groups can be in one assembly or in different ones, but each group can create its own exceptional situations.

So, we can build a hierarchy for the types of exceptional situations based on the belonging of these types to one or another group or layer. Thus, we allow a catching code to easily navigate among these types in the hierarchy.

Let’s examine the following code:

```csharp

namespace JetFinance
{
    namespace FinancialPipe
    {
        namespace Services
        {
            namespace XmlParserService
            {
            }

            namespace JsonCompilerService
            {
            }

            namespace TransactionalPostman
            {
            }
        }
    }

    namespace Accounting
    {
        /* ... */
    }
}

```

What’s it like? I think the namespace is a perfect way to naturally group the types of exceptions based on the behavioral situations: everything that belongs to particular groups should stay there, including exceptions. Moreover, when you get a particular exception, you will see the name of its type and also its namespace that will specify a group it belongs to. Do you remember the bad reuse of `InvalidDataException` which is actually defined in the `System.IO` namespace? The fact that it belongs to this namespace means this type of exception can be thrown from classes that are in the `System.IO` namespace or in a more nested one. But the actual exception was thrown from a completely different space, confusing a person that handles the issue. However, if you put the types of exceptions and the types that throw these exceptions in the same namespaces you keep the architecture of types consistent and make it easier for developers to understand the reasons for what happens.

What is the second way for grouping on the level of code? Inheritance:

```csharp

public abstract class LoggerExceptionBase : Exception
{
    protected LoggerException(..);
}

public class IOLoggerException : LoggerExceptionBase
{
    internal IOLoggerException(..);
}

public class ConfigLoggerException : LoggerExceptionBase
{
    internal ConfigLoggerException(..);
}

```

Note that for usual application entities, they inherit both behavior and data and group types that belong to a *single group of entities*. However, for exceptions, they inherit and are grouped based on a *single group of situations*, because the essence of an exception is not an entity but a problem.

Combining these two grouping methods we can make the following conclusions: 

  - there should be a base type of exceptions inside `Assembly` that will be thrown by this assembly. This type of exceptions should be in a root namespace of the assembly. This will be the first layer of grouping.
  - further, there can be one or several namespaces inside an assembly. Each of them divides the assembly into functional zones, defining the groups of situations, that appear in this assembly. These may be zones of controllers, database entities, data processing algorithms, etc. For us, these namespaces mean grouping types based on their function. However, in terms of exceptions they are grouped based on problems within the same assembly;
  - exceptions must be inherited from types in the same upper-level namespace. This ensures that end user will unambiguously understand situations and won’t catch *wrong* type based exceptions. Admit, it would be strange to catch `global::Finiki.Logistics.OhMyException` by `catch(global::Legacy.LoggerExeption exception)`, while the following code looks absolutely appropriate:

```csharp
namespace JetFinance.FinancialPipe
{
    namespace Services.XmlParserService
    {
        public class XmlParserServiceException : FinancialPipeExceptionBase
        {
            // ..
        }

        public class Parser
        {
            public void Parse(string input)
            {
                // ..
            }
        }
    }

    public abstract class FinancialPipeExceptionBase : Exception
    {

    }
}

using JetFinance.FinancialPipe;
using JetFinance.FinancialPipe.Services.XmlParserService;

var parser = new Parser();

try {
    parser.Parse();
}
catch (XmlParserServiceException exception)
{
    // Something is wrong in the parser
}
catch (FinancialPipeExceptionBase exception)
{
    // Something else is wrong. Looks critical because we don't know the real reason
}

```

Here, the user code calls a library method that, as we know, can throw `XmlParserServiceException` in some situation. And, as we know, this exception refers to the inherited namespace `JetFinance.FinancialPipe.FinancialPipeExceptionBase`, which means that there may be some other exceptions — this time `XmlParserService` microservice creates only one exception but other exceptions may appear in future. As we have a convention for creating types of exceptions, we know what entity this new exception will be inherited from and put an encompassing `catch` in advance. That enables us to skip all things irrelevant to us.

How to build such a hierarchy of types?

  - First of all, we should create a base class for a domain. Let’s call it a domain base class. In this case, a domain is a word that encompasses a number of assemblies, combining them based on some feature: logging, business-logic, UI. I mean functional zones of an application that are as large as possible.
  - Next, we should introduce an additional base class for exceptions which must be caught: all the exceptions that will be caught using the `catch` keyword will be inherited from this base class;
  - All the exceptions indicating fatal errors should be inherited directly from a domain base class. Thus we will separate them from those caught on the architecture level;
  – Divide the domain into functional areas based on namespaces and declare the base type of exceptions that will be thrown from each area. Here it is necessary to use common sense: if an application has a high degree of namespace nesting, you shouldn’t do a base type for each nesting level. However, if there is branching at a nesting level when one group of exceptions goes to one namespace and another group goes to another namespace, it is necessary to use two base types for each subgroup; 
  - Special exceptions should be inherited from the types of exceptions belonging to functional areas
  - If a group of special exceptions can be combined, it is necessary to do it in one more base type: thus you can catch them easier;
  - If you suppose the group will be more often caught using a base class, introduce Mixed Mode with ErrorCode.

### Based on the source of an error

The source of an error can be another basis to combine exceptions in a group. For example, if you design a class library, the following things can form groups of sources: 

  - unsafe code call with an error. This situation can be dealt with by wrapping an exception or an error code in its own type of exception while saving returned data (for example the original error code) in a public property of the exception;
  - a call of code by external dependencies, which has thrown exceptions that can’t be caught by our library as they are beyond its area of responsibility. This group can include exceptions from the methods of those entities that were accepted as the parameters of a current method or exceptions from the constructor of a class which method has called an external dependence. For example, a method of our class has called a method of another class, the instance of which was returned via parameters of another method. If an exception indicates that we are the source of a problem, we should generate our own exception while retaining the original one in `InnerExcepton`. However, if we understand that the problem has been caused by an external dependency we ignore this exception as belonging to a group of external dependencies beyond our control;
  - our own code that was accidentally put in an inconsistent state. A good example is text parsing — no external dependencies, no transfer to `unsafe` world, but a problem of parsing occurs.
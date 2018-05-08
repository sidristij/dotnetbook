## Состав и развертка блока обработки исключительных ситуаций

Если взглянуть на блок обработки исключительных ситуаций, то мы увидим всем привычную картину:

``` csharp
try {
    // 1
} catch (ArgumentsOutOfRangeException exception)
{
    // 2
} catch (IOException exception)
{
    // 3
} catch
{
    // 4
} finally {
    // 5
}
```

Т.е. существует некий участок кода от которого ожидается некоторое нарушение поведения. Причем не просто некоторое, а вполне конкретные ситуации. Однако, если заглянуть в результирующий код, то мы увидим что по факту эта самая конструкция, которая в C# выглядит как единое целое, в CLI на самом деле - отдельные блоки. Т.е. не существует возможности построить вот такую единую цепочку, однако есть возможность построить блоки `try-catch` и `try-finally`. И если переводить обратно в C#, то получим мы следующий код:

``` csharp
try {
    try {
        try {
            try {
                // 1
            } catch (ArgumentsOutOfRangeException exception)
            {
                // 2
            }
        } catch (IOException exception)
        {
            // 3
        }
    } catch
    {
        // 4
    }
} finally {
    // 5
}

// 6

```

Отлично. Однако выглядит все еще несколько искусственно. Ведь эти блоки - конструкции языка и не более того. Как они разворачиваются в конечном коде? На данном этапе я ограничусь псевдокодом, однако без лишних подробностей он прекрасно покажет во что _примерно_ разворачивается конструкция:

```csharp
GlobalHandlers.Push(BlockType.Finally, FinallyLabel);
GlobalHandlers.Push(BlockType.Catch, typeof(Exception), ExceptionCatchLabel);
GlobalHandlers.Push(BlockType.Catch, typeof(IOException), IOExceptionCatchLabel);
GlobalHandlers.Push(BlockType.Catch, typeof(ArgumentsOutOfRangeException), ArgumentsOutOfRangeExceptionCatchLabel);

// 1

GlobalHandlers.Pop(4);
FinallyLabel:

// 5

goto AfterTryBlockLabel;
ExceptionCatchLabel:
GlobalHandlers.Pop(4);

// 4

goto FinallyLabel;
IOExceptionCatchLabel:
GlobalHandlers.Pop(4);

// 3

goto FinallyLabel;
ArgumentsOutOfRangeExceptionCatchLabel:
GlobalHandlers.Pop(4);

// 2

goto FinallyLabel;
AfterTryBlockLabel:

// 6

return;
```

Последнее, о чем хотелось бы упомянуть во вводной части - это фильтры исключительных ситуаций. Для платформы .NET это новшеством не является, однако является таковым для разработчиков на языке программирования C#: фильтрация исключительных ситуаций появилась у нас только в шестой версии языка. Особенностью исполнения кода по уверениям многих источников является то, что код фильтрации происходит *до* того как произойдет развертка стека. Однако если посмотреть на результаты работы следующего кода:

```csharp
    class Program
    {
        static void Main()
        {
            try
            {
                ProxyRunner.Go();
            }
            catch (Exception ex) when (Check(ex))
            {
                ;
            }
        }

        static bool Check(Exception ex)
        {
            var domain = AppDomain.CurrentDomain.FriendlyName; // -> TestApp.exe
            return ex.Message == "1";
        }

        public class ProxyRunner : MarshalByRefObject
        {
            private void MethodInsideAppDomain()
            {
                throw new Exception("1");
            }

            public static void Go()
            {
                var dom = AppDomain.CreateDomain("PseudoIsolated", null, new AppDomainSetup
                {
                    ApplicationBase = AppDomain.CurrentDomain.BaseDirectory
                });
                var proxy = (ProxyRunner) dom.CreateInstanceAndUnwrap(typeof(ProxyRunner).Assembly.FullName, typeof(ProxyRunner).FullName);
                proxy.MethodInsideAppDomain();
            }
        }
    }

```

То станет ясно что она происходит еще до того как мы попадаем в фильтр. Взглянем на скриншоты. Первый взят до того как генерируется исключение:

![StackUnroll](.\imgs\ExceptionalFlow\StackUnroll.png)

А второй - после:

![StackUnroll2](.\imgs\ExceptionalFlow\StackUnroll2.png)

Суть примера становится ясным если взглянуть на трассировку вызовов до и после попадания в фильтр исключений. Мало того что произошла развертка стека, мы еще и увидели повторное попадание в `Main`. Это в некоторой степени путает заставляя думать о том что никакой развертки нет, особенно на простых примерах. Однако на более сложном примере видно что она существует.
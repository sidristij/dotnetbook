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

Последнее, о чем хотелось бы упомянуть во вводной части - это фильтры исключительных ситуаций. Для платформы .NET это новшеством не является, однако является таковым для разработчиков на языке программирования C#: фильтрация исключительных ситуаций появилась у нас только в шестой версии языка. Особенностью исполнения кода по уверениям многих источников является то, что код фильтрации происходит *до* того как произойдет развертка стека. Эту ситуацию можно наблюдать в ситуациях, когда между местом выброса исключения и местом проверки на фильтрацию нет никаких других вызовов кроме обычных:

```csharp
static void Main()
{
    try
    {
        Foo();
    }
    catch (Exception ex) when (Check(ex))
    {
        ;
    }
}

static void Foo()
{
    Boo();
}

static void Boo()
{
    throw new Exception("1");
}

static bool Check(Exception ex)
{
    return ex.Message == "1";
}
```

![](./imgs/ExceptionalFlow/StackWOutUnrolling.png)

Как видно на изображении трассировка стека содержит не только первый вызов `Main` как место отлова исключительной ситуации, но и весь стек до точки выброса исключения плюс повторный вход в `Main` через некоторый неуправляемый код. Можно предположить что этот код и есть код выброса исключений, который просто находится в стадии фильтрации и выбора конечного обработчика если бы не одно _но_. Если посмотреть на результаты работы следующего кода (я добавил проброс вызова через границу между доменами приложения):

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

То станет ясно что она размотка стека в данном случае происходит еще до того как мы попадаем в фильтр. Взглянем на скриншоты. Первый взят до того как генерируется исключение:

![StackUnroll](./imgs/ExceptionalFlow/StackUnroll.png)

А второй - после:

![StackUnroll2](./imgs/ExceptionalFlow/StackUnroll2.png)

Изучим трассировку вызовов до и после попадания в фильтр исключений. Что же здесь происходит? Здесь мы видим что разработчики платформы сделали некоторую защиту дочернего домена. Трассировка обрезана по крайний метод в цепочке вызовов, после которого идет переход в другой домен. На самом деле это выглядит несколько странно.

Также стоит остановиться на том, для чего же были введены фильтры исключений. Давайте взглянем на пример и он как по мне будет лучше тысячи слов:

```csharp
try {
    UnmanagedApiWrapper.SomeMethod();
} catch (WrappedException ex) when (ex.ErrorCode == ErrorCodes.DeviceNotFound)
{
    // ...
} catch (WrappedException ex) when (ex.ErrorCode == ErrorCodes.ConnectionLost)
{
    // ...
} catch (WrappedException ex) when (ex.ErrorCode == ErrorCodes.TimeOut)
{
    // ...
} catch (WrappedException ex) when (ex.ErrorCode == ErrorCodes.Disconnected)
{
    // ...
}
```

Согласитесь, это выглядит интереснее чем один блок `catch` и `switch` внутри с `throw;` в `default` блоке. Это выглядит более разграниченным, более правильным с точки зрения разделения ответственности. Ведь исключение с кодом ошибки по своей сути - ошибка дизайна, а фильтрация - это выправка нарушения архитектуры, переводя в кконцепцию раздельных типов исключений.
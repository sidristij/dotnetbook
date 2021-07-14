# Практика отпимизаций по работе с памятью

В своей практике оптимизаций я сделал интересный, хотя и логичный вывод. Если мы избавлемся от срабатываний GC, приложение работает быстрее. Звучит как: "ну естествено! Кто бы спорил-то?". Однако, далеко не каждый ответит на вопрос: "на сколько"? 

Мой пример, конечно же, будет несколько наивен.. Но когда я работал над протоколом Samba и мной была переписана библиотека `SMBLibrary`, то результат при той же структуре кода, но другими способами по работе с памятью составил `-41%` к времени исполнения. Вдумайтесь: `41%` на аллокацию, GC и неоптимальный выбор структур данных (в т.ч. потому что массивы выделялись обычным способом, а можно -- пуллить).

Конечно же, основная масса времени и памяти уходила на работу со структурами данных типа `Dictionary`, `List`, `Queue`, т.к. их заполнение шло без выставленного `capacity`, однако и на траффик мелких объектов пользовательских типов данных уходило много времени.

Траффик объектов приводит, как мы помним, как минимум к GC0. Однако если траффик реально плотный, то приложение попросту не успеет освободить ссылки и объекты вместо ухода на покой в GC0 сделают это в GC1. К чему это приведет? К тому, что GC будет работать дольше. Т.е. ему придется проанализировать большой участок памяти для понимания, на какие объекты более нет ссылок. Плюс к этому, если на эти обекты будут ссылаться какие-то старые коллекции или старые объекты (из мира второго поколения), то в игру вступает механизм таблицы карт (подробнее о которой вы можете прочитать в труде Конрада Кососы .NET Memory Management). Что включает еще большие расходы на анализ. Звучит как-то не очень оптимально. Но что же делать? Попахивает-то безнадёгой: сущность ведь всё равно надо выделить. Причем не структуру, а именно класс. И память при этом выделяется и освобождается по полной. Однако, если вспомнить главу "Время жизни сущностей", то мы вспомним, что по сути время жизни любой сущности -- это несколько виртуальное понятие и скорее зависит от восприятия *человека*. Именно человека. Ведь по факту никакого выделения памяти не существует. Вместо этого есть разметка физической памяти под какой-то тип и понимание... человеком, что *вот с вот этого* момента объектом можно пользоваться. 

А потому мы легко можем сделать еще один слой виртуализации понятия времени жизни сущности. Например, так: 

```csharp
var instance = Heap<Foo>.Create();
instance.DanceDance();
Heap<Foo>.Return(instance)
```

Что произошло с нашим восприятием? Ассоциативным мышлением. Если раньше мы вызывали `new Foo()`, что для *нас* означало "создать *из ниоткуда* объект типа Foo", то теперь при вызове `Heap<Foo>.Create()` возникает новый конструкт: "создать объект в куче объектов типа Foo". И "вернуть объект типа Foo обратно в кучу". 

Согласитесь, интересно? Код программы может быть каким угодно. *Мы* наделяем его смыслом. Таким, каким хотим чтобы он воспринимался *правильно*. Ведь если назвать всё тоже самое по-другому:

```csharp
var instance = FreeObjectsQueue<Foo>.Dequeue();
FreeObjectsQueue<Foo>.Enqueue(instance)
```

Мы перестанем воспринимать это как кучу. Просто какая-то обёрнутая очередь. А если так:  

```csharp
var instance = UnfocusedObjects<Foo>.MakeFocus();
UnfocusedObjects<Foo>.RemoveFocus(instance)
```

То несмотря на то, что это -- совершенно то же самое с точки зрения функционала, пользоваться этим мы даже не станем.

Однако, вернёмся к нашему примеру, которым пользоваться хочется:

```csharp
var instance = Heap<Foo>.Create();
Heap<Foo>.Return(instance)
```

И подумаем, как мы можем его реализовать. На самом деле, когда я впервые написал эту реализацию, пришли даже некоторые сомнения, что я вообще имею право вот **это** называть Heap'ом:

```csharp
public static class Heap<T> where T : class, new()
{
    private ConcurrentQueue<T> objects;
    private const MinPoolSize = 32;

    static Heap()
    {
        objects = new ConcurrentQueue<T>(MinPoolSize);
    }

    public static T Create()
    {
        if(objects.TryDeqeue(out var instance))
        {
            return instance;
        }
        return new T();
    }

    public static void Return(T instance) => objects.Enqeue(instance);
}
```

Он очень прост: даже примитивен. Тут нет ни массива, ни его разметки на объекты... Нет тут и списка свободных участков, разделенных на секции исходя из размера... Тут ничего нет. Просто очередь. Станислав?? Вы это... Главу ради очереди пишете что-ли? *Бегающие глаза*. Ну в общем-то в каком-то смысле да. Вообще стек работает быстрее. А ещё быстрее с некоторыми хитростями. Но.. подождите закрывать главу! Скоро вы всё поймёте и полюбите этот небольшой набор строк. 

Изменилось ли ваше отношение к конструкции `var instance = Heap<Foo>.Create()`? Стало ли оно "ну и примитивно же"? Если да, то абсолютно напрасно! Всё, что мы тут видим -- это менеджмент объектов одного типа. Список занятых участков нам хранить нет смысла: объекты у пользователя. Зато мы имеем целый ряд преимуществ:

- Мы получим объект в любом случае: даже если объектов в пуле нет вообще;
- Если объекты в пуле есть, мы получим их: переиспользуем;
- Мы можем предварительно создать множество объектов и поместить их в пул рядом чтобы они:
  - легли плотно во втором поколении;
  - легли плотно, помещаясь всей группой в L1 кэше;
- Если вдруг... по какой-то причине мы не вернули объект в пул.. Ничего страшного: его соберёт GC (у читателей, пришедших с неуправляемых языков типа C++ тут возникат фантомные боли). А такие утечки легко отследить в `dotMemory`.

Исходя из последнего пункта можно сделать вывод, что переход на `Heap<T>.Create()` обойдётся достаточно легко: можно сначала вмето `new T()` везде написать `Heap<T>.Create()`, после чего первым шагом написать `Heap<T>.Return(instance)` там, где вы считаете, что это необходимо. А дальше -- запустить `dotMemory` и проверить наличие "утечек": траффика объектов вашего типа. После нахождения таких "утечек" решаем: они вообще важны? Влияют на картину? Если да, идём и убираем их, вставляя `Heap<T>.Return(instance)` куда надо. Например, в метод `Dispose()`.

[>]: Вот тоже забавное заблюдение. Слово "утечка" теперь будет значить не только незапланированное удержание некоторого объекта в памяти, но и незапланированный уход объекта в Garbage Collector.

## Пара слов про коллекции

Прежде чем начать погружаться более глубоко, хотелось бы пару слов сказать про коллекции. Основная прблема коллекций заключается в том, что основной траффик по памяти они создают не траффиком типа самой коллекции, а траффиком содержимого. Другими словами, основной траффик идёт не от типа `List<T>`, а от массивов `T[]`, из которых `List<T>` состоит. Решение, конечно же, будет состоять в замене на другие коллекции, но идентичные по API. Снаружи -- то же самое, а внутри -- совершенно другие алгоритмы.

## Вместо заключения к вводной

Задача оптимизации кода -- очень творческая и увлекательная. Эта задача увлекает настолько, что иной раз трудно оторваться и поставить точку. Всегда видишь "ещё один момент". Мало того, когда переходишь от оптимизаций к микрооптимизациям (от "не 500 мс, а 200 мс" до "не 100 мс, а 90 мс"), то начинаешь замечать, что даже они могут увеличивать кратность увеличения производительонсти.

Простой пример:
- Первая оптимизация сократила операцию с 500 мсек до 150 мсек (500/150= x3,33). Чувствуется как "много";
- Вторая -- сократила до 100 мсек (500/100 = x5). Ощущения - наверное, достаточно;
- Третья -- до 80 мсек (500/80 = x6,25). Оптимизоровать уже сложно и кажется, что делаем лишнюю работу. Но удивлены, что кратность выросла;
- Четвёртая -- до 65 мсек (500/65 = x7,69). Посмотрев "назад", есть ощущение, что если бы остановились на втором шаге, многое бы потеряли.

Увидев вторую, большую оптимизацию, можно было остановиться и на этом этапе. Ведь дальше если что и встретится, это будут такие маленькие числа в оптимизации по времени, что даже не стоит и задумываться. Однако всё сильно зависит от контекста. Иногда решение остановиться на втором этапе реально будет оправдано. А иногда - опрадано будет решение "идти до конца". Например, если задача сервиса - парсить некие документы, то оптимизировать "до упора" будет оправдано, если с учётом работы с сетью кратность быстродействия также будет возрастать, а не только кратность быстродействия библиотеки парсинга. Если сеть начинает проигрывать, тут можно думать (если опять же, это важно) про оптимизацию работы с сетью. Уход от REST API в пользу бинарного обмена. А иногда даже переход на UDP. Также стоит посмотреть, нагружаете ли вы канал данными до максимально-возможной планки? Может стоит разъединить парсинг данных и приём этих данных по сети? Принимать одним потоком, отдавать в другой или в два других - на парсинг? В общем вопросов - тьма. И на каждом этапе можно сделать быстрее.

Вообще говоря, когда я начал заниматься анализом быстродействия, то понял, что самое большое зло -- это популярные библиотеки. Они популярны очень часто просто потому, что возникли "когда-то давным давно". К ним привыкли, новые к внедрению не рассматриваются (например, потому что там нет каких-то фишек либо потому что другие производители их не поддерживают) и как результат - процент исполнения кода сокращается до 20% от общего.

Вы скажете: ну да уж конечно, враки всё. А я вам -- контрпример. Сервис запрос-ответ. Работает с Монгой. Время работы кода самого сервиса - 20%. Почему? С Монги достаются достаточно увесистые документы, парсятся, отдаются коду сервиса и обрабатываются. Библиотека парсинга протокола передачи Монги - формата `BSON` генерирует до 100 Мб траффика в секунду. Просто потому что написано криво. Плюс `Newtonsoft.JSON`. Плюс Сам драйвер монги имел неоптимальную работу с сетью. Как итог - по данным `dotMemory` только на GC Wait уходило чуть менее 50% времени. 10% - сеть, 20% код самого сервиса и 30% - код парсинга BSON, который порождал траффик для 40-50% GC Wait. Другими словами, код парсинга рабтал не 30%, а 70-80%% времени. Что стало причиной переделки этой библиотеки. Которая между прочим была частью официального драйвера MongoDB.    
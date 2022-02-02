# Управление памятью{.wide}

[>m]: ## Аудиокнига

   [](https://music.yandex.ru/album/9613103)

Когда в районе 2015-х я разговаривал с различными людьми и рассказывал, как работает Garbage Collector (для меня по началу это было большим и странным увлечением), то весь рассказ умещался максимум минут на 15. После чего, мне задавали один вопрос: «а зачем это знать? Ведь работает как-то и работает». После чего в голове начиналась путаница: с одной стороны я понимал, что они в большинстве случаев правы. И рассказывал про то самое меньшинство случаев, где эти знания прекрасно себя чувствуют и используются. Но поскольку таких случаев было всё-таки меньшинство, в глазах собеседника оставалось некоторое чувство недоверия.

На уровне тех знаний, которые нам давали раньше в немногочисленных источниках, люди, которых собеседуют на позицию разработчика обычно говорят: есть три поколения, пара хипов больших и малых объектов. Еще максимум можно услышать -- про наличие неких сегментов и таблицы карт. Но обычно дальше поколений и хипов люди не уходят. И все почему? Ведь **вовсе не потому**, что чего-то не знают, а потому, что действительно **не понятно, зачем это знать**. Ведь та информация, которая нам давалась раньше, выглядела как рекламный буклет к чему-то большому и закрытому. Ну знаем мы про три поколения, ну и что?.. Всё это, согласитесь, какое-то эфемерное.

Сейчас же, когда Microsoft открыли исходники, я ожидал нескольких бенефитов от этого. Первый бенефит -- это то, что сообщество накинется и начнет какие-то баги исправлять. И оно накинулось! И исправило все грамматические ошибки в комментариях. Много запятых исправлено и опечаток. Иногда даже написано, что, например, свойство `IsEnabled` возвращает признак того, что что-то включено. Можно даже подать на членство в .NET Foundation, опираясь на множество вот таких вот комментариев (и, понятное дело, не получить). Второй бенефит, который ожидался -- это предложение нового и полезного функционала в готовом виде. Этот бенефит, насколько я знаю, время от времени также срабатывает, но это очень редкие кейсы. Например, один разработчик очень ускорил получение символа по индексу в строке. Как выяснилось, ранее это работало не очень эффективно.

[>]: Членство в .NET Foundation сейчас зарабатывается плотной работой и интересными коммитами. Хорошее дело можно затеять, обратившись в код компилятора методов (JIT) и оптимизировать какой-либо пользовательский случай. Например, автоматическую векторизацию каких-то расчётов.

Наш рассказ про менеджмент памяти будет идти от общего к частному. Т.е. для начала мы посмотрим на алгоритмы с высоты птичьего полёта не сильно вдаваясь в подробности. Ведь если мы начнем сразу с подробностей, придётся делать отсылки к будущим главам, а оттуда -- обратно в ранние главы. Это крайне не удобно как для написания, так и в чтении. Напротив: сделав вводную мы поймем все основы. А потом -- начнем погружаться в детали.

## Введение в управление памятью

Мы пишем разные программы: консольные, сервисы, web-сервисы и другие. Все они работают примерно одинаково. Среди прочего есть очень важное отличие -- это стиль работы с памятью. Консольные приложения, скорее всего, работают в рамках базовой, выделенной при старте приложения, памяти. Такое приложение всю или частично ее использует и больше ничего не запросит: запустилось и вышло. Иногда речь идет о сервисах, которые долго работают, перерабатывают память постоянно. И делают это не по изолированным запросам, в отличие от сервисов ASP.NET и WCF (которые мы вызвали, из базы что-то достали и забыли). А именно как какой-то расчётный сервис: есть поток данных на вход, с которыми сервис работает (например, поток команд от `RabbitMQ`) и так может работать очень долго, выделяя и освобождая память. И это уже совершенно другой стиль расхода памяти: ведь в этом случае память необходимо контролировать, смотреть как она расходуется, течёт или не течёт.

А если это ASP.NET, то это уже третий способ управления памятью. Надо понимать, что нас вызвал внешний код, мы отработаем достаточно быстро и исчезнем. Отсюда, если мы во время запроса выделяем некоторую память, можно сделать всё так, чтобы не волноваться по поводу её освобождения: ведь метод завершит свою работу и все объекты потеряют свои корни: локальные переменные метода, обрабатывающего запрос. Но и расточительством заниматься не стоит: при высокой нагрузке вы получите траффик объектов. Возможно, часть объектов стоит сделать структурами?

Как же этим всем управлять? С точки зрения *разработки* Garbage Collector'а, с точки зрения *системы менеджмента памяти* у нас есть совершенно разные стили и мы должны в них идеально хорошо работать. У нас же может быть машина, на которой запустилось консольное приложение, а есть машина, на которой приложение отъедает 256 Гб. Эти системы помимо различия в объёме пожираемой памяти также отличаются по ряду других признаков: например, в стиле её выделения и освобождения путём обнуления ссылок, в плотности выделения и освобождения объектов определённого типа (например, одно приложение чаще работает со строками, а другое -- с массивами данных). И в зависимости от типа данных может так оказаться, что пойти другим путём в менеджменте памяти может оказаться лучше. Поэтому, перед тем, как думать, как реализовать некий менеджер памяти, стоит подумать над вопросом: как можно классифицировать эту память? А от классификации памяти танцевать в сторону оптимизации её выделения и освобождения в зависимости от того, с каким классом памяти мы в данный момент работаем. Давайте подойдём к вопросу как будто мы сами разрабатываем Garbage Collector: шаг за шагом мы сами придём к тем же выводам, к каким пришли разработчики Платформы.

## Как можно классифицировать память?

Как можно классифицировать память? Чисто интуитивно можно разделять выделяемые участки памяти исходя из размеров объекта, который выделяется. Например, понятно, что если мы говорим о больших структурах данных, то управлять ими надо совершенно по-другому, нежели маленькими: потому что они тяжелые и их трудно перемещать если возникнет такая надобность (например, чтобы снизить фрагментацию -- частый враг обычных программ, написанных на C/C++). А маленькие, соответственно, занимают мало места и из-за того, что они образуют группы, перемещать легко. Однако из-за того что их намного больше, ими тяжелее управлять с точки зрения менеджера памяти: ведь чтобы управлять ими, необходимо знать о положении в памяти каждого из них. А значит, для них без всякой статистики и так понятно, что должен быть какой-то другой подход.

Если разделять по времени жизни, то тут тоже возникают идеи. Например, если объекты короткоживущие, то, возможно, к ним надо чаще присматриваться, чтобы побыстрее от них избавляться (желательно, сразу, как только они стали не нужны). Тогда GC будет отрабатывать быстрее: если меньше работы, то быстрее отработаешь. Если объекты долгоживущие, то можно уже посмотреть на статистику. Например, можно пофантазировать и решить, что эту область памяти анализировать на предмет ненужных объектов можно намного реже: ведь если объект долгоживущий, проверять его "смерть" необходимо гораздо реже. А если смотреть редко, это сокращает время на сборку мусора в сумме, но увеличивает длительность работы каждого вызова GC: за это время может накопиться приличное количество умерших долгоживущих объектов. А это наводит на мысль, что долгоживущие объекты необходимо хранить отдельно от короткоживущих: тогда группу короткоживущих можно будет анализировать одними алгоритмами, а группу долгоживущих - другими.

Также можно попытаться классифицировать память по типу данных. Можно легко предположить, что все типы, которые отнаследованы от типа `Attribute` или в зоне `Reflection` (а в особенности `runtime` часть `reflection`, которая нам не доступна), будут жить вечно -- а потому с ними тоже можно работать по особенному.

[>]: Сразу оговорюсь, что `runtime` часть `reflection` живёт в собственных кучах, которые нам не доступны.

Ровно также к строкам, которые представляют собой массив символов может быть применён какой-то свой подход: строки могут часто повторяться. А это значит, можно предусмотреть некий механизм исключения дублей строк (strings interning).

Видов может быть сколько угодно много и в зависимости от классификаций может оказаться, что управление памятью для конкретной группы может быть более эффективно, если учитывать её особенности.

Когда создавали архитектуру нашего GC, то выбрали первые два вида классификаций: размер и время жизни (хотя, если присмотреться к делению типов на классы и структуры, то можно подумать, что классификации на самом деле три. Однако, различие свойств классов и структур можно свести к размеру и времени жизни). Давайте же обоснуем этот выбор.

## Как это работает у нас. Обоснование выбора архитекторов

Если мы с вами будем досконально разбираться, почему были выбраны именно эти два алгоритма управления памятью: *Sweep* (разметка освободившихся блоков для последующего переиспользования) и *Compact* (сжатие кучи с целью снижения фрагментации), нам для этого придётся рассматривать десятки алгоритмов управления памятью, которые существуют в мире: начиная обычными словарями, заканчивая очень сложными lock-free структурами. Вместо этого, оставив голову мыслям о полезном, мы просто *обоснуем* выбор и тем самым *поймём*, почему выбор был сделан именно таким. Мы более не смотрим в рекламный буклет ракеты-носителя: у нас на руках полный набор документации.

[>]: Я выбрал формат рассуждения чтобы вы почувствовали себя архитекторами платформы и сами пришли к тем же самым выводам, к каким пришли реальные архитекторы в штаб-квартире Microsoft в Рэдмонде.

Определимся с терминологией: менеджмент памяти -- это структура данных и ряд алгоритмов, которые позволяют "выделять" память и отдавать её внешнему потребителю и освобождать её, регистрируя как свободный участок. Т.е. если взять, например, какой-то массив байт (линейный кусок памяти), написать алгоритмы разметки массива на объекты .NET (запросили новый объект: мы подсчитали его размер, пометили у себя что этот вот кусок и есть новый объект, отдали указатель на объект внешней стороне) и алгоритмы освобождения памяти (когда нам говорят, что объект более не нужен, а потому память из-под него можно выдать кому-то другому), то получится именно менеджер памяти.

[>]: В .NET такое не возможно, зато вполне приычное дело в C++, когда для некоторых нужд переопределяются операторы `new` и `delete`. Пишутся дополнительные методы, которые арендуют у ОС большой отрезок виртуальной памяти и размечают его на объекты. После чего -- освобождают, опять выеляют... Делается это по разным причинам. Снижение фрагментации -- одна из них.

Исходя из классификации выделяемых объектов на основании их размера можно разделить места под выделение памяти на два больших раздела: на место с объектами размером ниже определенного порога и на место с размером выше этого порога и посмотреть, какую разницу можно внести в управление этими группами (исходя из их размера) и что из этого выйдет. Рассмотрим каждую категорию в отдельности.

Если рассматривать вопросы **управления условно** "*маленьких*" объектов, то можно заметить, что если придерживаться идеи сохранения информации о каждом объекте, нам будет очень дорого поддерживать структуры данных управления памятью, которые будут хранить в себе ссылки на каждый такой объект. В конечном счёте может оказаться, что для того, чтобы хранить информацию об одном объекте понадобится столько же памяти, сколько занимает сам объект. Вместо этого стоит подумать: если при сборке мусора мы будем **помечать** достижимые объекты обходом графа объектов (понять это легко, зная, откуда начинать обход графа), тогда для идентификации всех остальных можно воспользоваться обычным последовательным, линеёным проходом. Тогда так ли нам необходимо в алгоритмах менеджмента памяти хранить информацию о каждом объекте? Ответ очевиден: надобности в этом нет никакой. Ведь если мы будем размещать объекты друг за другом и при этом сделать возможным узнать размер каждого из них, сделать итератор кучи очень просто:

```csharp
void* current = memory_start;

while(current < memory_end)
{
    var size = current->typeInfo.size;
    current += size;
}
```

 А значит, можно попробовать исходить из того, что такую информацию мы хранить не должны: пройти кучу мы можем линейно, зная размер каждого объекта и смещая указатель каждый раз на размер очередного объекта.

> Дополнительные структуры данных, хранящие указатели на каждый из объектов, расположенных в куче, отсутствуют.

Однако, тем не менее, когда память нам более не нужна, мы должны её освобождать. Далее, после освобождения, наверняка захочется снова занять освобожденный участок. Искать его линейным прохожденим кучи долго и не эффективно. А потому для освободившихся участков можно воспользоваться идеей хранения списка освободившихся участков.

> В куче есть списки свободных участков памяти: набор указателей на их начала + размер.

Если, как мы решили, хранить информацию о свободных участках, и при этом при освобождении памяти из под объектов эти участки оказались слишком малы для размещения в них чего-либо полезного, то во-первых мы приходим к той-же проблеме хранения информации о свободных участках, с которой столкнулись при рассмотрении занятых: хранить информацию о таких малышах может оказаться слишком дорого. Это снова звучит расточительно, согласитесь: не всегда выпадает удача освобождения группы объектов, следующих друг за другом. Обычно они освобождаются в хаотичном порядке, образуя небольшие просветы свободной памяти, где сложно выделить что-либо ещё. Но всё-таки в отличии от занятых участков, которые нам нет надобности линейно искать, искать свободные участки нам необходимо потому что при выделении памяти они нам снова могут понадобиться. А потому возникает вполне естественное желание слишком маленькие участки не сохранять в список освобожденных, зато при достижении определенного уровня фрагментации уменьшить фрагментацию путём сжатия кучи: переместив все занятые участки на места свободных, образовав тем самым большую зону свободного участка, где можно совершенно спокойно выделять память.

> Отсюда рождается идея алгоритма сжатия кучи Compacting.

Но, подождите, скажите вы. Ведь эта операция может быть очень тяжёлой. Представьте только, что вы освободили объект в самом начале кучи. И что, скажете вы, надо двигать вообще всё?? Ну конечно, можно пофантазировать на тему векторных инструкций CPU, которыми можно воспользоваться для копирования огромного занятого участка памяти. Но это ведь только начало работы. Надо ещё исправить все указатели с полей объектов на объекты, которые подверглись передвижениям. Эта операция может занять дичайше длительное время. Нет, надо исходить из чего-то другого. Например, разделив весь отрезок памяти кучи на сектора и работать с ними по отдельности. Если работать отдельно в каждом секторе (для предсказуемости времени работы алгоритмов и масштабирования этой предсказмуемости -- желательно, фиксированных размеров), идея сжатия уже не кажется такой уж тяжёлой: достаточно сжать отдельно взятый сектор и тогда можно даже начать рассуждать о времени, которое необходимо для сжатия одного такого сектора.

Теперь осталось понять, на основании чего делить на сектора. Тут надо обратиться ко второй классификации, которая введена на платформе: разделение памяти, исходя из времени жизни отдельных её элементов.

Деление простое: если учесть, что выделять память мы будем по мере возрастания адресов, то первые выделенные объекты (в младших адресах) становятся самыми старыми, а те, что находятся в старших адресах -- самыми молодыми. Далее, со временем, после многочисленных выделений памяти, освобождений памяти и сжатия все объекты естественным образом будут прижаты к младшим адресам: они существуют давно. И по-прежнему в старших адресах будут нахдиться молодые объекты, которые были недавно созданы.Соответственно, время от времени сжимая кучу мы получаем ряд долгоживущих объектов -- в младших адресах и ряд короткоживущих -- в старших.

> Таким образом мы получили *поколения*.

Разделив память на поколения, мы получаем возможность реже заглядывать за сборкой мусора в объекты старшего поколения, которых становится всё больше и больше. И на самом деле статистика и здравый ум подсказывают, что если объект прережил пару срабатываний GC (при умеренном траффике объектов), то он будет существовать очень долго. Это даёт повод в старшее поколение заглядывать по редким причинам.

Но возникает еще один вопрос: если мы будем иметь всего два поколения, мы получим проблемы:

- Либо мы будем стараться, чтобы GC отрабатывал максимально быстро: тогда размер *младшего поколения мы будем стараться делать минимальных размеров*. Как результат -- недавно созданные объекты при вызове GC будут случайно уходить в старшее поколение (если GC сработал "прям вот сейчас, во время яростного выделения памяти под множество объектов"), хотя если бы он сработал чуть позже, они бы остались в младшем, где были бы собраны за короткие сроки.
- Либо, чтобы минимизировать такое случайное "проваливание", мы *увеличим размер младшего поколения*. Однако, в этом случае GC на младшем поколении будет работать достаточно долго, замедляя и подтормаживая тем самым всё приложение.

Выход -- введение "среднего" поколения. Подросткового. Суть его введения сводится к получению баланса между *получением минимального по размеру младшего поколения* и *максимально-стабильного старшего поколения*, где лучше ничего не трогать. Это -- зона, где судьба объектов еще не решена. Первое (не забываем, что мы считаем с нуля) поколение создается также небольшим, но чуть крупнее, чем младшее и потому GC туда заглядывает реже. Он тем самым дает возможность объектам, которые находятся во временном, "подростковом" поколении, не уйти в старшее поколение, которое собирать крайне тяжело.

> Так мы получили идею трёх поколений.

Следующий слой оптимизации -- попытка отказаться от сжатия. Ведь если его не делать, мы избавляемся от огромного пласта работы. Вернемся к вопросу свободных участков.

Если после того, как мы израсходовали всю доступную в куче память и был вызван GC, возникает естественное желание отказаться от сжатия в пользу дальнейшего выделения памяти внутри освободившихся участков, если их размер достаточен для размещения некоторого количества объектов. Тут мы приходим к идее второго алгоритма освобождения памяти в GC, который называется `Sweep`: память не сжимаем, для размещения новых объектов используем пустоты от освобожденных объектов.

> Так мы описали и обосновали все основы алгоритмов GC.

Далее спускаться мы не будем, иначе я не оставлю себе почвы для размышлений. Замечу только, что мы рассмотрели все предпосылки и выводы к существующим алгоритмам менеджмента памяти.

## Выводы

Давайте быстренько пробежимся чтобы получше осело в памяти:
- Существует две кучи объектов: для объектов меньше 85K байт и, соответственно, больше либо равно 85К байт. Это сделано для того чтобы можно было воспользоваться алгоритмом сжатия кучи - `Compact`. Без деления на две кучи сборщик мусора работал бы очень долго;
- Также для малых объектов куча делится на три поколения. Это также сделано для того чтобы отработать освобождение памяти максимально быстро. Ведь вероятность того, что старый объект лишился последней ссылки намного ниже чем если бы ссылки на себя лишился только что созданный: такова структура бытия;
- 0 поколение создано фиксированных размеров чтобы гарантировать, что сборка мусора по времени не превысит некоторый лимит. Иначе это будет заметно для пользователя. На неограниченном объёме эта операция работала бы случайно-большое время;
- 2 поколение -- склад старых объектов. Он огромен и потому туда имеет смысл заглядывать максимально редко. 
- 1 поколение -- промежуточный перевал между 0 и 2 поколениями чтобы объект - неудачник, не потерявший последнюю ссылку на себя при срабатывании GC не ушёл на склад старых объектов: в поколение 2. Это поколение также имеет фиксированный размер, но более крупное, чем поколение 0;
- Память линейна и поколения -- это по сути непрерывные отрезки этой памяти в том смысле что объекты разных поколений не перемешаны. Напротив, сначала идут объекты 2 поколения, затем - 1 поколения и в самом конце - 0 поколения
- Помимо `Compact` существует второй алгоритм управления занятыми и свободными участками: `Sweep`. По сути это - список свободных участков кучи. Он работает в обоих кучах: и в LOH и в SOH. Когда в куче заканчивается место, вычисляются свободные участки. Если участки пригодны для размещения объектов, выделение продолжится на них. Иначе -- срабатывает `Compact`.
- Для SOH первым приоритетом отрабатывает `Sweep`, т.к. он легче, вторым - `Compact`. Для LOH - всегда `Compact`, т.к. сжимать кучу больших массивов не оптимально.
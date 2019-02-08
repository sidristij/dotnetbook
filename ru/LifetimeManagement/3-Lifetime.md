## Шаблон Lifetime

После того как мы так подробно рассмотрели шаблон освобождения ресурсов под названием IDisposable, пришло самое время поговорить про шаблон Lifetime. Он решает те же самые задачи, какие решал шаблон IDisposable, но является в некоторой степени inversion of control этого шаблона: если IDisposable подразумевает, что он будет вызываться кем-то, кто контролирует время жизни нашего класса (а чаще всего тем, кто экземпляр нашего класса создал), то Lifetime напротив: выносит в этот процесс много гибкости, вынося задачу разрушения зависимых ресурсов в отдельную плоскость.

Теперь, в новых реалиях, внешний объект будет передавать нам специальный экземпляр `Lifetime`, от которого мы будем зависеть и относительно времени жизни которого мы будем существовать.

Для разделения зон ответственности существует три типа, определяющие понятие времени жизни:

1. `LifetimeDef`. Экземпляр данного типа хранится у того объекта, который будет владеть вопросом завершения времени жизни зависящих от него объектов;
2. `Lifetime` создается экземпляром `LifetimeDef` и будет передан всем тем, кто будет так или иначе зависеть от владельца их временем жизни. Забота экземпляров типов, которые получили `Lifetime` - просто накидать внутрь него действий, которые будут их разрушать.
3. `OuterLifetime` - инкапсулирует понятие readonly `Lifetime`. Другими словами, это средство защиты от конечного программиста: чтобы он не мог в переданную зависимость добавить действия по уничтожению своей. Публично, как и `Lifetime`, `OuterLifetime` содержит только свойство `IsTerminated`, однако на основе него можно создать зависимый экземпляр `LifetimeDef`, на основе которого можно осуществлять менеджмент жизни собственного экземпляра.

Рассмотрим сам интерфейс типа:

``` csharp
public class Lifetime
{
    // private List<Action> Actions { get; }

    public static Lifetime Eternal = Define("Eternal").Lifetime;

    public bool IsTerminated { get; internal set; }

    public Lifetime();

    public void Add(Action action);

    // protected void Remove(Action action);

    public void AddDisposable(IDisposable disposable);

    public void AddBracket(Action subscribe, Action unsubscribe);

    // internal void Terminate();

    public static LifetimeDef Define(string name);

    public static LifetimeDef DefineDependent(OuterLifetime parent, string name = null);

    public static Lifetime WhenAll(params OuterLifetime[] lifetimes);

    public static Lifetime WhenAny(params OuterLifetime[] lifetimes);

    // protected void CheckTerminated();
}
```
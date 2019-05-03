<Query Kind="Program" />

void Main()
{
	var foo = new Foo();
	var boo = new Boo();

	((IDisposable)foo).Dispose();
	foo.Dispose();
	((IDisposable)boo).Dispose();
	boo.Dispose();
}

class Foo : IDisposable
{
	void IDisposable.Dispose()
	{
		Console.WriteLine("Foo.IDisposable::Dispose");
	}
	
	public void Dispose()
	{
		Console.WriteLine("Foo::Dispose()");
	}
}

class Boo : Foo, IDisposable
{
	void IDisposable.Dispose()
	{
		Console.WriteLine("Boo.IDisposable::Dispose");
	}

	public new void Dispose()
	{
		Console.WriteLine("Boo::Dispose()");
	}
}
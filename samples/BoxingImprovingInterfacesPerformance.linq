<Query Kind="Expression" />

public class Boxed<T>
{
	public ref T Value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool Equals(object obj)
	{
		return Value.Equals(obj);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override string ToString()
	{
		return Value.ToString();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode()
	{
		return Value.GetHashCode();
	}
}

struct Foo : IBoo
{
	public int a;

	public void Boo()
	{
		a = 10;
	}
}

interface IBoo
{
	void Boo();
}

public BoxedBoo : Boxed<Foo>, IBoo
{
	void Boo()
	{
		Value.Boo();
	}
}
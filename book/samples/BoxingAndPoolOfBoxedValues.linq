<Query Kind="Program">
  <NuGetReference>BenchmarkDotNet</NuGetReference>
  <Namespace>BenchmarkDotNet.Attributes</Namespace>
  <Namespace>System.Runtime.CompilerServices</Namespace>
</Query>

void Main()
{
	Stopwatch sw = Stopwatch.StartNew();
	TestBoxing();
	sw.ElapsedMilliseconds.Dump();

	sw = Stopwatch.StartNew();
	TestBoxing2();
	sw.ElapsedMilliseconds.Dump();

	var pool = new Pool<int>(10);
	
	sw = Stopwatch.StartNew();
	TestBoxing3(pool);
	sw.ElapsedMilliseconds.Dump();

}

void TestBoxing()
{
	for (int i = 0; i < 100000001; i++)
	{
		var a = (object)5;
		var b = (int)a;
	}
}

void TestBoxing2()
{
	for (int i = 0; i < 100000001; i++)
	{
		var a = new Boxed<int> { Value = 5 };
	}
}

void TestBoxing3(Pool<int> queue)
{
	var xx = queue.Take();
	for (int i = 0; i < 100000001; i++)
	{
		xx.Value = 5;
		var a = xx.Value;
	}
	queue.Return(xx);
}


struct Foo {
	public int x;
}

public static class Box
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Boxed<T> It<T>(ref T value){
		return new Boxed<T> {Value = value};
	}
}

public sealed class Boxed<T>
{	
	public T Value;

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

sealed class Pool<T> where T : struct
{
	Boxed<T>[] items;
	int count;

	public Pool(int maxCount)
	{
		items = new Boxed<T>[maxCount];
		for (int i = 0; i < maxCount; i++)
		{
			items[i] = new Boxed<T>();
		}
		count = maxCount;
	}

	public int Count
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get { return count; }
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Boxed<T> Take()
	{
		return items[--count];
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Return(Boxed<T> item)
	{
		items[count++] = item;
	}

	public void Clear()
	{
		for (int i = 0; i < count; i++)
			items[i] = null;
		count = 0;
	}
}
<Query Kind="Program">
  <NuGetReference>System.Memory</NuGetReference>
</Query>

void Main()
{
	int length = 10;
	Span<byte> first = stackalloc byte[length];
	Span<byte> second = stackalloc byte[length];
	var pair = new TwoSpans<byte>
	{
		first = first,
		second = second
	};
		
	Console.WriteLine(pair.first.Length);
	Console.WriteLine(pair.second.Length);
}

ref struct TwoSpans<T>
{
	public Span<T> first;
	public Span<T> second;
}
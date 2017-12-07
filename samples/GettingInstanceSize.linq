<Query Kind="Program">
  <Namespace>System.Runtime.InteropServices</Namespace>
</Query>

unsafe void Main()
{
	Console.WriteLine(SizeOf(typeof(Sample)));
	Console.WriteLine(SizeOf(typeof(Int32)));
	Console.WriteLine(SizeOf(typeof(Int64)));
	Console.WriteLine(SizeOf(typeof(Int16)));
	Console.WriteLine(SizeOf(typeof(Char)));
	Console.WriteLine(SizeOf(typeof(double)));
	Console.WriteLine(SizeOf(typeof(IEnumerable)));
	Console.WriteLine(SizeOf(typeof(List<int>)));
	Console.WriteLine(SizeOf(typeof(GenericSample<int>)));
	Console.WriteLine(SizeOf(typeof(GenericSample<Int64>)));
	Console.WriteLine(SizeOf(typeof(GenericSample<IEnumerable>)));
	Console.WriteLine(SizeOf(typeof(GenericSample<DateTime>)));
	Console.WriteLine(SizeOf(typeof(string)));
	Console.WriteLine(SizeOf(new int[] {1}.GetType()));
	Console.WriteLine(SizeOf(new int[] {1,2,3}.GetType()));
}

unsafe int SizeOf(Type type)
{
	MethodTable* pvmt = (MethodTable*)type.TypeHandle.Value.ToPointer();
	return pvmt->Size;
}

[StructLayout(LayoutKind.Explicit)]
public struct MethodTable
{
	[FieldOffset(4)]
	public short Size;
}

class Sample
{
	int x;
}

class GenericSample<T>
{
	T fld;
}
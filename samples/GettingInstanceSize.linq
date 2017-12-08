<Query Kind="Program">
  <Namespace>System.Runtime.InteropServices</Namespace>
  <Namespace>System.Runtime.CompilerServices</Namespace>
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

	Action<string> stringWriter = (arg) =>
	{
		Console.WriteLine($"Length of `{arg}` string: {SizeOf(arg)}");
	};

	stringWriter("a");
	stringWriter("ab");
	stringWriter("abc");
	stringWriter("abcd");
	stringWriter("abcde");
	stringWriter("abcdef");
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
unsafe int SizeOf(Type type)
{
	var pvmt = (MethodTable*)type.TypeHandle.Value.ToPointer();
	return pvmt->Size;
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
int SizeOf<T>()
{
	return SizeOf(typeof(T));
}

unsafe int SizeOf(object obj)
{
	var majorNetVersion = Environment.Version.Major;
	var type = obj.GetType();
	var href = Union.GetRef(obj).ToInt64();
	
	if(type == typeof(string))
	{
		if (majorNetVersion >= 4)
		{
			var length = *(int*)((int)href + 4);
			return 4 * ((14 + 2 * length + 3) / 4);
		}
		else
		{
			// on 1.0 -> 3.5 string have additional RealLength field
			var length = *(int*)((int)href + 8);
			return 4 * ((16 + 2 * length + 3) / 4);
		}
	} else
	if(type == typeof(Array))
	{
		return 0;
	}
	return SizeOf(type);
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

[StructLayout(LayoutKind.Explicit)]
public class Union
{
	public Union()
	{
		IntPtr = new Holder<IntPtr>();
		Reference = new Holder<object>();
	}
	
	public static IntPtr GetRef(object obj)
	{
		var union = new Union();
		union.Reference.Value = obj;
		return union.IntPtr.Value;
	}

	[FieldOffset(0)]
	public Holder<IntPtr> IntPtr;

	[FieldOffset(0)]
	public Holder<object> Reference;


	public class Holder<T>
	{
		public T Value;
	}
}
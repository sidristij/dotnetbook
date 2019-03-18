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

	Console.WriteLine($"size of int[]{{1,2}}: {SizeOf(new int[2])}");
	Console.WriteLine($"size of int[2,1]{{1,2}}: {SizeOf(new int[1,2])}");
	Console.WriteLine($"size of int[2,3,4,5]{{...}}: {SizeOf(new int[2, 3, 4, 5])}");
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
	var DWORD = sizeof(IntPtr);
	var baseSize = 3 * DWORD;

	if (type == typeof(string))
	{
		if (majorNetVersion >= 4)
		{
			var length = (int)*(int*)(href + DWORD /* skip vmt */);
			return DWORD * ((baseSize + 2 + 2 * length + (DWORD-1)) / DWORD);
		}
		else
		{
			// on 1.0 -> 3.5 string have additional RealLength field
			var arrlength = *(int*)(href + DWORD /* skip vmt */);
			var length = *(int*)(href + DWORD /* skip vmt */ + 4 /* skip length */);
			return DWORD * ((baseSize + 2 + 2 * length + (DWORD -1)) / DWORD);
		}
	}
	else
	if (type.BaseType == typeof(Array) || type == typeof(Array))
	{
		return ((ArrayInfo*)href)->SizeOf();
	}
	return SizeOf(type);
}

[StructLayout(LayoutKind.Explicit)]
public struct MethodTable
{
	[FieldOffset(0)]
	public MethodTableFlags Flags;
	
	[FieldOffset(4)]
	public short Size;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ArrayInfo
{
	private MethodTable* MethodTable;

	private IntPtr TotalLength;

	private int Lengthes;

	public bool IsMultidimentional
	{
		get
		{
			return (MethodTable->Flags & MethodTableFlags.IfArrayThenMultidim) == 0;
		}
	}

	public bool IsValueTypes
	{
		get
		{
			return (MethodTable->Flags & MethodTableFlags.IfArrayThenSharedByReferenceTypes) == 0;
		}
	}

	public int Dimensions
	{
		get
		{
			if (IsMultidimentional)
			{
				fixed (IntPtr* cur = &TotalLength)
				{
					var count = 0;
					while (((int*)cur)[count] != 0) count++;
					return count;
				}
			}

			return 1;
		}
	}

	public int GetLength(int dim)
	{
		var maxDim = Dimensions;
		if (maxDim < dim)
			throw new ArgumentOutOfRangeException("dim");

		fixed (int* addr = &Lengthes)
		{
			return addr[dim];
		}
	}

	public int SizeOf()
	{
		var total = 0;
		int elementsize;

		fixed (void* entity = &MethodTable)
		{
			var arr = Union.GetObj<Array>((IntPtr)entity);
			var elementType = arr.GetType().GetElementType();

			if (elementType.IsValueType)
			{
				var typecode = Type.GetTypeCode(elementType);

				switch (typecode)
				{
					case TypeCode.Byte:
					case TypeCode.SByte:
					case TypeCode.Boolean:
						elementsize = 1;
						break;
					case TypeCode.Int16:
					case TypeCode.UInt16:
					case TypeCode.Char:
						elementsize = 2;
						break;
					case TypeCode.Int32:
					case TypeCode.UInt32:
					case TypeCode.Single:
						elementsize = 4;
						break;
					case TypeCode.Int64:
					case TypeCode.UInt64:
					case TypeCode.Double:
						elementsize = 8;
						break;
					case TypeCode.Decimal:
						elementsize = 12;
						break;
					default:
						var info = (MethodTable*)elementType.TypeHandle.Value;
						elementsize = info->Size - 2 * sizeof(IntPtr); // sync blk + vmt ptr
						break;
				}
			}
			else
			{
				elementsize = IntPtr.Size;
			}

			// Header
			total += 3 * sizeof(IntPtr); // sync blk + vmt ptr + total length
			total += elementType.IsValueType ? 0 : sizeof(IntPtr); // MethodsTable for refTypes
			total += IsMultidimentional ? Dimensions * sizeof(int) : 0;
		}

		// Contents
		total += (int)TotalLength * elementsize;

		// align size to IntPtr
		if ((total % sizeof(IntPtr)) != 0)
		{
			total += sizeof(IntPtr) - total % (sizeof(IntPtr));
		}
		return total;
	}
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

	public static T GetObj<T>(IntPtr reference)
	{
		var union = new Union();
		union.IntPtr.Value = reference;
		return (T)union.Reference.Value;
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

[Flags]
public enum MethodTableFlags : uint
{
	Array = 0x00010000,

	InternalCorElementTypeExtraInfoMask = 0x00060000,
	InternalCorElementTypeExtraInfo_IfNotArrayThenTruePrimitive = 0x00020000,
	InternalCorElementTypeExtraInfo_IfNotArrayThenClass = 0x00040000,
	InternalCorElementTypeExtraInfo_IfNotArrayThenValueType = 0x00060000,

	IfArrayThenMultidim = 0x00020000,
	IfArrayThenSharedByReferenceTypes = 0x00040000,

	ContainsPointers = 0x00080000,
	HasFinalizer = 0x00100000, // instances require finalization

	IsMarshalable = 0x00200000, // Is this type marshalable by the pinvoke marshalling layer

	HasRemotingVtsInfo = 0x00400000, // Optional data present indicating VTS methods and optional fields
	IsFreezingRequired = 0x00800000, // Static data should be frozen after .cctor

	TransparentProxy = 0x01000000, // tranparent proxy
	CompiledDomainNeutral = 0x02000000, // Class was compiled in a domain neutral assembly

	// This one indicates that the fields of the valuetype are 
	// not tightly packed and is used to check whether we can
	// do bit-equality on value types to implement ValueType::Equals.
	// It is not valid for classes, and only matters if ContainsPointer
	// is false.
	//
	NotTightlyPacked = 0x04000000,

	HasCriticalFinalizer = 0x08000000, // finalizer must be run on Appdomain Unload
	UNUSED = 0x10000000,
	ThreadContextStatic = 0x20000000,

	IsFreezingCompleted = 0x80000000, // Static data has been frozen

	NonTrivialInterfaceCast = Array | TransparentProxy,
}
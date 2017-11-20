<Query Kind="Program">
  <Namespace>System.Runtime.CompilerServices</Namespace>
  <Namespace>System.Runtime.InteropServices</Namespace>
</Query>

unsafe void Main()
{
	object boxed = 10;

	RefGetter rg = new RefGetter();
	//rg.reference = new Holder<object> { Val = boxed};
	
	// забираем адрес указателя на VMT
	//var address = (void**)rg.val.Val.ToPointer();
	
	unsafe {
		// забираем адрес Virtual Methods Table
		//var structVmt = typeof(SimpleIntHolder).TypeHandle.Value.ToPointer();
		
		// меняем адрес VMT целого числа, ушедшего в Heap на VMT SimpleIntHolder, превратив Int в структуру
		//*address = structVmt;
	}
	
	var structure = (SimpleIntHolder)boxed;
	structure.value.Dump();
}

struct SimpleIntHolder
{
	public int value;
}
/*
[StructLayoutAttribute(LayoutKind.Explicit)]
public class RefGetter
{
	[FieldOffset(0)]
	public Holder<object> reference;
	
	[FieldOffset(0)]
	public Holder<IntPtr> val;
}

public struct Holder<T> {
	public T Val;
}
*/
<Query Kind="Program">
  <Namespace>System.Runtime.InteropServices</Namespace>
</Query>

public static unsafe void Main()
{
	Union x = new Union();
	x.Reference.Value = "Hello!";
	IntPtr vmt = *(IntPtr*)x.Value.Value;
	vmt.Dump();
	
	typeof(string).TypeHandle.Value.Dump();
}

[StructLayout(LayoutKind.Explicit)]
public class Union
{
	public Union()
	{
		Value = new Holder<IntPtr>();
		Reference = new Holder<object>();
	}

	[FieldOffset(0)]
	public Holder<IntPtr> Value;

	[FieldOffset(0)]
	public Holder<object> Reference;


	public class Holder<T>
	{
		public T Value;
	}
}
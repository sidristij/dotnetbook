<Query Kind="Program">
  <NuGetReference>System.Memory</NuGetReference>
  <Namespace>System</Namespace>
  <Namespace>System.Runtime.CompilerServices</Namespace>
</Query>

unsafe void Main()
{
	var firstString = System.Environment.GetFolderPath(Environment.SpecialFolder.CommonMusic);
	var str = string.Intern(@"C:\Users\Public\Music");
	ReadOnlyMemory<char> ronly = str.AsMemory();
	Memory<char> mem = Unsafe.As<ReadOnlyMemory<char>, Memory<char>>(ref ronly);
	//var destination = mem.Span.Slice("Michael ".Length, 5);
	//"hates".AsSpan().CopyTo(destination);
	Console.WriteLine(firstString);
}

<Query Kind="Program">
  <Namespace>System.Runtime.CompilerServices</Namespace>
  <Namespace>System.Runtime.InteropServices</Namespace>
</Query>

unsafe void Main()
{
	// делаем boxed int
    object boxed = 10;

    // забираем адрес указателя на VMT
    var address = (void**)EntityPtr.ToPointerWithOffset(boxed);

    unsafe
    {
        // забираем адрес Virtual Methods Table
        var structVmt = typeof(SimpleIntHolder).TypeHandle.Value.ToPointer();

        // меняем адрес VMT целого числа, ушедшего в Heap на VMT SimpleIntHolder, превратив Int в структуру
        *address = structVmt;
    }

    var structure = (IGetterByInterface)boxed;

    Console.WriteLine(structure.GetByInterface());
}

interface IGetterByInterface
{
    int GetByInterface();
}

struct SimpleIntHolder : IGetterByInterface
{
    public int value;

    int IGetterByInterface.GetByInterface()
    {
        return value;
    }
}
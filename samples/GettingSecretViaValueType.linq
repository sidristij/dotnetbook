<Query Kind="Program" />

unsafe void Main()
{
	int secret2 = 999;
	Console.WriteLine("Entering FirstMethod");
	FirstMethod();
	Console.WriteLine("Returned from FirstMethod");
}

void FirstMethod()
{
	int secret = 666;
	Console.WriteLine("Entered FirstMethod");
	SecondMethod();
	Console.WriteLine("Returning from FirstMethod");
}

unsafe void SecondMethod()
{
	Console.WriteLine("Entered FirstMethod");
	StartingPoint sp;
	StackStructure ss;
	unsafe
	{
		ss = *(StackStructure*)&sp;
	}

	SecondMethod();
	Console.WriteLine("Returning from FirstMethod");
}

struct StackStructure
{
	public int a01_Self;
	public int a02_EBP_unsafe;
	public int a03_RET_unsafe;
	public int a04_RET_unsafe2;
	public int a05_RET;
	public int a06_EBP;
	public int a07_secret;
	public int a08_secret4;
	public int a09_secret5;
	public int a10_secret6;
	public int a11_EBP2;
	public int a12_RET2;
}

struct StartingPoint
{
	public int Self;
}
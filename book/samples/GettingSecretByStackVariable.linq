<Query Kind="Program" />

unsafe void Main()
{
	int secret = 666;
	HeightHolder hh;
	hh.Height = 5;
	
	WidthHolder wh;
	unsafe
	{
		wh = *(WidthHolder *)&hh;
	}
	Console.WriteLine("Width: " + wh.Width);
	Console.WriteLine("Secret: " + wh.Secret);
}

struct WidthHolder
{
	public int Width;
	public int Secret;
}

struct HeightHolder
{
	public int Height;
}
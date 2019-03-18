<Query Kind="Program" />

void Main()
{
	var foo = new Foo();
	foo.a = 1;
	
	foo.a.Dump();
	
	IBoo boo = foo;
	boo.Boo();
	
	foo.a.Dump();
}

struct Foo : IBoo {

	public int a;

	public void Boo()
	{
		a = 10;
	}
}

interface IBoo {
	void Boo();
}

// Define other methods and classes here
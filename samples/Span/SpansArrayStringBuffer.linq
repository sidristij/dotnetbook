<Query Kind="Program">
  <NuGetReference>System.Memory</NuGetReference>
</Query>

unsafe void Main()
{
	var array = new[] { '1', '2', '3', '4', '5', '6' };
	var arrSpan = new Span<char>(array, 1, 3);
	if (TryParseInt32(arrSpan, out var res1))
	{
		Console.WriteLine(res1);
	}

	var srcString = "123456";
	var strSpan = srcString.AsSpan().Slice(1, 3);
	if (TryParseInt32(strSpan, out var res2))
	{
		Console.WriteLine(res2);
	}

	Span<char> buf = stackalloc char[6];
	buf[0] = '1'; buf[1] = '2'; buf[2] = '3';
	buf[3] = '4'; buf[4] = '5'; buf[5] = '6';

	if (TryParseInt32(buf.Slice(1, 3), out var res3))
	{
		Console.WriteLine(res3);
	}
}

public bool TryParseInt32(ReadOnlySpan<char> input, out int result)
{
	result = 0;
	for (int i = 0; i < input.Length; i++)
	{
		if(input[i] < '0' || input[i] > '9')
			return false;
		result = result * 10 + ((int)input[i] - '0');
	}
	return true;
}
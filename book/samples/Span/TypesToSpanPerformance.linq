<Query Kind="Program">
  <NuGetReference>BenchmarkDotNet</NuGetReference>
  <NuGetReference>System.Memory</NuGetReference>
  <Namespace>BenchmarkDotNet.Analysers</Namespace>
  <Namespace>BenchmarkDotNet.Attributes</Namespace>
  <Namespace>BenchmarkDotNet.Characteristics</Namespace>
  <Namespace>BenchmarkDotNet.Code</Namespace>
  <Namespace>BenchmarkDotNet.Columns</Namespace>
  <Namespace>BenchmarkDotNet.Configs</Namespace>
  <Namespace>BenchmarkDotNet.ConsoleArguments</Namespace>
  <Namespace>BenchmarkDotNet.Diagnosers</Namespace>
  <Namespace>BenchmarkDotNet.Engines</Namespace>
  <Namespace>BenchmarkDotNet.Environments</Namespace>
  <Namespace>BenchmarkDotNet.Exporters</Namespace>
  <Namespace>BenchmarkDotNet.Exporters.Csv</Namespace>
  <Namespace>BenchmarkDotNet.Exporters.Json</Namespace>
  <Namespace>BenchmarkDotNet.Exporters.Xml</Namespace>
  <Namespace>BenchmarkDotNet.Extensions</Namespace>
  <Namespace>BenchmarkDotNet.Filters</Namespace>
  <Namespace>BenchmarkDotNet.Helpers</Namespace>
  <Namespace>BenchmarkDotNet.Horology</Namespace>
  <Namespace>BenchmarkDotNet.Jobs</Namespace>
  <Namespace>BenchmarkDotNet.Loggers</Namespace>
  <Namespace>BenchmarkDotNet.Mathematics</Namespace>
  <Namespace>BenchmarkDotNet.Mathematics.Histograms</Namespace>
  <Namespace>BenchmarkDotNet.Order</Namespace>
  <Namespace>BenchmarkDotNet.Parameters</Namespace>
  <Namespace>BenchmarkDotNet.Portability</Namespace>
  <Namespace>BenchmarkDotNet.Portability.Cpu</Namespace>
  <Namespace>BenchmarkDotNet.Properties</Namespace>
  <Namespace>BenchmarkDotNet.Reports</Namespace>
  <Namespace>BenchmarkDotNet.Running</Namespace>
  <Namespace>BenchmarkDotNet.Toolchains</Namespace>
  <Namespace>BenchmarkDotNet.Toolchains.CoreRt</Namespace>
  <Namespace>BenchmarkDotNet.Toolchains.CsProj</Namespace>
  <Namespace>BenchmarkDotNet.Toolchains.CustomCoreClr</Namespace>
  <Namespace>BenchmarkDotNet.Toolchains.DotNetCli</Namespace>
  <Namespace>BenchmarkDotNet.Toolchains.InProcess</Namespace>
  <Namespace>BenchmarkDotNet.Toolchains.Parameters</Namespace>
  <Namespace>BenchmarkDotNet.Toolchains.Results</Namespace>
  <Namespace>BenchmarkDotNet.Toolchains.Roslyn</Namespace>
  <Namespace>BenchmarkDotNet.Validators</Namespace>
</Query>

public class SpanIndexer
{
	private const int Loops = 1000, Count = 100;
    private char[] arrayField;
	private ArraySegment<char> segment;
	private string str;

	[GlobalSetup]
	public void Setup()
	{
		str = new string(Enumerable.Repeat('a', Count).ToArray());
		arrayField = str.ToArray();
		segment = new ArraySegment<char>(arrayField);
	}

    [Benchmark(Baseline = true, OperationsPerInvoke = Loops*Count)]
    public void ArrayIndexer_Set()
    {
		var sum = 0;
		for (int _ = 0; _ < Loops; _++)
		for (int index = 0, len = arrayField.Length; index < len; index++)
		{
			sum += arrayField[index];
		}
	}

	[Benchmark(OperationsPerInvoke = Loops*Count)]
	public void ArraySegmentIndexer_Set()
	{
		var sum = 0;
		var accessor = (IList<char>)segment;
		for (int _ = 0; _ < Loops; _++)
		for (int index = 0, len = accessor.Count; index < len; index++)
		{
			sum += accessor[index];
		}
	}

	[Benchmark(OperationsPerInvoke = Loops*Count)]
	public void StringIndexer_Set()
	{
		var sum = 0;
		for (int _ = 0; _ < Loops*Count; _++)
		for (int index = 0, len = str.Length; index < len; index++)
		{
			sum += str[index];
		}
	}

	[Benchmark(OperationsPerInvoke = Loops*Count)]
	public void SpanArrayIndexer_Set()
	{
		SetValues(arrayField);
	}
	
	[Benchmark(OperationsPerInvoke = Loops*Count)]
	public void SpanArraySegmentIndexer_Set()
	{
		SetValues(segment);
	}
	
	[Benchmark(OperationsPerInvoke = Loops*Count)]
	public void SpanStringIndexer_Set()
	{
		SetValues(str.AsSpan());
	}
	
	private int SetValues(ReadOnlySpan<char> span)
	{
		var sum = 0;
		for (int _ = 0; _ < Loops; _++)
		for (int index = 0, len = span.Length; index < len; index++)
		{
			sum += span[index];
		}
		return sum;
	}
}

void Main()
{
	BenchmarkRunner.Run<SpanIndexer>();
}
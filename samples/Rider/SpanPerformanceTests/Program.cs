using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.CsProj;

namespace SpanPerformanceTests
{
	[Config(typeof(MultipleRuntimesConfig))]
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
		public void ArrayIndexer_Get()
		{
			var tmp = 0;
			for (int _ = 0; _ < Loops; _++)
			for (int index = 0, len = arrayField.Length; index < len; index++)
			{
				tmp = arrayField[index];
			}
		}

		[Benchmark(OperationsPerInvoke = Loops*Count)]
		public void ArraySegmentIndexer_Get()
		{
			var tmp = 0;
			var accessor = (IList<char>)segment;
			for (int _ = 0; _ < Loops; _++)
			for (int index = 0, len = accessor.Count; index < len; index++)
			{
				tmp = accessor[index];
			}
		}

		[Benchmark(OperationsPerInvoke = Loops*Count)]
		public void StringIndexer_Get()
		{
			var tmp = 0;
			for (int _ = 0; _ < Loops*Count; _++)
			for (int index = 0, len = str.Length; index < len; index++)
			{
				tmp = str[index];
			}
		}

		[Benchmark(OperationsPerInvoke = Loops*Count)]
		public void SpanArrayIndexer_Get()
		{
			GetValues(arrayField);
		}
	
		[Benchmark(OperationsPerInvoke = Loops*Count)]
		public void SpanArraySegmentIndexer_Get()
		{
			GetValues(segment);
		}
	
		[Benchmark(OperationsPerInvoke = Loops*Count)]
		public void SpanStringIndexer_Get()
		{
			GetValues(str.AsSpan());
		}
	
		private int GetValues(ReadOnlySpan<char> span)
		{
			var tmp = 0;
			for (int _ = 0; _ < Loops; _++)
			for (int index = 0, len = span.Length; index < len; index++)
			{
				tmp = span[index];
			}
			return tmp;
		}
	}

	public class MultipleRuntimesConfig : ManualConfig
	{
		public MultipleRuntimesConfig()
		{
			Add(Job.Default
				.With(CsProjClassicNetToolchain.Net471) // Span не поддерживается CLR
				.WithId(".NET 4.7.1"));

			Add(Job.Default
				.With(CsProjCoreToolchain.NetCoreApp20) // Span поддерживается CLR
				.WithId(".NET Core 2.0"));
		}
	}
	
    class Program
    {
        static void Main(string[] args)
        {
		    BenchmarkRunner.Run<SpanIndexer>();
	    }
    }
}
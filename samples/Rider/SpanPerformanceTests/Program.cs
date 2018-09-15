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
        private const int Count = 100;
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

        [Benchmark(Baseline = true, OperationsPerInvoke = Count)]
        public int ArrayIndexer_Get()
        {
            var tmp = 0;
            for (int index = 0, len = arrayField.Length; index < len; index++)
            {
                tmp = arrayField[index];
            }
            return tmp;
        }

        [Benchmark(OperationsPerInvoke = Count)]
        public void ArrayIndexer_Set()
        {
            for (int index = 0, len = arrayField.Length; index < len; index++)
            {
                arrayField[index] = '0';
            }
        }

        [Benchmark(OperationsPerInvoke = Count)]
        public int ArraySegmentIndexer_Get()
        {
            var tmp = 0;
            var accessor = (IList<char>)segment;
            for (int index = 0, len = accessor.Count; index < len; index++)
            {
                tmp = accessor[index];
            }
            return tmp;
        }

        [Benchmark(OperationsPerInvoke = Count)]
        public void ArraySegmentIndexer_Set()
        {
            var accessor = (IList<char>)segment;
            for (int index = 0, len = accessor.Count; index < len; index++)
            {
                accessor[index] = '0';
            }
        }
        
        [Benchmark(OperationsPerInvoke = Count)]
        public int StringIndexer_Get()
        {
            var tmp = 0;
            for (int index = 0, len = str.Length; index < len; index++)
            {
                tmp = str[index];
            }

            return tmp;
        }
        
        [Benchmark(OperationsPerInvoke = Count)]
        public int SpanArrayIndexer_Get()
        {
            var span = arrayField.AsSpan();
            var tmp = 0;
            for (int index = 0, len = span.Length; index < len; index++)
            {
                tmp = span[index];
            }
            return tmp;
        }
    
        [Benchmark(OperationsPerInvoke = Count)]
        public int SpanArraySegmentIndexer_Get()
        {
            var span = segment.AsSpan();
            var tmp = 0;
            for (int index = 0, len = span.Length; index < len; index++)
            {
                tmp = span[index];
            }
            return tmp;
        }
    
        [Benchmark(OperationsPerInvoke = Count)]
        public int SpanStringIndexer_Get()
        {
            var span = str.AsSpan();
            var tmp = 0;
            for (int index = 0, len = span.Length; index < len; index++)
            {
                tmp = span[index];
            }
            return tmp;
        }

        [Benchmark(OperationsPerInvoke = Count)]
        public void SpanArrayIndexer_Set()
        {
            var span = arrayField.AsSpan();
            for (int index = 0, len = span.Length; index < len; index++)
            {
                span[index] = '0';
            }
        }
    
        [Benchmark(OperationsPerInvoke = Count)]
        public void SpanArraySegmentIndexer_Set()
        {
            var span = segment.AsSpan();
            for (int index = 0, len = span.Length; index < len; index++)
            {
                span[index] = '0';
            }
        }
    }

    public class MultipleRuntimesConfig : ManualConfig
    {
        public MultipleRuntimesConfig()
        {
            Add(Job.Default
                .With(CsProjClassicNetToolchain.Net47) // Span не поддерживается CLR
                .WithId(".NET 4.7"));
            Add(Job.Default
                .With(CsProjClassicNetToolchain.Net472) // Span не поддерживается CLR
                .WithId(".NET 4.7.2"));

            Add(Job.Default
                .With(CsProjCoreToolchain.NetCoreApp20) // Span поддерживается CLR
                .WithId(".NET Core 2.0"));

            Add(Job.Default
                .With(CsProjCoreToolchain.NetCoreApp21) // Span поддерживается CLR
                .WithId(".NET Core 2.1"));

            Add(Job.Default
                .With(CsProjCoreToolchain.NetCoreApp22) // Span поддерживается CLR
                .WithId(".NET Core 2.2"));
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
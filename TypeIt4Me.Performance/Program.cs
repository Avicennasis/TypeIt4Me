using BenchmarkDotNet.Running;

namespace TypeIt4Me.Performance
{
    class Program
    {
        static void Main(string[] args)
        {
            BenchmarkRunner.Run<SnippetManagerBenchmark>();
        }
    }
}

using BenchmarkDotNet.Running;

namespace TypeIt4Me.Performance
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<InputInjectorBenchmark>();
        }
    }
}

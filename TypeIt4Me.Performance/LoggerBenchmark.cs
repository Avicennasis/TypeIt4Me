using BenchmarkDotNet.Attributes;
using System;
using System.IO;
using System.Threading.Tasks;

namespace TypeIt4Me.Performance
{
    [MemoryDiagnoser]
    public class LoggerBenchmark
    {
        private string _logPath = string.Empty;
        private string _content = string.Empty;

        [GlobalSetup]
        public void Setup()
        {
            _logPath = Path.Combine(Path.GetTempPath(), "bench_error.log");
            _content = new string('a', 1000); // 1KB log
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            if (File.Exists(_logPath))
            {
                File.Delete(_logPath);
            }
        }

        [Benchmark(Baseline = true)]
        public void SyncLog()
        {
            File.AppendAllText(_logPath, _content);
        }

        [Benchmark]
        public async Task AsyncLog()
        {
            await File.AppendAllTextAsync(_logPath, _content);
        }
    }
}

using BenchmarkDotNet.Attributes;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TypeIt4Me.Models;
using TypeIt4Me.Services;

namespace TypeIt4Me.Performance
{
    public class NullLogger : ILogger
    {
        public void Log(string message) { }
        public void LogError(string message, Exception? ex = null) { }
        public void LogWarning(string message) { }
        public void LogInformation(string message) { }
        public void LogDebug(string message) { }
        public void LogInfo(string message) { }
    }

    [MemoryDiagnoser]
    public class SnippetManagerBenchmark
    {
        private SnippetManager _manager = null!;
        private string _appDataPath = null!;

        [Params(100, 1000)]
        public int SnippetCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _appDataPath = Path.Combine(Path.GetTempPath(), $"TypeIt4MeBench_{Guid.NewGuid()}");
            Directory.CreateDirectory(_appDataPath);

            _manager = new TestSnippetManager(new NullLogger(), Path.Combine(_appDataPath, "snippets.json"));

            for (int i = 0; i < SnippetCount; i++)
            {
                _manager.Snippets.Add(new Snippet
                {
                    Id = Guid.NewGuid(),
                    Name = $"abbrev{i}",
                    Content = $"content for snippet {i}",
                });
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_appDataPath))
            {
                Directory.Delete(_appDataPath, true);
            }
        }

        [Benchmark]
        public async Task SaveSnippets()
        {
            await _manager.SaveSnippetsAsync();
        }

        [Benchmark]
        public async Task SaveSnippetsEncrypted()
        {
            _manager.SetPin("1234".AsSpan());
            await _manager.SaveSnippetsAsync();
            _manager.SetPin("".AsSpan());
        }

        private class TestSnippetManager : SnippetManager
        {
            private readonly string _path;
            public TestSnippetManager(ILogger logger, string path) : base(logger)
            {
                _path = path;
            }
            protected override string GetFilePath() => _path;
        }
    }
}

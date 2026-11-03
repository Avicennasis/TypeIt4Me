using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using TypeIt4Me.Models;

namespace TypeIt4Me.Performance
{
    [MemoryDiagnoser]
    public class EmptyFilterBenchmark
    {
        private List<Snippet> _source = new List<Snippet>();
        private string _filter = "";

        [GlobalSetup]
        public void Setup()
        {
            for (int i = 0; i < 1000; i++)
            {
                _source.Add(new Snippet { Name = $"Snippet {i} test", Category = i % 2 == 0 ? "test category" : "other", Content = "content" });
                _source.Add(new Snippet { Name = $"Another {i}", Category = "none", Content = "content" });
            }
        }

        [Benchmark(Baseline = true)]
        public IEnumerable<Snippet> BaselineToList()
        {
            return ToListFiltering(_filter, _source);
        }

        [Benchmark]
        public IEnumerable<Snippet> OptimizedReturnSource()
        {
            return ReturnSourceFiltering(_filter, _source);
        }

        private IEnumerable<Snippet> ToListFiltering(string filter, IEnumerable<Snippet> source)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                return source.ToList();
            }
            return new List<Snippet>();
        }

        private IEnumerable<Snippet> ReturnSourceFiltering(string filter, IEnumerable<Snippet> source)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                return source;
            }
            return new List<Snippet>();
        }
    }
}

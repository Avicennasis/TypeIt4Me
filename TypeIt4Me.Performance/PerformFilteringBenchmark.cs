using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using TypeIt4Me.Models;

namespace TypeIt4Me.Performance
{
    [MemoryDiagnoser]
    public class PerformFilteringBenchmark
    {
        private List<Snippet> _source = new List<Snippet>();
        private string _filter = "test";

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
        public List<Snippet> LinqFilter()
        {
            return LinqFiltering(_filter, _source).ToList();
        }

        [Benchmark]
        public List<Snippet> LoopFilter()
        {
            return LoopFiltering(_filter, _source);
        }

        private IEnumerable<Snippet> LinqFiltering(string filter, IEnumerable<Snippet> source)
        {
            var query = source.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(filter))
            {
                query = query.Where(s => s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                         (s.Category != null && s.Category.Contains(filter, StringComparison.OrdinalIgnoreCase)));
            }
            return query;
        }

        private List<Snippet> LoopFiltering(string filter, IEnumerable<Snippet> source)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                return source.ToList();
            }

            int filterLength = filter.Length;
            var result = new List<Snippet>(source is ICollection<Snippet> col ? col.Count : 100);
            foreach (var s in source)
            {
                if (s == null) continue;

                string name = s.Name;
                if (name != null && name.Length >= filterLength && name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(s);
                    continue;
                }

                string category = s.Category;
                if (category != null && category.Length >= filterLength && category.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(s);
                }
            }
            return result;
        }
    }
}

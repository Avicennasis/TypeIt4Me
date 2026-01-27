```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.3 LTS (Noble Numbat)
Intel Xeon Processor 2.30GHz, 1 CPU, 4 logical and 4 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.0, 10.0.25.52411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.0 (10.0.0, 10.0.25.52411), X64 RyuJIT x86-64-v3


```
| Method                     | Mean        | Error     | StdDev     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------------- |------------:|----------:|-----------:|------:|--------:|-------:|----------:|------------:|
| Baseline_SendInputBatch    | 2,861.87 ns | 95.799 ns | 273.320 ns |  1.01 |    0.13 | 0.3395 |    8024 B |        1.00 |
| Baseline_ReleaseModifiers  |   294.56 ns |  5.915 ns |  15.582 ns |  0.10 |    0.01 | 0.0329 |     784 B |        0.10 |
| Optimized_SendInputBatch   | 1,429.96 ns | 22.461 ns |  17.536 ns |  0.50 |    0.04 |      - |         - |        0.00 |
| Optimized_ReleaseModifiers |    81.34 ns |  1.230 ns |   1.090 ns |  0.03 |    0.00 |      - |         - |        0.00 |

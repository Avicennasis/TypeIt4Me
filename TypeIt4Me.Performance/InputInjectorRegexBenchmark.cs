using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace TypeIt4Me.Performance
{
    [MemoryDiagnoser]
    public class InputInjectorRegexBenchmark
    {
        private string _text;
        private static readonly Regex CommandPattern = new Regex(@"\{([^}]+)\}", RegexOptions.Compiled);

        [GlobalSetup]
        public void Setup()
        {
            _text = "Here is some text with {TAB} and then some more text {ENTER} and finally {SLEEP 1000} we are done.";
        }

        [Benchmark(Baseline = true)]
        public async Task StringSubstring()
        {
            await ProcessTextWithCommands_Original(_text);
        }

        [Benchmark]
        public async Task ReadOnlyMemory()
        {
            await ProcessTextWithCommands_Optimized(_text);
        }

        private Task TypePlainTextAsync_Original(string text)
        {
            return Task.CompletedTask;
        }

        private Task ProcessCommand_Original(string command)
        {
            return Task.CompletedTask;
        }

        private async Task ProcessTextWithCommands_Original(string text)
        {
            int lastIndex = 0;
            var matches = CommandPattern.Matches(text);

            foreach (Match match in matches)
            {
                if (match.Index > lastIndex)
                {
                    string beforeText = text.Substring(lastIndex, match.Index - lastIndex);
                    await TypePlainTextAsync_Original(beforeText);
                }

                string command = match.Groups[1].Value.Trim();
                await ProcessCommand_Original(command);

                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < text.Length)
            {
                string remainingText = text.Substring(lastIndex);
                await TypePlainTextAsync_Original(remainingText);
            }
        }

        private Task TypePlainTextAsync_Optimized(ReadOnlyMemory<char> text)
        {
            return Task.CompletedTask;
        }

        private Task ProcessCommand_Optimized(string command)
        {
            return Task.CompletedTask;
        }

        private async Task ProcessTextWithCommands_Optimized(string text)
        {
            int lastIndex = 0;
            var matches = CommandPattern.Matches(text);

            foreach (Match match in matches)
            {
                if (match.Index > lastIndex)
                {
                    var beforeText = text.AsMemory(lastIndex, match.Index - lastIndex);
                    await TypePlainTextAsync_Optimized(beforeText);
                }

                string command = match.Groups[1].Value.Trim();
                await ProcessCommand_Optimized(command);

                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < text.Length)
            {
                var remainingText = text.AsMemory(lastIndex);
                await TypePlainTextAsync_Optimized(remainingText);
            }
        }
    }
}

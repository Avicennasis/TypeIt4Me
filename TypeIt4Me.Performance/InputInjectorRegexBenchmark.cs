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
        private string? _text;
        private static readonly Regex CommandPattern = new Regex(@"\{([^}]+)\}", RegexOptions.Compiled);

        [GlobalSetup]
        public void Setup()
        {
            _text = "Here is some text with {TAB} and then some more text {ENTER} and finally {SLEEP 1000} we are done.";
        }

        [Benchmark(Baseline = true)]
        public async Task ReadOnlyMemoryRegex()
        {
            await ProcessTextWithCommands_Original(_text!);
        }

        [Benchmark]
        public async Task ManualScan()
        {
            await ProcessTextWithCommands_Optimized(_text!);
        }

        private Task TypePlainTextAsync_Original(ReadOnlyMemory<char> text)
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
                    var beforeText = text.AsMemory(lastIndex, match.Index - lastIndex);
                    await TypePlainTextAsync_Original(beforeText);
                }

                string command = match.Groups[1].Value.Trim();
                await ProcessCommand_Original(command);

                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < text.Length)
            {
                var remainingText = text.AsMemory(lastIndex);
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
            int currentIndex = 0;

            while (currentIndex < text.Length)
            {
                int openBraceIndex = text.IndexOf('{', currentIndex);
                if (openBraceIndex == -1)
                    break;

                int closeBraceIndex = text.IndexOf('}', openBraceIndex + 1);
                if (closeBraceIndex == -1)
                    break;

                if (closeBraceIndex == openBraceIndex + 1)
                {
                    // Empty braces "{}" - Regex [^}]+ requires at least 1 char
                    currentIndex = openBraceIndex + 1;
                    continue;
                }

                if (openBraceIndex > lastIndex)
                {
                    var beforeText = text.AsMemory(lastIndex, openBraceIndex - lastIndex);
                    await TypePlainTextAsync_Optimized(beforeText);
                }

                var commandMemory = text.AsMemory(openBraceIndex + 1, closeBraceIndex - openBraceIndex - 1);
                string command = commandMemory.ToString().Trim(); // Need ToString because ProcessCommand expects string, or we change it.
                await ProcessCommand_Optimized(command);

                lastIndex = closeBraceIndex + 1;
                currentIndex = lastIndex;
            }

            if (lastIndex < text.Length)
            {
                var remainingText = text.AsMemory(lastIndex);
                await TypePlainTextAsync_Optimized(remainingText);
            }
        }
    }
}

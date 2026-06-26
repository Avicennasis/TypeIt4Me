using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TypeIt4Me.Models;
using TypeIt4Me.Services;

namespace TypeIt4Me.Tests.Fakes
{
    public class FakeSnippetManager : ISnippetManager
    {
        public BulkObservableCollection<Snippet> Snippets { get; } = new BulkObservableCollection<Snippet>();
        public List<string> SetPinLog { get; } = new List<string>();

        public void SetPin(ReadOnlySpan<char> pin)
        {
            SetPinLog.Add(pin.ToString());
        }

        public Task SaveSnippetsAsync() => Task.CompletedTask;
        public Task LoadSnippetsAsync() => Task.CompletedTask;

        public void AddSnippet(Snippet snippet)
        {
            Snippets.Add(snippet);
        }

        public void RemoveSnippet(Snippet snippet)
        {
            Snippets.Remove(snippet);
        }

        public Task ExportSnippetsAsync(string filePath) => Task.CompletedTask;

        public Task<bool> ImportSnippetsAsync(string filePath, string pin = null) => Task.FromResult(true);
    }
}

using System;
using System.Threading.Tasks;
using TypeIt4Me.Models;

namespace TypeIt4Me.Services
{
    public interface ISnippetManager : IDisposable
    {
        BulkObservableCollection<Snippet> Snippets { get; }
        void SetPin(ReadOnlySpan<char> pin);
        Task SaveSnippetsAsync();
        Task LoadSnippetsAsync();
        Task AddSnippet(Snippet snippet);
        Task RemoveSnippet(Snippet snippet);
        Task ExportSnippetsAsync(string filePath);
        Task<bool> ImportSnippetsAsync(string filePath, char[]? pin = null);
    }
}

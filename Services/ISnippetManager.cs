using System;
using System.Threading.Tasks;
using TypeIt4Me.Models;

namespace TypeIt4Me.Services
{
    public interface ISnippetManager
    {
        BulkObservableCollection<Snippet> Snippets { get; }
        void SetPin(string pin);
        Task SaveSnippetsAsync();
        Task LoadSnippetsAsync();
        void AddSnippet(Snippet snippet);
        void RemoveSnippet(Snippet snippet);
        Task ExportSnippetsAsync(string filePath);
        Task<bool> ImportSnippetsAsync(string filePath, string? pin = null);
    }
}

using TypeIt4Me.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TypeIt4Me.Models;

namespace TypeIt4Me.ViewModels
{
    public partial class MainViewModel
    {
        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(UseSelectedSnippetCommand))]
        [NotifyCanExecuteChangedFor(nameof(EditSelectedSnippetCommand))]
        [NotifyCanExecuteChangedFor(nameof(DeleteSelectedSnippetCommand))]
        private Snippet? _selectedSnippet;

        [ObservableProperty]
        private bool _isSearching;

        [ObservableProperty]
        private bool _isInserting;

        public bool HasResults => !IsLocked && FilteredSnippets.Count > 0;
        public bool IsFirstRun => !IsLocked && _snippetManager.Snippets.Count == 0;
        public bool HasNoResults => !IsLocked && !IsFirstRun && !HasResults;
        public string ResultSummary => IsLocked ? "Locked" : IsWorking ? "Working…" : IsSearching ? "Searching…" :
            string.IsNullOrWhiteSpace(SearchText) ? $"{FilteredSnippets.Count} snippet{(FilteredSnippets.Count == 1 ? "" : "s")}" :
            $"{FilteredSnippets.Count} of {_snippetManager.Snippets.Count} snippets";

        private void NotifyResults()
        {
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(IsFirstRun));
            OnPropertyChanged(nameof(HasNoResults));
            OnPropertyChanged(nameof(ResultSummary));
            if (SelectedSnippet == null || !FilteredSnippets.Contains(SelectedSnippet))
                SelectedSnippet = FilteredSnippets.FirstOrDefault();
        }

        partial void OnIsSearchingChanged(bool value)
        {
            OnPropertyChanged(nameof(ResultSummary));
            UseSelectedSnippetCommand.NotifyCanExecuteChanged();
            EditSelectedSnippetCommand.NotifyCanExecuteChanged();
            DeleteSelectedSnippetCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsInsertingChanged(bool value) => NotifyDataCommands();

        public BulkObservableCollection<Snippet> FilteredSnippets { get; } = new BulkObservableCollection<Snippet>();

        partial void OnSearchTextChanged(string value)
        {
             // Simple Debounce: Cancel previous, start new delay
             // Using Task.Delay is simple but assumes single threading logic for UI updates which is true here.
             DebounceSearch();
        }

        private CancellationTokenSource? _searchCts;

        private void CancelPendingSearch()
        {
            var cts = Interlocked.Exchange(ref _searchCts, null);
            if (cts != null)
            {
                try { cts.Cancel(); } catch (Exception ex) { _logger.LogError("Error cancelling search", ex); }
                cts.Dispose();
            }
        }

        private async void DebounceSearch()
        {
            IsSearching = true;
            var newCts = new CancellationTokenSource();
            var oldCts = Interlocked.Exchange(ref _searchCts, newCts);
            if (oldCts != null)
            {
                try { oldCts.Cancel(); } catch (Exception ex) { _logger.LogError("Error cancelling search", ex); }
                oldCts.Dispose();
            }

            var token = newCts.Token;

            try
            {
                await Task.Delay(300, token); // 300ms delay

                string filter = SearchText;
                // Snapshot the collection (shallow copy of references) to avoid InvalidOperationException
                // if the underlying collection is modified during background enumeration.
                // Using an array allocation is significantly faster and uses less memory than .ToList().
                Snippet[] source = _snippetManager.Snippets.ToArray();

                var results = await Task.Run(() => PerformFiltering(filter, source), token);

                if (!token.IsCancellationRequested)
                {
                    FilteredSnippets.ReplaceAll(IsLocked ? Array.Empty<Snippet>() : results);
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore - this is expected when search is cancelled
            }
            catch (Exception ex)
            {
                _logger.LogError("Search error", ex);
            }
            finally
            {
                if (Interlocked.CompareExchange(ref _searchCts, null, newCts) == newCts) IsSearching = false;
                newCts.Dispose();
            }
        }

        private IEnumerable<Snippet> PerformFiltering(string filter, IEnumerable<Snippet> source)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                return source.ToList();
            }

            filter = filter.Trim();
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
                    continue;
                }

                if (s.Content?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true ||
                    s.HotkeyLabel.Contains(filter, StringComparison.OrdinalIgnoreCase)) result.Add(s);
            }
            return result;
        }

        private void Snippets_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            RefreshSnippets();
        }

        private void RefreshSnippets()
        {
            CancelPendingSearch();
            IsSearching = false;
            FilteredSnippets.ReplaceAll(IsLocked ? Array.Empty<Snippet>() : PerformFiltering(SearchText, _snippetManager.Snippets));
        }

        public void RefreshAfterEdit() => RefreshSnippets();

        [RelayCommand]
        private void ClearSearch() => SearchText = string.Empty;

        private bool CanManageSnippets() => !IsLocked && !IsDialogOpen && !IsInserting && _dataOperations == 0;
        private bool CanUseSnippet(Snippet? snippet) => CanManageSnippets() && snippet != null;
        private bool CanUseSelectedSnippet() => !IsSearching && CanUseSnippet(SelectedSnippet);

        [RelayCommand(CanExecute = nameof(CanUseSelectedSnippet))]
        private Task UseSelectedSnippet() => !CanUseSelectedSnippet() || SelectedSnippet == null ? Task.CompletedTask : TriggerSnippet(SelectedSnippet);

        [RelayCommand(CanExecute = nameof(CanUseSelectedSnippet))]
        private void EditSelectedSnippet()
        {
            if (CanUseSelectedSnippet() && SelectedSnippet != null) RequestSnippetEditor?.Invoke(SelectedSnippet);
        }

        [RelayCommand(CanExecute = nameof(CanUseSelectedSnippet))]
        private Task DeleteSelectedSnippet() => !CanUseSelectedSnippet() || SelectedSnippet == null ? Task.CompletedTask : DeleteSnippet(SelectedSnippet);

        [RelayCommand(CanExecute = nameof(CanUseSnippet))]
        private async Task TriggerSnippet(Snippet? snippet)
        {
            if (snippet == null || !CanUseSnippet(snippet)) return;
            if (FilteredSnippets.Contains(snippet)) SelectedSnippet = snippet;
            IsInserting = true;
            try
            {

                // Return to the last external target before sending input.
                IntPtr target = _focusTracker.LastExternalWindowHandle;
                if (target == IntPtr.Zero)
                {
                    ReportStatus("Choose a text field in another app, then try again.", true);
                    return;
                }

                if (NativeMethods.GetForegroundWindow() != target)
                {
                     if (!NativeMethods.SetForegroundWindow(target))
                     {
                         ReportStatus("Could not focus the target app. Select its text field and try again.", true);
                         return;
                     }
                     // Allow time for focus switch
                     await Task.Delay(200);
                }

                if (IsLocked || NativeMethods.GetForegroundWindow() != target) return;
                try
                {
                    _autoLockService.UpdateLastActivity();
                    await _inputInjector.TypeTextAsync(snippet.Content);
                    ReportStatus($"Sent “{snippet.Name}” to the active app.");
                }
                catch (Exception ex)
                {
                    _logger.LogError("Could not insert snippet", ex);
                    ReportStatus("Could not insert this snippet. Check its length and the target app.", true);
                }
            }
            finally { IsInserting = false; }
        }

        [RelayCommand(CanExecute = nameof(CanUseSnippet))]
        private async Task DeleteSnippet(Snippet snippet)
        {
            if (snippet == null || !CanManageSnippets()) return;
            if (!_dialogService.ShowConfirmation($"Delete “{snippet.Name}”? This permanently removes the snippet from this device.", "Delete snippet") || IsLocked) return;
            using var operation = BeginDataOperation();
            int index = _snippetManager.Snippets.IndexOf(snippet);
            _hotkeyManager.UnregisterBySnippetId(snippet.Id);
            try
            {
                await _snippetManager.RemoveSnippet(snippet);
                ReportStatus("Snippet deleted.");
            }
            catch (Exception ex)
            {
                if (index >= 0 && !_snippetManager.Snippets.Contains(snippet))
                    _snippetManager.Snippets.Insert(Math.Min(index, _snippetManager.Snippets.Count), snippet);
                _logger.LogError("Could not delete snippet", ex);
                ReportStatus("Could not save the deletion. The snippet is still available; check the data folder and try again.", true);
            }
        }

        [RelayCommand(CanExecute = nameof(CanManageSnippets))]
        private async Task AddSnippet()
        {
            // Ask the view to open the editor for a brand-new snippet.
            // The view (App.xaml.cs) subscribes to RequestSnippetEditor and is
            // the only layer that knows about WPF windows — this keeps the
            // ViewModel free of UI dependencies and unit-testable in isolation.
            if (CanManageSnippets()) RequestSnippetEditor?.Invoke(new Snippet());
            await Task.CompletedTask;
        }

        [RelayCommand(CanExecute = nameof(CanUseSnippet))]
        private async Task EditSnippet(Snippet snippet)
        {
            if (snippet == null || !CanManageSnippets()) return;
            RequestSnippetEditor?.Invoke(snippet);
            await Task.CompletedTask;
        }

        [RelayCommand(CanExecute = nameof(CanManageSnippets))]
        private async Task ExportSnippets()
        {
            if (!CanManageSnippets()) return;
            using var operation = BeginDataOperation();
            string? filePath = _dialogService.SaveFileDialog("JSON Files (*.json)|*.json", ".json", "snippets_export");
            if (!string.IsNullOrEmpty(filePath) && !IsLocked)
            {
                await _snippetManager.ExportSnippetsAsync(filePath);
                ReportStatus(HasPin ? "Encrypted export saved." : "Export saved as plain text.");
            }
        }

        [RelayCommand(CanExecute = nameof(CanManageSnippets))]
        private async Task ImportSnippets()
        {
            if (!CanManageSnippets()) return;
            using var operation = BeginDataOperation();
            string? filePath = _dialogService.OpenFileDialog("JSON Files (*.json)|*.json", ".json");
            if (string.IsNullOrEmpty(filePath) || IsLocked)
                return;

            bool success = await _snippetManager.ImportSnippetsAsync(filePath);
            if (success)
            {
                ShowImportSuccessfulMessage();
                return;
            }

            await TryImportWithPinAsync(filePath);
        }

        private async Task TryImportWithPinAsync(string fileName)
        {
            bool success = false;
            while (!success)
            {
                if (IsLocked) break;
                string message = "Failed to decrypt snippets. Do you want to try entering a PIN?";
                string caption = "Import Failed";
                bool userWantsToTryPin = _dialogService.ShowConfirmation(message, caption);

                if (!userWantsToTryPin)
                    break;

                char[]? inputPin = RequestPinCopy();

                if (inputPin == null || inputPin.Length == 0)
                    break;

                try
                {
                    if (IsLocked) break;
                    success = await _snippetManager.ImportSnippetsAsync(fileName, inputPin);
                    if (success)
                    {
                        ShowImportSuccessfulMessage();
                    }
                }
                finally
                {
                    Array.Clear(inputPin, 0, inputPin.Length);
                }
            }
        }

        private void ShowImportSuccessfulMessage()
        {
            _dialogService.ShowInformation("Import Successful!", "Import");
        }

        public event Action<Snippet>? RequestSnippetEditor;
    }
}

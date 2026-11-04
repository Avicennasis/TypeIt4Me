using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Threading;
using System.Threading.Tasks;
using TypeIt4Me.Services;

namespace TypeIt4Me.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ISnippetManager _snippetManager;
        private readonly IHotkeyManager _hotkeyManager;
        private readonly IInputInjector _inputInjector;
        private readonly IFocusTracker _focusTracker;
        private readonly ISettingsManager _settingsManager;
        private readonly IAutoLockService _autoLockService;
        private readonly IThemeService _themeService;
        private readonly ILogger _logger;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private string _statusMessage = "Select a text field in another app, then insert a snippet.";

        [ObservableProperty]
        private bool _statusIsError;

        [ObservableProperty]
        private bool _isDialogOpen;

        partial void OnIsDialogOpenChanged(bool value) => NotifyDataCommands();

        private bool _applyingSettings;
        private int _dataOperations;
        private bool _clearPinAfterOperation;

        public IDisposable BeginDataOperation()
        {
            _dataOperations++;
            NotifyDataCommands();
            return new DataOperation(() =>
            {
                _dataOperations--;
                if (_dataOperations == 0 && _clearPinAfterOperation)
                {
                    _clearPinAfterOperation = false;
                    _snippetManager.SetPin(ReadOnlySpan<char>.Empty);
                }
                NotifyDataCommands();
            });
        }

        private sealed class DataOperation : IDisposable
        {
            private Action? _end;
            public DataOperation(Action end) => _end = end;
            public void Dispose() => Interlocked.Exchange(ref _end, null)?.Invoke();
        }

        private void NotifyDataCommands()
        {
            OnPropertyChanged(nameof(IsWorking));
            OnPropertyChanged(nameof(ResultSummary));
            AddSnippetCommand.NotifyCanExecuteChanged();
            EditSnippetCommand.NotifyCanExecuteChanged();
            DeleteSnippetCommand.NotifyCanExecuteChanged();
            ImportSnippetsCommand.NotifyCanExecuteChanged();
            ExportSnippetsCommand.NotifyCanExecuteChanged();
            SetPinCommand.NotifyCanExecuteChanged();
            RemovePinCommand.NotifyCanExecuteChanged();
            TriggerSnippetCommand.NotifyCanExecuteChanged();
            UseSelectedSnippetCommand.NotifyCanExecuteChanged();
            EditSelectedSnippetCommand.NotifyCanExecuteChanged();
            DeleteSelectedSnippetCommand.NotifyCanExecuteChanged();
            UnlockCommand.NotifyCanExecuteChanged();
        }

        public Task Initialization { get; }
        public bool IsWorking => _dataOperations > 0 || IsInserting;
        public bool IsLightMode => !IsDarkMode;

        public void ReportStatus(string message, bool isError = false)
        {
            StatusMessage = message;
            StatusIsError = isError;
        }

        public MainViewModel(ISnippetManager snippetManager, IHotkeyManager hotkeyManager, IInputInjector inputInjector,
                             IFocusTracker focusTracker, ISettingsManager settingsManager,
                             IAutoLockService autoLockService, IThemeService themeService, ILogger logger,
                             IDialogService? dialogService = null)
        {
            _snippetManager = snippetManager;
            _hotkeyManager = hotkeyManager;
            _inputInjector = inputInjector;
            _focusTracker = focusTracker;
            _settingsManager = settingsManager;
            _autoLockService = autoLockService;
            _themeService = themeService;
            _logger = logger;
            _dialogService = dialogService ?? new DialogService();

            _autoLockService.OnLockTriggered += LockApp;

            Initialization = LoadSettings();

            _snippetManager.Snippets.CollectionChanged += Snippets_CollectionChanged;
            FilteredSnippets.CollectionChanged += (_, _) => NotifyResults();
            RefreshSnippets();
        }
    }
}

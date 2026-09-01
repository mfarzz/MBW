using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MBW.Core.Interfaces;
using MBW.Core.Services;
using Microsoft.UI.Xaml;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MBW.App.ViewModels
{
    public partial class DatabaseViewModel : ObservableObject
    {
        public const int DefaultPageSize = 50;

        private readonly IExcelImporter _excelImporter;
        private readonly WorkspaceCoordinator _workspaceCoordinator;

        private string? _loadedPath;
        private string? _loadedSheetName;
        private int _loadedHeaderRow;
        private DateTime _loadedFileWriteTimeUtc;
        private bool _isInitialized;
        private int _loadGeneration;
        private bool _suppressWorkspaceReload;

        public DatabaseViewModel(IExcelImporter excelImporter, WorkspaceCoordinator workspaceCoordinator)
        {
            _excelImporter = excelImporter;
            _workspaceCoordinator = workspaceCoordinator;
            _workspaceCoordinator.Changed += (_, _) =>
            {
                OnPropertyChanged(nameof(HasWorkspace));
                OnPropertyChanged(nameof(CanImport));
                ImportExcelCommand.NotifyCanExecuteChanged();
                InvalidateCache();
                if (!_suppressWorkspaceReload)
                {
                    _ = EnsureLoadedAsync(force: true);
                }
            };
        }

        public Func<Task<string?>>? PickExcelFileAsync { get; set; }

        public Func<string, Task<ExcelImportSelection?>>? ShowImportDialogAsync { get; set; }

        public Func<string, Task<bool>>? ConfirmOverwriteAsync { get; set; }

        public ObservableCollection<string> ColumnHeaders { get; } = new();

        public ObservableCollection<DatabasePreviewRow> PreviewRows { get; } = new();

        [ObservableProperty]
        public partial string StatusMessage { get; set; } = "No database yet. Import an Excel file to get started.";

        [ObservableProperty]
        public partial string FileName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string SheetName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial long TotalRows { get; set; }

        [ObservableProperty]
        public partial int ColumnCount { get; set; }

        [ObservableProperty]
        public partial int CurrentPage { get; set; } = 1;

        [ObservableProperty]
        public partial int TotalPages { get; set; }

        [ObservableProperty]
        public partial bool HasData { get; set; }

        [ObservableProperty]
        public partial bool IsBusy { get; set; }

        public bool HasWorkspace => _workspaceCoordinator.HasWorkspace;

        public bool CanImport => !IsBusy && HasWorkspace;

        public bool CanGoPrevious => !IsBusy && HasData && CurrentPage > 1;

        public bool CanGoNext => !IsBusy && HasData && CurrentPage < TotalPages;

        public Visibility BusyVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;

        [ObservableProperty]
        public partial Visibility EmptyVisibility { get; set; } = Visibility.Visible;

        [ObservableProperty]
        public partial Visibility PreviewVisibility { get; set; } = Visibility.Collapsed;

        [ObservableProperty]
        public partial Visibility NoWorkspaceVisibility { get; set; } = Visibility.Collapsed;

        public string PageCaption
        {
            get
            {
                if (!HasData || TotalRows == 0)
                {
                    return string.Empty;
                }

                var start = ((CurrentPage - 1) * DefaultPageSize) + 1;
                var end = Math.Min(CurrentPage * DefaultPageSize, TotalRows);
                return $"Rows {start:N0}–{end:N0} of {TotalRows:N0} · Page {CurrentPage} / {Math.Max(1, TotalPages)}";
            }
        }

        public string FileSummary
        {
            get
            {
                if (string.IsNullOrEmpty(FileName))
                {
                    return string.Empty;
                }

                var sheet = string.IsNullOrEmpty(SheetName) ? string.Empty : $" · Sheet: {SheetName}";
                return $"File: {FileName}{sheet} · {ColumnCount} columns · header row {_loadedHeaderRow}";
            }
        }

        partial void OnFileNameChanged(string value) => OnPropertyChanged(nameof(FileSummary));

        partial void OnSheetNameChanged(string value) => OnPropertyChanged(nameof(FileSummary));

        partial void OnColumnCountChanged(int value) => OnPropertyChanged(nameof(FileSummary));

        partial void OnCurrentPageChanged(int value)
        {
            OnPropertyChanged(nameof(PageCaption));
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
            PreviousPageCommand.NotifyCanExecuteChanged();
            NextPageCommand.NotifyCanExecuteChanged();
        }

        partial void OnTotalPagesChanged(int value)
        {
            OnPropertyChanged(nameof(PageCaption));
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
            PreviousPageCommand.NotifyCanExecuteChanged();
            NextPageCommand.NotifyCanExecuteChanged();
        }

        partial void OnTotalRowsChanged(long value) => OnPropertyChanged(nameof(PageCaption));

        partial void OnHasDataChanged(bool value)
        {
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
            PreviousPageCommand.NotifyCanExecuteChanged();
            NextPageCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsBusyChanged(bool value)
        {
            OnPropertyChanged(nameof(CanImport));
            OnPropertyChanged(nameof(BusyVisibility));
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
            ImportExcelCommand.NotifyCanExecuteChanged();
            PreviousPageCommand.NotifyCanExecuteChanged();
            NextPageCommand.NotifyCanExecuteChanged();
        }

        public Task EnsureLoadedAsync(bool force = false) => LoadPageAsync(force ? 1 : CurrentPage, force);

        [RelayCommand(CanExecute = nameof(CanImport))]
        private async Task ImportExcelAsync()
        {
            if (!_workspaceCoordinator.HasWorkspace)
            {
                StatusMessage = "Create or open a workspace first.";
                return;
            }

            if (PickExcelFileAsync is null || ShowImportDialogAsync is null)
            {
                StatusMessage = "Import dialog is not available.";
                return;
            }

            var pickedPath = await PickExcelFileAsync();
            if (string.IsNullOrWhiteSpace(pickedPath))
            {
                return;
            }

            var extension = Path.GetExtension(pickedPath);
            if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(extension, ".xlsm", StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = "Unsupported format. Use a .xlsx or .xlsm file.";
                return;
            }

            var selection = await ShowImportDialogAsync(pickedPath);
            if (selection is null)
            {
                StatusMessage = "Import cancelled.";
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = "Importing Excel file...";
                _suppressWorkspaceReload = true;

                var workspacePath = _workspaceCoordinator.WorkspacePath!;
                var dataDir = Path.Combine(workspacePath, "data");
                Directory.CreateDirectory(dataDir);

                var fileName = Path.GetFileName(selection.SourcePath);
                var destinationPath = Path.Combine(dataDir, fileName);

                if (File.Exists(destinationPath))
                {
                    var overwrite = ConfirmOverwriteAsync is null
                        || await ConfirmOverwriteAsync(fileName);
                    if (!overwrite)
                    {
                        StatusMessage = "Import cancelled. Existing file was not changed.";
                        return;
                    }
                }

                File.Copy(selection.SourcePath, destinationPath, overwrite: true);

                var relativePath = Path.Combine("data", fileName).Replace('\\', '/');
                InvalidateCache();
                _workspaceCoordinator.UpdateDataFilePath(relativePath, selection.SheetName, selection.HeaderRow);
                await _workspaceCoordinator.SaveCurrentAsync();
                await LoadPageAsync(1, force: true);
                StatusMessage = $"Successfully imported {fileName} (sheet \"{selection.SheetName}\").";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Import failed: {ex.Message}";
            }
            finally
            {
                _suppressWorkspaceReload = false;
                IsBusy = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanGoPrevious))]
        private async Task PreviousPageAsync()
        {
            if (CurrentPage <= 1)
            {
                return;
            }

            await LoadPageAsync(CurrentPage - 1, force: true);
        }

        [RelayCommand(CanExecute = nameof(CanGoNext))]
        private async Task NextPageAsync()
        {
            if (CurrentPage >= TotalPages)
            {
                return;
            }

            await LoadPageAsync(CurrentPage + 1, force: true);
        }

        private void InvalidateCache()
        {
            _loadedPath = null;
            _loadedSheetName = null;
            _loadedHeaderRow = 0;
            _loadedFileWriteTimeUtc = default;
            _isInitialized = false;
        }

        private async Task LoadPageAsync(int page, bool force)
        {
            NoWorkspaceVisibility = Visibility.Collapsed;

            if (!_workspaceCoordinator.HasWorkspace)
            {
                ClearUi("Create or open a workspace first.");
                NoWorkspaceVisibility = Visibility.Visible;
                return;
            }

            var dataPath = _workspaceCoordinator.GetResolvedDataFilePath();
            if (string.IsNullOrWhiteSpace(dataPath))
            {
                ClearUi("No database yet. Import an Excel file to get started.");
                return;
            }

            var sheetName = _workspaceCoordinator.GetDataSheetName();
            var headerRow = _workspaceCoordinator.GetDataHeaderRow();
            var writeTime = File.GetLastWriteTimeUtc(dataPath);

            if (!force
                && _isInitialized
                && string.Equals(_loadedPath, dataPath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(_loadedSheetName, sheetName, StringComparison.Ordinal)
                && _loadedHeaderRow == headerRow
                && _loadedFileWriteTimeUtc == writeTime
                && page == CurrentPage
                && HasData)
            {
                return;
            }

            try
            {
                var loadId = ++_loadGeneration;
                IsBusy = true;
                if (!_isInitialized || !HasData)
                {
                    StatusMessage = "Loading database...";
                }

                var result = await _excelImporter.GetPageAsync(
                    dataPath,
                    page,
                    DefaultPageSize,
                    sheetName,
                    headerRow);

                if (loadId != _loadGeneration)
                {
                    return;
                }

                ColumnHeaders.Clear();
                PreviewRows.Clear();

                foreach (var header in result.Headers)
                {
                    ColumnHeaders.Add(header);
                }

                foreach (var row in result.Rows)
                {
                    var cells = result.Headers
                        .Select(header => row.Get(header) ?? string.Empty)
                        .ToList();
                    PreviewRows.Add(new DatabasePreviewRow(cells));
                }

                FileName = Path.GetFileName(dataPath);
                SheetName = sheetName ?? string.Empty;
                ColumnCount = ColumnHeaders.Count;
                TotalRows = result.TotalRows;
                CurrentPage = result.Page;
                TotalPages = Math.Max(result.TotalPages, result.TotalRows > 0 ? 1 : 0);
                HasData = ColumnHeaders.Count > 0;
                EmptyVisibility = HasData ? Visibility.Collapsed : Visibility.Visible;
                PreviewVisibility = HasData ? Visibility.Visible : Visibility.Collapsed;

                _loadedPath = dataPath;
                _loadedSheetName = sheetName;
                _loadedHeaderRow = headerRow;
                _loadedFileWriteTimeUtc = writeTime;
                _isInitialized = true;

                StatusMessage = HasData
                    ? $"Database siap: {TotalRows:N0} penerima dari {FileName}."
                    : "The selected sheet has no columns.";
                OnPropertyChanged(nameof(FileSummary));
                OnPropertyChanged(nameof(PageCaption));
            }
            catch (Exception ex)
            {
                ClearUi($"Failed to load database: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ClearUi(string message)
        {
            ColumnHeaders.Clear();
            PreviewRows.Clear();
            FileName = string.Empty;
            SheetName = string.Empty;
            TotalRows = 0;
            ColumnCount = 0;
            CurrentPage = 1;
            TotalPages = 0;
            HasData = false;
            EmptyVisibility = Visibility.Visible;
            PreviewVisibility = Visibility.Collapsed;
            StatusMessage = message;
            InvalidateCache();
            OnPropertyChanged(nameof(FileSummary));
            OnPropertyChanged(nameof(PageCaption));
        }
    }
}

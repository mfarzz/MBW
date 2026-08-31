using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MBW.Core.Interfaces;
using MBW.Core.Models;
using MBW.Core.Services;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MBW.App.ViewModels
{
    public enum AttachmentExplorerLocation
    {
        Root,
        Shared,
        IndividualFolder
    }

    public partial class AttachmentsViewModel : ObservableObject
    {
        private readonly WorkspaceCoordinator _workspaceCoordinator;
        private readonly IAttachmentService _attachmentService;
        private readonly Stack<ExplorerSnapshot> _backStack = new();
        private readonly Stack<ExplorerSnapshot> _forwardStack = new();
        private readonly List<AttachmentItemViewModel> _allItems = new();

        private bool _suppressSave;
        private bool _suppressWorkspaceReload;
        private bool _isLoading;
        private bool _isInitialized;
        private string? _loadedWorkspacePath;
        private AttachmentExplorerLocation _location = AttachmentExplorerLocation.Root;
        private string? _currentFolderPath;
        private string? _currentIndividualFolderName;
        private AttachmentSortColumn _sortColumn = AttachmentSortColumn.Name;
        private bool _sortAscending = true;

        public AttachmentsViewModel(
            WorkspaceCoordinator workspaceCoordinator,
            IAttachmentService attachmentService)
        {
            _workspaceCoordinator = workspaceCoordinator;
            _attachmentService = attachmentService;

            _workspaceCoordinator.Changed += (_, _) =>
            {
                OnPropertyChanged(nameof(HasWorkspace));
                OnPropertyChanged(nameof(NoWorkspaceVisibility));
                OnPropertyChanged(nameof(ToggleVisibility));
                OnPropertyChanged(nameof(ContentVisibility));

                var path = _workspaceCoordinator.WorkspacePath;
                if (!_suppressWorkspaceReload && !string.Equals(path, _loadedWorkspacePath, StringComparison.OrdinalIgnoreCase))
                {
                    _isInitialized = false;
                    _ = EnsureLoadedAsync(force: true);
                }
            };
        }

        public ObservableCollection<AttachmentItemViewModel> Items { get; } = new();

        public ObservableCollection<BreadcrumbSegmentViewModel> BreadcrumbSegments { get; } = new();

        public Func<Task<string?>>? PickFolderAsync { get; set; }

        public Func<Task<IReadOnlyList<string>>>? PickFilesAsync { get; set; }

        public Func<Task<string?>>? PromptFolderNameAsync { get; set; }

        public Func<string, Task<bool>>? ConfirmImportFolderAsync { get; set; }

        public Func<string, Task<bool>>? ConfirmDeleteAsync { get; set; }

        [ObservableProperty]
        public partial bool IsEnabled { get; set; }

        [ObservableProperty]
        public partial bool IsBusy { get; set; }

        [ObservableProperty]
        public partial string StatusMessage { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string SearchQuery { get; set; } = string.Empty;

        [ObservableProperty]
        public partial AttachmentItemViewModel? SelectedItem { get; set; }

        public bool HasWorkspace => _workspaceCoordinator.HasWorkspace;

        public bool CanGoBack => _backStack.Count > 0;

        public bool CanGoForward => _forwardStack.Count > 0;

        public bool CanGoUp => _location != AttachmentExplorerLocation.Root;

        public bool IsInsideFolder => _location != AttachmentExplorerLocation.Root;

        public string ImportButtonLabel => IsInsideFolder ? "Import file" : "Import folder";

        public string SearchPlaceholder => IsInsideFolder
            ? "Cari file di folder ini"
            : "Cari folder";

        public Visibility BusyVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;

        public Visibility NoWorkspaceVisibility => HasWorkspace ? Visibility.Collapsed : Visibility.Visible;

        public Visibility ToggleVisibility => HasWorkspace ? Visibility.Visible : Visibility.Collapsed;

        public Visibility ContentVisibility =>
            HasWorkspace && IsEnabled ? Visibility.Visible : Visibility.Collapsed;

        public Visibility CreateFolderVisibility =>
            _location == AttachmentExplorerLocation.Root ? Visibility.Visible : Visibility.Collapsed;

        public Visibility EmptyVisibility =>
            Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public string EmptyTitle => _location switch
        {
            AttachmentExplorerLocation.Shared => "Belum ada file di folder shared",
            AttachmentExplorerLocation.IndividualFolder => "Belum ada file di folder ini",
            _ => string.IsNullOrWhiteSpace(SearchQuery) ? "Belum ada folder individual" : "Tidak ada hasil pencarian"
        };

        public string NameSortIndicator => GetSortIndicator(AttachmentSortColumn.Name);

        public string TypeSortIndicator => GetSortIndicator(AttachmentSortColumn.Type);

        public string SizeSortIndicator => GetSortIndicator(AttachmentSortColumn.Size);

        public string ModifiedSortIndicator => GetSortIndicator(AttachmentSortColumn.Modified);

        public Visibility NameSortVisibility => GetSortVisibility(AttachmentSortColumn.Name);

        public Visibility TypeSortVisibility => GetSortVisibility(AttachmentSortColumn.Type);

        public Visibility SizeSortVisibility => GetSortVisibility(AttachmentSortColumn.Size);

        public Visibility ModifiedSortVisibility => GetSortVisibility(AttachmentSortColumn.Modified);

        partial void OnIsEnabledChanged(bool value)
        {
            OnPropertyChanged(nameof(ContentVisibility));
            if (!_suppressSave)
            {
                ScheduleSave();
            }
        }

        partial void OnSearchQueryChanged(string value) => ApplyFilterAndSort();

        partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(BusyVisibility));

        public Task EnsureLoadedAsync(bool force = false)
        {
            if (!force && _isInitialized)
            {
                return Task.CompletedTask;
            }

            return LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (_isLoading)
            {
                return;
            }

            _isLoading = true;

            if (!_workspaceCoordinator.HasWorkspace)
            {
                _suppressSave = true;
                IsEnabled = false;
                ResetNavigation(clearHistory: true);
                _allItems.Clear();
                Items.Clear();
                StatusMessage = "Buat atau buka workspace terlebih dahulu.";
                _suppressSave = false;
                _isInitialized = true;
                _loadedWorkspacePath = null;
                NotifyExplorerState();
                _isLoading = false;
                return;
            }

            try
            {
                IsBusy = true;
                _suppressSave = true;
                _workspaceCoordinator.EnsureAttachmentDirectories();

                var config = _workspaceCoordinator.GetAttachmentConfiguration();
                IsEnabled = config.Enabled;

                ResetNavigation(clearHistory: true);
                await LoadItemsFromDiskAsync();
                StatusMessage = IsEnabled
                    ? "Kelola folder dan file lampiran di workspace."
                    : "Lampiran email nonaktif.";
                _isInitialized = true;
                _loadedWorkspacePath = _workspaceCoordinator.WorkspacePath;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Gagal memuat lampiran: {ex.Message}";
            }
            finally
            {
                _suppressSave = false;
                IsBusy = false;
                _isLoading = false;
                NotifyExplorerState();
            }
        }

        private void ResetNavigation(bool clearHistory)
        {
            _location = AttachmentExplorerLocation.Root;
            _currentFolderPath = null;
            _currentIndividualFolderName = null;
            if (clearHistory)
            {
                _backStack.Clear();
                _forwardStack.Clear();
            }

            RebuildBreadcrumbs();
            NotifyNavigationState();
        }

        private async Task LoadItemsFromDiskAsync()
        {
            _allItems.Clear();

            switch (_location)
            {
                case AttachmentExplorerLocation.Root:
                    _allItems.Add(AttachmentItemViewModel.CreateSharedFolder(
                        _workspaceCoordinator.GetSharedAttachmentsDirectory()));

                    var individualRoot = _workspaceCoordinator.GetIndividualAttachmentsDirectory();
                    var folders = await _attachmentService.ListDirectoryEntriesAsync(individualRoot, directoriesOnly: true);
                    foreach (var folder in folders)
                    {
                        _allItems.Add(AttachmentItemViewModel.FromEntry(folder, AttachmentItemType.IndividualFolder));
                    }

                    break;

                case AttachmentExplorerLocation.Shared:
                    var sharedEntries = await _attachmentService.ListDirectoryEntriesAsync(
                        _workspaceCoordinator.GetSharedAttachmentsDirectory());
                    foreach (var entry in sharedEntries.Where(e => !e.IsDirectory))
                    {
                        _allItems.Add(AttachmentItemViewModel.FromEntry(entry, AttachmentItemType.File));
                    }

                    break;

                case AttachmentExplorerLocation.IndividualFolder when !string.IsNullOrWhiteSpace(_currentFolderPath):
                    var fileEntries = await _attachmentService.ListDirectoryEntriesAsync(_currentFolderPath);
                    foreach (var entry in fileEntries.Where(e => !e.IsDirectory))
                    {
                        _allItems.Add(AttachmentItemViewModel.FromEntry(entry, AttachmentItemType.File));
                    }

                    break;
            }

            ApplyFilterAndSort();
        }

        private void ApplyFilterAndSort()
        {
            IEnumerable<AttachmentItemViewModel> query = _allItems;

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var term = SearchQuery.Trim();
                query = query.Where(item =>
                    item.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || item.TypeLabel.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            query = _sortColumn switch
            {
                AttachmentSortColumn.Type => _sortAscending
                    ? query.OrderBy(i => i.TypeLabel, StringComparer.OrdinalIgnoreCase).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                    : query.OrderByDescending(i => i.TypeLabel, StringComparer.OrdinalIgnoreCase).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
                AttachmentSortColumn.Size => _sortAscending
                    ? query.OrderBy(i => i.IsFolder).ThenBy(i => i.SizeBytes ?? -1).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                    : query.OrderBy(i => i.IsFolder).ThenByDescending(i => i.SizeBytes ?? -1).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
                AttachmentSortColumn.Modified => _sortAscending
                    ? query.OrderBy(i => i.ModifiedAt ?? DateTimeOffset.MinValue).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                    : query.OrderByDescending(i => i.ModifiedAt ?? DateTimeOffset.MinValue).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
                _ => _sortAscending
                    ? query.OrderBy(i => i.IsFolder ? 0 : 1).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                    : query.OrderByDescending(i => i.IsFolder ? 0 : 1).ThenByDescending(i => i.Name, StringComparer.OrdinalIgnoreCase)
            };

            Items.Clear();
            foreach (var item in query)
            {
                Items.Add(item);
            }

            NotifyExplorerState();
        }

        private string GetSortIndicator(AttachmentSortColumn column) =>
            _sortColumn == column ? (_sortAscending ? "\uE70E" : "\uE70D") : string.Empty;

        private Visibility GetSortVisibility(AttachmentSortColumn column) =>
            _sortColumn == column ? Visibility.Visible : Visibility.Collapsed;

        private void NotifySortIndicators()
        {
            OnPropertyChanged(nameof(NameSortIndicator));
            OnPropertyChanged(nameof(TypeSortIndicator));
            OnPropertyChanged(nameof(SizeSortIndicator));
            OnPropertyChanged(nameof(ModifiedSortIndicator));
            OnPropertyChanged(nameof(NameSortVisibility));
            OnPropertyChanged(nameof(TypeSortVisibility));
            OnPropertyChanged(nameof(SizeSortVisibility));
            OnPropertyChanged(nameof(ModifiedSortVisibility));
        }

        [RelayCommand]
        private void SortByName() => SetSortColumn(AttachmentSortColumn.Name);

        [RelayCommand]
        private void SortByType() => SetSortColumn(AttachmentSortColumn.Type);

        [RelayCommand]
        private void SortBySize() => SetSortColumn(AttachmentSortColumn.Size);

        [RelayCommand]
        private void SortByModified() => SetSortColumn(AttachmentSortColumn.Modified);

        private void SetSortColumn(AttachmentSortColumn column)
        {
            if (_sortColumn == column)
            {
                _sortAscending = !_sortAscending;
            }
            else
            {
                _sortColumn = column;
                _sortAscending = true;
            }

            NotifySortIndicators();
            ApplyFilterAndSort();
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            if (!_workspaceCoordinator.HasWorkspace)
            {
                return;
            }

            try
            {
                IsBusy = true;
                await LoadItemsFromDiskAsync();
                StatusMessage = "Daftar lampiran diperbarui.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Gagal memuat ulang: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task CreateFolderAsync()
        {
            if (!_workspaceCoordinator.HasWorkspace
                || PromptFolderNameAsync is null
                || _location != AttachmentExplorerLocation.Root)
            {
                return;
            }

            var folderName = await PromptFolderNameAsync();
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return;
            }

            folderName = SanitizeFolderName(folderName.Trim());
            if (string.IsNullOrWhiteSpace(folderName))
            {
                StatusMessage = "Nama folder tidak valid.";
                return;
            }

            if (string.Equals(folderName, "shared", StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = "Nama \"shared\" sudah dipakai untuk folder sistem.";
                return;
            }

            var destination = Path.Combine(_workspaceCoordinator.GetIndividualAttachmentsDirectory(), folderName);
            if (Directory.Exists(destination))
            {
                StatusMessage = $"Folder \"{folderName}\" sudah ada.";
                return;
            }

            try
            {
                IsBusy = true;
                await _attachmentService.CreateFolderAsync(destination);
                await LoadItemsFromDiskAsync();
                StatusMessage = $"Folder individual \"{folderName}\" dibuat.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Gagal membuat folder: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ImportAsync()
        {
            if (!_workspaceCoordinator.HasWorkspace)
            {
                return;
            }

            if (IsInsideFolder)
            {
                await ImportFilesAsync();
            }
            else
            {
                await ImportFolderAsync();
            }
        }

        private async Task ImportFolderAsync()
        {
            if (PickFolderAsync is null)
            {
                return;
            }

            var picked = await PickFolderAsync();
            if (string.IsNullOrWhiteSpace(picked))
            {
                return;
            }

            var sourceName = Path.GetFileName(picked.TrimEnd(Path.DirectorySeparatorChar));
            if (ConfirmImportFolderAsync is not null
                && !await ConfirmImportFolderAsync(sourceName))
            {
                StatusMessage = "Import folder dibatalkan.";
                return;
            }

            try
            {
                IsBusy = true;
                var destination = Path.Combine(_workspaceCoordinator.GetIndividualAttachmentsDirectory(), sourceName);
                await _attachmentService.CreateFolderAsync(destination);
                var count = await _attachmentService.ImportFolderAsync(picked, destination);
                await LoadItemsFromDiskAsync();
                StatusMessage = $"Folder individual \"{sourceName}\" dibuat dengan {count:N0} file.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Gagal import folder: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ImportFilesAsync()
        {
            if (PickFilesAsync is null || string.IsNullOrWhiteSpace(_currentFolderPath))
            {
                return;
            }

            var picked = await PickFilesAsync();
            if (picked.Count == 0)
            {
                return;
            }

            try
            {
                IsBusy = true;
                var count = 0;
                foreach (var sourcePath in picked)
                {
                    var fileName = Path.GetFileName(sourcePath);
                    var destination = Path.Combine(_currentFolderPath, fileName);
                    await _attachmentService.CopyFileAsync(sourcePath, destination);
                    count++;
                }

                await LoadItemsFromDiskAsync();
                StatusMessage = $"{count:N0} file diimpor.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Gagal import file: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task OpenItemAsync(AttachmentItemViewModel? item)
        {
            if (item is null || !item.IsFolder)
            {
                return;
            }

            PushCurrentToBackStack();
            _forwardStack.Clear();

            if (item.ItemType == AttachmentItemType.SharedFolder)
            {
                _location = AttachmentExplorerLocation.Shared;
                _currentFolderPath = item.FullPath;
                _currentIndividualFolderName = null;
            }
            else if (item.ItemType == AttachmentItemType.IndividualFolder)
            {
                _location = AttachmentExplorerLocation.IndividualFolder;
                _currentFolderPath = item.FullPath;
                _currentIndividualFolderName = item.Name;
            }
            else
            {
                return;
            }

            SelectedItem = null;
            SearchQuery = string.Empty;
            RebuildBreadcrumbs();
            NotifyNavigationState();
            await LoadItemsFromDiskAsync();
        }

        [RelayCommand(CanExecute = nameof(CanGoBack))]
        private async Task NavigateBackAsync()
        {
            if (_backStack.Count == 0)
            {
                return;
            }

            _forwardStack.Push(CreateSnapshot());
            ApplySnapshot(_backStack.Pop());
            SelectedItem = null;
            RebuildBreadcrumbs();
            NotifyNavigationState();
            await LoadItemsFromDiskAsync();
        }

        [RelayCommand(CanExecute = nameof(CanGoForward))]
        private async Task NavigateForwardAsync()
        {
            if (_forwardStack.Count == 0)
            {
                return;
            }

            _backStack.Push(CreateSnapshot());
            ApplySnapshot(_forwardStack.Pop());
            SelectedItem = null;
            RebuildBreadcrumbs();
            NotifyNavigationState();
            await LoadItemsFromDiskAsync();
        }

        [RelayCommand(CanExecute = nameof(CanGoUp))]
        private async Task NavigateUpAsync()
        {
            if (_location == AttachmentExplorerLocation.Root)
            {
                return;
            }

            PushCurrentToBackStack();
            _forwardStack.Clear();
            _location = AttachmentExplorerLocation.Root;
            _currentFolderPath = null;
            _currentIndividualFolderName = null;
            SelectedItem = null;
            RebuildBreadcrumbs();
            NotifyNavigationState();
            await LoadItemsFromDiskAsync();
        }

        [RelayCommand]
        private async Task NavigateToRootAsync()
        {
            if (_location == AttachmentExplorerLocation.Root)
            {
                return;
            }

            PushCurrentToBackStack();
            _forwardStack.Clear();
            _location = AttachmentExplorerLocation.Root;
            _currentFolderPath = null;
            _currentIndividualFolderName = null;
            SelectedItem = null;
            RebuildBreadcrumbs();
            NotifyNavigationState();
            await LoadItemsFromDiskAsync();
        }

        [RelayCommand]
        private async Task DeleteItemAsync()
        {
            if (SelectedItem is null)
            {
                StatusMessage = "Pilih item yang akan dihapus.";
                return;
            }

            if (!SelectedItem.IsDeletable)
            {
                StatusMessage = "Folder shared tidak dapat dihapus.";
                return;
            }

            if (ConfirmDeleteAsync is not null && !await ConfirmDeleteAsync(SelectedItem.Name))
            {
                StatusMessage = "Penghapusan dibatalkan.";
                return;
            }

            try
            {
                IsBusy = true;
                await _attachmentService.DeletePathAsync(SelectedItem.FullPath);
                SelectedItem = null;
                await LoadItemsFromDiskAsync();
                StatusMessage = "Item dihapus.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Gagal menghapus: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void PushCurrentToBackStack()
        {
            _backStack.Push(CreateSnapshot());
        }

        private ExplorerSnapshot CreateSnapshot() => new()
        {
            Location = _location,
            FolderPath = _currentFolderPath,
            IndividualFolderName = _currentIndividualFolderName
        };

        private void ApplySnapshot(ExplorerSnapshot snapshot)
        {
            _location = snapshot.Location;
            _currentFolderPath = snapshot.FolderPath;
            _currentIndividualFolderName = snapshot.IndividualFolderName;
        }

        private void RebuildBreadcrumbs()
        {
            BreadcrumbSegments.Clear();
            BreadcrumbSegments.Add(new BreadcrumbSegmentViewModel(
                "attachments",
                _location == AttachmentExplorerLocation.Root ? null : () => _ = NavigateToRootAsync(),
                _location == AttachmentExplorerLocation.Root));

            if (_location == AttachmentExplorerLocation.Shared)
            {
                BreadcrumbSegments.Add(new BreadcrumbSegmentViewModel("shared", null, isLast: true));
            }
            else if (_location == AttachmentExplorerLocation.IndividualFolder)
            {
                BreadcrumbSegments.Add(new BreadcrumbSegmentViewModel(
                    "individual",
                    () => _ = NavigateToRootAsync(),
                    isLast: false));
                BreadcrumbSegments.Add(new BreadcrumbSegmentViewModel(
                    _currentIndividualFolderName ?? "folder",
                    null,
                    isLast: true));
            }
        }

        private void NotifyNavigationState()
        {
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoForward));
            OnPropertyChanged(nameof(CanGoUp));
            OnPropertyChanged(nameof(IsInsideFolder));
            OnPropertyChanged(nameof(ImportButtonLabel));
            OnPropertyChanged(nameof(SearchPlaceholder));
            OnPropertyChanged(nameof(CreateFolderVisibility));
            NavigateBackCommand.NotifyCanExecuteChanged();
            NavigateForwardCommand.NotifyCanExecuteChanged();
            NavigateUpCommand.NotifyCanExecuteChanged();
        }

        private void ScheduleSave()
        {
            if (_suppressSave || _isLoading || !_workspaceCoordinator.HasWorkspace)
            {
                return;
            }

            _ = PersistAsync();
        }

        private async Task PersistAsync()
        {
            if (_suppressSave || _suppressWorkspaceReload || !_workspaceCoordinator.HasWorkspace)
            {
                return;
            }

            try
            {
                _suppressWorkspaceReload = true;
                _suppressSave = true;

                _workspaceCoordinator.UpdateAttachmentConfiguration(new AttachmentConfiguration
                {
                    Enabled = IsEnabled
                });
                await _workspaceCoordinator.SaveCurrentAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Gagal menyimpan: {ex.Message}";
            }
            finally
            {
                _suppressSave = false;
                _suppressWorkspaceReload = false;
            }
        }

        private void NotifyExplorerState()
        {
            OnPropertyChanged(nameof(EmptyVisibility));
            OnPropertyChanged(nameof(EmptyTitle));
            OnPropertyChanged(nameof(ContentVisibility));
            NotifyNavigationState();
        }

        private static string SanitizeFolderName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            return name.Trim();
        }

        private sealed class ExplorerSnapshot
        {
            public AttachmentExplorerLocation Location { get; init; }

            public string? FolderPath { get; init; }

            public string? IndividualFolderName { get; init; }
        }
    }
}

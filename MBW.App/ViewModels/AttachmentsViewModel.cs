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

        public Func<string, string, Task<string?>>? PromptRenameAsync { get; set; }

        private AttachmentClipboardEntry? _clipboard;

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
            ? "Search files in this folder"
            : "Search folders";

        public bool CanCutItem => SelectedItem?.IsDeletable == true;

        public bool CanCopyItem => SelectedItem is not null && SelectedItem.IsDeletable;

        public bool CanRenameItem => SelectedItem?.IsDeletable == true;

        public bool CanDeleteItem => SelectedItem?.IsDeletable == true;

        public bool CanPasteItem => _clipboard is not null && GetPasteDestinationFolder() is not null;

        public Visibility BusyVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;

        public Visibility NoWorkspaceVisibility => HasWorkspace ? Visibility.Collapsed : Visibility.Visible;

        public Visibility ContentVisibility =>
            HasWorkspace ? Visibility.Visible : Visibility.Collapsed;

        public Visibility CreateFolderVisibility =>
            _location == AttachmentExplorerLocation.Root ? Visibility.Visible : Visibility.Collapsed;

        public Visibility EmptyVisibility =>
            Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public string EmptyTitle => _location switch
        {
            AttachmentExplorerLocation.Shared => "No files in the shared folder yet",
            AttachmentExplorerLocation.IndividualFolder => "No files in this folder yet",
            _ => string.IsNullOrWhiteSpace(SearchQuery) ? "No individual folders yet" : "No search results"
        };

        public string NameSortIndicator => GetSortIndicator(AttachmentSortColumn.Name);

        public string TypeSortIndicator => GetSortIndicator(AttachmentSortColumn.Type);

        public string SizeSortIndicator => GetSortIndicator(AttachmentSortColumn.Size);

        public string ModifiedSortIndicator => GetSortIndicator(AttachmentSortColumn.Modified);

        public Visibility NameSortVisibility => GetSortVisibility(AttachmentSortColumn.Name);

        public Visibility TypeSortVisibility => GetSortVisibility(AttachmentSortColumn.Type);

        public Visibility SizeSortVisibility => GetSortVisibility(AttachmentSortColumn.Size);

        public Visibility ModifiedSortVisibility => GetSortVisibility(AttachmentSortColumn.Modified);

        partial void OnSearchQueryChanged(string value) => ApplyFilterAndSort();

        partial void OnSelectedItemChanged(AttachmentItemViewModel? value) => NotifyClipboardState();

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
                ResetNavigation(clearHistory: true);
                _allItems.Clear();
                Items.Clear();
                StatusMessage = "Create or open a workspace first.";
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

                ResetNavigation(clearHistory: true);
                await LoadItemsFromDiskAsync();
                StatusMessage = "Manage attachment folders and files in the workspace.";
                _isInitialized = true;
                _loadedWorkspacePath = _workspaceCoordinator.WorkspacePath;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load attachments: {ex.Message}";
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
                    item.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || item.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || item.TypeLabel.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            query = _sortColumn switch
            {
                AttachmentSortColumn.Type => _sortAscending
                    ? query.OrderBy(i => i.TypeLabel, StringComparer.OrdinalIgnoreCase).ThenBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase)
                    : query.OrderByDescending(i => i.TypeLabel, StringComparer.OrdinalIgnoreCase).ThenBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase),
                AttachmentSortColumn.Size => _sortAscending
                    ? query.OrderBy(i => i.IsFolder).ThenBy(i => i.SizeBytes ?? -1).ThenBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase)
                    : query.OrderBy(i => i.IsFolder).ThenByDescending(i => i.SizeBytes ?? -1).ThenBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase),
                AttachmentSortColumn.Modified => _sortAscending
                    ? query.OrderBy(i => i.ModifiedAt ?? DateTimeOffset.MinValue).ThenBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase)
                    : query.OrderByDescending(i => i.ModifiedAt ?? DateTimeOffset.MinValue).ThenBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase),
                _ => _sortAscending
                    ? query.OrderBy(i => i.IsFolder ? 0 : 1).ThenBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase)
                    : query.OrderByDescending(i => i.IsFolder ? 0 : 1).ThenByDescending(i => i.DisplayName, StringComparer.OrdinalIgnoreCase)
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
                StatusMessage = "Attachment list refreshed.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to refresh: {ex.Message}";
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
                StatusMessage = "Invalid folder name.";
                return;
            }

            if (string.Equals(folderName, "shared", StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = "The name \"shared\" is reserved for the system folder.";
                return;
            }

            var destination = Path.Combine(_workspaceCoordinator.GetIndividualAttachmentsDirectory(), folderName);
            if (Directory.Exists(destination))
            {
                StatusMessage = $"Folder \"{folderName}\" already exists.";
                return;
            }

            try
            {
                IsBusy = true;
                await _attachmentService.CreateFolderAsync(destination);
                await LoadItemsFromDiskAsync();
                StatusMessage = $"Individual folder \"{folderName}\" created.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to create folder: {ex.Message}";
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
                StatusMessage = "Folder import cancelled.";
                return;
            }

            try
            {
                IsBusy = true;
                var destination = Path.Combine(_workspaceCoordinator.GetIndividualAttachmentsDirectory(), sourceName);
                await _attachmentService.CreateFolderAsync(destination);
                var count = await _attachmentService.ImportFolderAsync(picked, destination);
                await LoadItemsFromDiskAsync();
                StatusMessage = $"Individual folder \"{sourceName}\" created with {count:N0} file(s).";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to import folder: {ex.Message}";
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
                StatusMessage = $"{count:N0} file(s) imported.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to import file(s): {ex.Message}";
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

        [RelayCommand(CanExecute = nameof(CanDeleteItem))]
        private async Task DeleteItemAsync()
        {
            if (SelectedItem is null)
            {
                StatusMessage = "Select an item to delete.";
                return;
            }

            if (!SelectedItem.IsDeletable)
            {
                StatusMessage = "The shared folder cannot be deleted.";
                return;
            }

            if (ConfirmDeleteAsync is not null && !await ConfirmDeleteAsync(SelectedItem.DisplayName))
            {
                StatusMessage = "Delete cancelled.";
                return;
            }

            try
            {
                IsBusy = true;
                await _attachmentService.DeletePathAsync(SelectedItem.FullPath);
                SelectedItem = null;
                await LoadItemsFromDiskAsync();
                StatusMessage = "Item deleted.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to delete: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanCutItem))]
        private void CutItem()
        {
            if (SelectedItem is null || !SelectedItem.IsDeletable)
            {
                return;
            }

            _clipboard = new AttachmentClipboardEntry(
                SelectedItem.FullPath,
                SelectedItem.Name,
                SelectedItem.IsFolder,
                isCut: true);
            StatusMessage = $"\"{SelectedItem.DisplayName}\" ready to move.";
            NotifyClipboardState();
        }

        [RelayCommand(CanExecute = nameof(CanCopyItem))]
        private void CopyItem()
        {
            if (SelectedItem is null || !SelectedItem.IsDeletable)
            {
                return;
            }

            _clipboard = new AttachmentClipboardEntry(
                SelectedItem.FullPath,
                SelectedItem.Name,
                SelectedItem.IsFolder,
                isCut: false);
            StatusMessage = $"\"{SelectedItem.DisplayName}\" copied to clipboard.";
            NotifyClipboardState();
        }

        [RelayCommand(CanExecute = nameof(CanPasteItem))]
        private async Task PasteItemAsync()
        {
            if (_clipboard is null)
            {
                return;
            }

            var destinationFolder = GetPasteDestinationFolder();
            if (string.IsNullOrWhiteSpace(destinationFolder))
            {
                StatusMessage = "Cannot paste in this location.";
                return;
            }

            try
            {
                IsBusy = true;
                var isCut = _clipboard.IsCut;
                var itemName = _clipboard.Name;

                if (isCut)
                {
                    await _attachmentService.MoveEntryAsync(_clipboard.SourcePath, destinationFolder);
                    _clipboard = null;
                }
                else
                {
                    await _attachmentService.CopyEntryAsync(_clipboard.SourcePath, destinationFolder);
                }

                await LoadItemsFromDiskAsync();
                StatusMessage = isCut
                    ? $"\"{itemName}\" moved."
                    : $"\"{itemName}\" pasted.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to paste: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
                NotifyClipboardState();
            }
        }

        [RelayCommand(CanExecute = nameof(CanRenameItem))]
        private async Task RenameItemAsync()
        {
            if (SelectedItem is null || !SelectedItem.IsDeletable || PromptRenameAsync is null)
            {
                return;
            }

            var newName = await PromptRenameAsync(SelectedItem.Name, SelectedItem.IsFolder ? "folder" : "file");
            if (string.IsNullOrWhiteSpace(newName))
            {
                return;
            }

            newName = SelectedItem.IsFolder
                ? SanitizeFolderName(newName.Trim())
                : SanitizeFileName(PreserveFileExtension(SelectedItem.Name, newName.Trim()));

            if (string.IsNullOrWhiteSpace(newName)
                || string.Equals(newName, SelectedItem.Name, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                IsBusy = true;
                await _attachmentService.RenameEntryAsync(SelectedItem.FullPath, newName);
                SelectedItem = null;
                await LoadItemsFromDiskAsync();
                StatusMessage = "Item renamed.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to rename: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private string? GetPasteDestinationFolder()
        {
            if (_clipboard is null)
            {
                return null;
            }

            if (IsInsideFolder && !string.IsNullOrWhiteSpace(_currentFolderPath))
            {
                return _currentFolderPath;
            }

            if (_location == AttachmentExplorerLocation.Root && _clipboard.IsFolder)
            {
                return _workspaceCoordinator.GetIndividualAttachmentsDirectory();
            }

            return null;
        }

        private void NotifyClipboardState()
        {
            OnPropertyChanged(nameof(CanCutItem));
            OnPropertyChanged(nameof(CanCopyItem));
            OnPropertyChanged(nameof(CanRenameItem));
            OnPropertyChanged(nameof(CanDeleteItem));
            OnPropertyChanged(nameof(CanPasteItem));
            CutItemCommand.NotifyCanExecuteChanged();
            CopyItemCommand.NotifyCanExecuteChanged();
            RenameItemCommand.NotifyCanExecuteChanged();
            DeleteItemCommand.NotifyCanExecuteChanged();
            PasteItemCommand.NotifyCanExecuteChanged();
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
            NotifyClipboardState();
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

                var existing = _workspaceCoordinator.GetAttachmentConfiguration();
                _workspaceCoordinator.UpdateAttachmentConfiguration(new AttachmentConfiguration
                {
                    Enabled = true,
                    Link = existing.Link.Clone()
                });
                await _workspaceCoordinator.SaveCurrentAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to save: {ex.Message}";
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

        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            return name.Trim();
        }

        private static string PreserveFileExtension(string currentName, string newName)
        {
            var extension = Path.GetExtension(currentName);
            if (string.IsNullOrEmpty(extension))
            {
                return newName;
            }

            if (string.IsNullOrEmpty(Path.GetExtension(newName)))
            {
                return newName + extension;
            }

            return newName;
        }

        private sealed class ExplorerSnapshot
        {
            public AttachmentExplorerLocation Location { get; init; }

            public string? FolderPath { get; init; }

            public string? IndividualFolderName { get; init; }
        }
    }
}

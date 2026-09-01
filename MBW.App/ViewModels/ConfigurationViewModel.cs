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
    public partial class ConfigurationViewModel : ObservableObject
    {
        public const int DefaultPageSize = 50;

        private readonly WorkspaceCoordinator _workspaceCoordinator;
        private readonly IAttachmentService _attachmentService;
        private readonly IExcelImporter _excelImporter;

        private bool _suppressSave;
        private bool _suppressWorkspaceReload;
        private bool _isLoading;
        private bool _isInitialized;
        private string? _loadedWorkspacePath;
        private List<ConfigurationLinkPreviewRow> _allValidationRows = new();
        private List<ConfigurationLinkPreviewRow> _filteredRows = new();

        public ConfigurationViewModel(
            WorkspaceCoordinator workspaceCoordinator,
            IAttachmentService attachmentService,
            IExcelImporter excelImporter)
        {
            _workspaceCoordinator = workspaceCoordinator;
            _attachmentService = attachmentService;
            _excelImporter = excelImporter;

            _workspaceCoordinator.Changed += (_, _) =>
            {
                OnPropertyChanged(nameof(HasWorkspace));
                OnPropertyChanged(nameof(GateMessage));
                OnPropertyChanged(nameof(GateVisibility));
                OnPropertyChanged(nameof(FormVisibility));

                if (!_suppressWorkspaceReload)
                {
                    InvalidateValidation();
                }

                MatchCommand.NotifyCanExecuteChanged();

                var path = _workspaceCoordinator.WorkspacePath;
                if (!_suppressWorkspaceReload && !string.Equals(path, _loadedWorkspacePath, StringComparison.OrdinalIgnoreCase))
                {
                    _isInitialized = false;
                    _ = EnsureLoadedAsync(force: true);
                }
            };
        }

        public ObservableCollection<string> IndividualFolders { get; } = new();

        public ObservableCollection<string> DatabaseColumns { get; } = new();

        public ObservableCollection<ConfigurationLinkPreviewRow> MatchRows { get; } = new();

        [ObservableProperty]
        public partial string StatusMessage { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsBusy { get; set; }

        [ObservableProperty]
        public partial string? SelectedIndividualFolder { get; set; }

        [ObservableProperty]
        public partial string? SelectedKeyColumn { get; set; }

        [ObservableProperty]
        public partial int MatchFilterIndex { get; set; }

        [ObservableProperty]
        public partial int CurrentPage { get; set; }

        [ObservableProperty]
        public partial int TotalPages { get; set; }

        [ObservableProperty]
        public partial int TotalResultRows { get; set; }

        [ObservableProperty]
        public partial int? MatchedCount { get; set; }

        [ObservableProperty]
        public partial int? MissingCount { get; set; }

        private string _filePattern = string.Empty;

        public bool HasWorkspace => _workspaceCoordinator.HasWorkspace;

        public bool HasDatabase => !string.IsNullOrWhiteSpace(_workspaceCoordinator.GetResolvedDataFilePath());

        public bool HasIndividualFolders => IndividualFolders.Count > 0;

        public bool HasMatchData => _allValidationRows.Count > 0;

        public bool HasValidationSummary => MatchedCount.HasValue && MissingCount.HasValue;

        public bool CanMatch =>
            !IsBusy
            && HasWorkspace
            && HasDatabase
            && HasIndividualFolders
            && !string.IsNullOrWhiteSpace(SelectedIndividualFolder)
            && !string.IsNullOrWhiteSpace(SelectedKeyColumn);

        public bool CanGoPrevious => !IsBusy && HasMatchData && CurrentPage > 1;

        public bool CanGoNext => !IsBusy && HasMatchData && CurrentPage < TotalPages;

        public string KeyColumnHeader => string.IsNullOrWhiteSpace(SelectedKeyColumn) ? "Key" : SelectedKeyColumn;

        public string? GateMessage
        {
            get
            {
                if (!HasWorkspace)
                {
                    return "Create or open a workspace first.";
                }

                if (!HasDatabase)
                {
                    return "Import an Excel database in the Database panel first.";
                }

                if (!HasIndividualFolders)
                {
                    return "Create or import an individual folder in the Attachments panel.";
                }

                return null;
            }
        }

        public string ValidationSummary =>
            HasValidationSummary
                ? $"{MatchedCount:N0} matched · {MissingCount:N0} missing"
                : string.Empty;

        public string PageCaption
        {
            get
            {
                if (!HasMatchData || TotalResultRows == 0)
                {
                    return string.Empty;
                }

                var start = ((CurrentPage - 1) * DefaultPageSize) + 1;
                var end = Math.Min(CurrentPage * DefaultPageSize, TotalResultRows);
                return $"Rows {start:N0}–{end:N0} of {TotalResultRows:N0} · Page {CurrentPage} / {Math.Max(1, TotalPages)}";
            }
        }

        public Visibility BusyVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;

        public Visibility GateVisibility => GateMessage is null ? Visibility.Collapsed : Visibility.Visible;

        public Visibility FormVisibility => GateMessage is null ? Visibility.Visible : Visibility.Collapsed;

        public Visibility TableEmptyVisibility =>
            FormVisibility == Visibility.Visible && !HasMatchData ? Visibility.Visible : Visibility.Collapsed;

        public Visibility TableVisibility =>
            FormVisibility == Visibility.Visible && HasMatchData ? Visibility.Visible : Visibility.Collapsed;

        public Visibility PaginationVisibility => TableVisibility;

        public Visibility SummaryVisibility =>
            HasValidationSummary ? Visibility.Visible : Visibility.Collapsed;

        public async Task EnsureLoadedAsync(bool force = false)
        {
            if (_isLoading)
            {
                return;
            }

            if (_isInitialized && !force)
            {
                return;
            }

            _isLoading = true;

            if (!HasWorkspace)
            {
                ResetState();
                StatusMessage = "Create or open a workspace first.";
                _isInitialized = true;
                _loadedWorkspacePath = null;
                _isLoading = false;
                NotifyAll();
                return;
            }

            try
            {
                IsBusy = true;
                _suppressSave = true;
                _workspaceCoordinator.EnsureAttachmentDirectories();

                await LoadFoldersAsync();
                await LoadDatabaseColumnsAsync();

                var link = _workspaceCoordinator.GetAttachmentConfiguration().Link;
                SelectedIndividualFolder = ResolveSelectedFolder(link.IndividualFolderName);
                SelectedKeyColumn = ResolveSelectedColumn(link.KeyColumn);
                UpdateFilePattern();

                MatchedCount = link.LastMatchedCount;
                MissingCount = link.LastMissingCount;

                var restored = false;
                if (link.LastValidatedAt.HasValue
                    && HasValidationSummary
                    && GateMessage is null
                    && !string.IsNullOrWhiteSpace(SelectedIndividualFolder)
                    && !string.IsNullOrWhiteSpace(SelectedKeyColumn))
                {
                    restored = await TryRestoreMatchResultsAsync();
                }

                if (!restored)
                {
                    StatusMessage = HasValidationSummary
                        ? $"Last result: {ValidationSummary}. Click Match to refresh the table."
                        : "Select a folder and key column, then click Match.";
                }

                _isInitialized = true;
                _loadedWorkspacePath = _workspaceCoordinator.WorkspacePath;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load configuration: {ex.Message}";
            }
            finally
            {
                _suppressSave = false;
                IsBusy = false;
                _isLoading = false;
                NotifyAll();
            }
        }

        partial void OnSelectedIndividualFolderChanged(string? value)
        {
            InvalidateValidation();
            ScheduleSave();
            MatchCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(KeyColumnHeader));
        }

        partial void OnSelectedKeyColumnChanged(string? value)
        {
            UpdateFilePattern();
            InvalidateValidation();
            ScheduleSave();
            MatchCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(KeyColumnHeader));
        }

        partial void OnMatchFilterIndexChanged(int value)
        {
            CurrentPage = 1;
            ApplyFilterAndPage();
        }

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

        partial void OnIsBusyChanged(bool value)
        {
            OnPropertyChanged(nameof(CanMatch));
            OnPropertyChanged(nameof(BusyVisibility));
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
            MatchCommand.NotifyCanExecuteChanged();
            PreviousPageCommand.NotifyCanExecuteChanged();
            NextPageCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanMatch))]
        private async Task MatchAsync()
        {
            try
            {
                IsBusy = true;
                var success = await ExecuteMatchAsync();
                if (success)
                {
                    StatusMessage = $"Match complete: {ValidationSummary}.";
                    await PersistAsync(includeValidationCache: true);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Match failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
                NotifyAll();
            }
        }

        private async Task<bool> TryRestoreMatchResultsAsync()
        {
            try
            {
                var success = await ExecuteMatchAsync();
                if (success)
                {
                    StatusMessage = $"Last result: {ValidationSummary}.";
                }

                return success;
            }
            catch
            {
                StatusMessage = HasValidationSummary
                    ? $"Last result: {ValidationSummary}. Click Match to refresh the table."
                    : StatusMessage;
                return false;
            }
        }

        private async Task<bool> ExecuteMatchAsync()
        {
            var dataPath = _workspaceCoordinator.GetResolvedDataFilePath();
            if (string.IsNullOrWhiteSpace(dataPath)
                || string.IsNullOrWhiteSpace(SelectedIndividualFolder)
                || string.IsNullOrWhiteSpace(SelectedKeyColumn))
            {
                return false;
            }

            var folderPath = Path.Combine(
                _workspaceCoordinator.GetIndividualAttachmentsDirectory(),
                SelectedIndividualFolder);

            if (!Directory.Exists(folderPath))
            {
                StatusMessage = "Attachments folder not found.";
                return false;
            }

            UpdateFilePattern();

            var recipients = new List<RecipientRow>();
            await foreach (var row in _excelImporter.ReadAllAsync(
                               dataPath,
                               _workspaceCoordinator.GetDataSheetName(),
                               _workspaceCoordinator.GetDataHeaderRow()))
            {
                recipients.Add(row);
            }

            if (recipients.Count == 0)
            {
                StatusMessage = "No data rows below the Excel header row. Check the Database panel: file imported, correct sheet, and header row aligned with data.";
                return false;
            }

            var matches = await _attachmentService.MatchByKeyColumnAsync(
                folderPath,
                recipients,
                SelectedKeyColumn);
            _allValidationRows = BuildValidationRows(recipients, matches, SelectedKeyColumn);
            MatchedCount = _allValidationRows.Count(row => row.IsMatched);
            MissingCount = _allValidationRows.Count - MatchedCount;
            CurrentPage = 1;

            ApplyFilterAndPage();
            return true;
        }

        [RelayCommand(CanExecute = nameof(CanGoPrevious))]
        private void PreviousPage()
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
                ApplyFilterAndPage();
            }
        }

        [RelayCommand(CanExecute = nameof(CanGoNext))]
        private void NextPage()
        {
            if (CurrentPage < TotalPages)
            {
                CurrentPage++;
                ApplyFilterAndPage();
            }
        }

        private Task LoadFoldersAsync()
        {
            IndividualFolders.Clear();
            var root = _workspaceCoordinator.GetIndividualAttachmentsDirectory();
            if (Directory.Exists(root))
            {
                foreach (var directory in Directory.EnumerateDirectories(root).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
                {
                    IndividualFolders.Add(Path.GetFileName(directory));
                }
            }

            return Task.CompletedTask;
        }

        private async Task LoadDatabaseColumnsAsync()
        {
            DatabaseColumns.Clear();
            var dataPath = _workspaceCoordinator.GetResolvedDataFilePath();
            if (string.IsNullOrWhiteSpace(dataPath))
            {
                return;
            }

            var headers = await _excelImporter.GetHeadersAsync(
                dataPath,
                _workspaceCoordinator.GetDataSheetName(),
                _workspaceCoordinator.GetDataHeaderRow());

            foreach (var header in headers.Where(header => !string.IsNullOrWhiteSpace(header)))
            {
                DatabaseColumns.Add(header);
            }
        }

        private string? ResolveSelectedFolder(string savedFolder)
        {
            if (string.IsNullOrWhiteSpace(savedFolder))
            {
                return IndividualFolders.FirstOrDefault();
            }

            return IndividualFolders.FirstOrDefault(folder =>
                string.Equals(folder, savedFolder, StringComparison.OrdinalIgnoreCase))
                ?? IndividualFolders.FirstOrDefault();
        }

        private string? ResolveSelectedColumn(string savedColumn)
        {
            if (string.IsNullOrWhiteSpace(savedColumn))
            {
                return DatabaseColumns.FirstOrDefault();
            }

            return DatabaseColumns.FirstOrDefault(column =>
                string.Equals(column, savedColumn, StringComparison.OrdinalIgnoreCase))
                ?? DatabaseColumns.FirstOrDefault();
        }

        private void UpdateFilePattern()
        {
            _filePattern = string.IsNullOrWhiteSpace(SelectedKeyColumn)
                ? string.Empty
                : $"{{{SelectedKeyColumn}}}.pdf";
        }

        private static List<ConfigurationLinkPreviewRow> BuildValidationRows(
            IReadOnlyList<RecipientRow> recipients,
            IReadOnlyList<AttachmentMatch> matches,
            string keyColumn)
        {
            var matchByRow = matches
                .Select(match => new
                {
                    RowNumber = long.TryParse(match.RecipientKey, out var rowNumber) ? rowNumber : -1L,
                    Match = match
                })
                .Where(entry => entry.RowNumber >= 0)
                .ToDictionary(entry => entry.RowNumber, entry => entry.Match);

            var rows = new List<ConfigurationLinkPreviewRow>(recipients.Count);
            var dataRowNumber = 0L;
            foreach (var recipient in recipients)
            {
                dataRowNumber++;
                matchByRow.TryGetValue(recipient.RowNumber, out var match);
                rows.Add(new ConfigurationLinkPreviewRow(
                    dataRowNumber,
                    recipient.Get(keyColumn) ?? "—",
                    match?.FileName ?? "—",
                    match?.Matched == true));
            }

            return rows;
        }

        private void ApplyFilterAndPage()
        {
            _filteredRows = MatchFilterIndex switch
            {
                1 => _allValidationRows.Where(row => row.IsMatched).ToList(),
                2 => _allValidationRows.Where(row => !row.IsMatched).ToList(),
                _ => _allValidationRows.ToList()
            };

            TotalResultRows = _filteredRows.Count;
            TotalPages = TotalResultRows == 0
                ? 0
                : Math.Max(1, (int)Math.Ceiling(TotalResultRows / (double)DefaultPageSize));

            if (TotalPages == 0)
            {
                CurrentPage = 1;
            }
            else if (CurrentPage > TotalPages)
            {
                CurrentPage = TotalPages;
            }
            else if (CurrentPage < 1)
            {
                CurrentPage = 1;
            }

            MatchRows.Clear();
            if (TotalResultRows > 0)
            {
                foreach (var row in _filteredRows
                             .Skip((CurrentPage - 1) * DefaultPageSize)
                             .Take(DefaultPageSize))
                {
                    MatchRows.Add(row);
                }
            }

            OnPropertyChanged(nameof(HasMatchData));
            OnPropertyChanged(nameof(PageCaption));
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(TableVisibility));
            OnPropertyChanged(nameof(TableEmptyVisibility));
            OnPropertyChanged(nameof(PaginationVisibility));
            PreviousPageCommand.NotifyCanExecuteChanged();
            NextPageCommand.NotifyCanExecuteChanged();
        }

        private void InvalidateValidation()
        {
            MatchedCount = null;
            MissingCount = null;
            _allValidationRows.Clear();
            _filteredRows.Clear();
            MatchRows.Clear();
            CurrentPage = 1;
            TotalPages = 0;
            TotalResultRows = 0;
            NotifyAll();
        }

        private void ResetState()
        {
            IndividualFolders.Clear();
            DatabaseColumns.Clear();
            MatchRows.Clear();
            _allValidationRows.Clear();
            _filteredRows.Clear();
            SelectedIndividualFolder = null;
            SelectedKeyColumn = null;
            _filePattern = string.Empty;
            MatchFilterIndex = 0;
            MatchedCount = null;
            MissingCount = null;
            CurrentPage = 1;
            TotalPages = 0;
            TotalResultRows = 0;
        }

        private void ScheduleSave()
        {
            if (_suppressSave || _isLoading || !_workspaceCoordinator.HasWorkspace)
            {
                return;
            }

            _ = PersistAsync(includeValidationCache: false);
        }

        private async Task PersistAsync(bool includeValidationCache)
        {
            if (_suppressSave || _suppressWorkspaceReload || !_workspaceCoordinator.HasWorkspace)
            {
                return;
            }

            try
            {
                _suppressWorkspaceReload = true;
                _suppressSave = true;
                UpdateFilePattern();

                var existing = _workspaceCoordinator.GetAttachmentConfiguration();
                var link = new AttachmentLinkConfiguration
                {
                    IndividualFolderName = SelectedIndividualFolder ?? string.Empty,
                    KeyColumn = SelectedKeyColumn ?? string.Empty,
                    FilePattern = _filePattern,
                    LastMatchedCount = includeValidationCache ? MatchedCount : existing.Link.LastMatchedCount,
                    LastMissingCount = includeValidationCache ? MissingCount : existing.Link.LastMissingCount,
                    LastValidatedAt = includeValidationCache ? DateTimeOffset.UtcNow : existing.Link.LastValidatedAt
                };

                _workspaceCoordinator.UpdateAttachmentConfiguration(new AttachmentConfiguration
                {
                    Enabled = true,
                    Link = link
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

        private void NotifyAll()
        {
            OnPropertyChanged(nameof(HasWorkspace));
            OnPropertyChanged(nameof(HasDatabase));
            OnPropertyChanged(nameof(HasIndividualFolders));
            OnPropertyChanged(nameof(GateMessage));
            OnPropertyChanged(nameof(GateVisibility));
            OnPropertyChanged(nameof(FormVisibility));
            OnPropertyChanged(nameof(HasMatchData));
            OnPropertyChanged(nameof(HasValidationSummary));
            OnPropertyChanged(nameof(ValidationSummary));
            OnPropertyChanged(nameof(SummaryVisibility));
            OnPropertyChanged(nameof(KeyColumnHeader));
            OnPropertyChanged(nameof(TableVisibility));
            OnPropertyChanged(nameof(TableEmptyVisibility));
            OnPropertyChanged(nameof(PaginationVisibility));
            OnPropertyChanged(nameof(PageCaption));
            OnPropertyChanged(nameof(CanMatch));
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
            MatchCommand.NotifyCanExecuteChanged();
            PreviousPageCommand.NotifyCanExecuteChanged();
            NextPageCommand.NotifyCanExecuteChanged();
        }
    }
}

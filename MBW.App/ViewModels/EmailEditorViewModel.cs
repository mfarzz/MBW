using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MBW.Core.Interfaces;
using MBW.Core.Models;
using MBW.Core.Services;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace MBW.App.ViewModels
{
    public partial class EmailEditorViewModel : ObservableObject
    {
        private readonly IWorkspaceService _workspaceService;
        private readonly IExcelImporter _excelImporter;
        private readonly WorkspaceCoordinator _workspaceCoordinator;
        private readonly SmtpSettingsCoordinator _smtpCoordinator;

        private WorkspaceModel? _currentWorkspace;
        private string _workspacePath = string.Empty;

        [ObservableProperty]
        public partial string Subject { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string HtmlBody { get; set; } = string.Empty;

        [ObservableProperty]
        public partial ObservableCollection<string> AvailableVariables { get; set; } = new();

        public ObservableCollection<string> FilteredVariables { get; } = new();

        [ObservableProperty]
        public partial string VariableSearchQuery { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string StatusMessage { get; set; } = "Ready";

        [ObservableProperty]
        public partial Visibility NoVariablesVisibility { get; set; } = Visibility.Collapsed;

        [ObservableProperty]
        public partial Visibility VariablesPanelVisibility { get; set; } = Visibility.Collapsed;

        [ObservableProperty]
        public partial Visibility NoSearchResultsVisibility { get; set; } = Visibility.Collapsed;

        [ObservableProperty]
        public partial bool IsLoading { get; set; } = false;

        public event EventHandler<SendEventArgs>? SendRequested;

        public event EventHandler<string>? EditorContentLoaded;

        public Func<Task>? PullEditorContentAsync { get; set; }

        public bool CanSend =>
            !IsLoading
            && _workspaceCoordinator.HasWorkspace
            && !string.IsNullOrWhiteSpace(_workspaceCoordinator.GetResolvedDataFilePath());

        public class SendEventArgs : EventArgs
        {
            public EmailTemplate Template { get; init; } = new();
        }

        public EmailEditorViewModel(
            IWorkspaceService workspaceService,
            IExcelImporter excelImporter,
            WorkspaceCoordinator workspaceCoordinator,
            SmtpSettingsCoordinator smtpCoordinator)
        {
            _workspaceService = workspaceService;
            _excelImporter = excelImporter;
            _workspaceCoordinator = workspaceCoordinator;
            _smtpCoordinator = smtpCoordinator;
            _workspaceCoordinator.Changed += (_, _) => _ = InitializeAsync();
            _smtpCoordinator.Changed += (_, _) =>
            {
                OnPropertyChanged(nameof(CanSend));
                SendCommand.NotifyCanExecuteChanged();
            };

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Loading workspace...";

                if (_workspaceCoordinator.HasWorkspace)
                {
                    await LoadWorkspaceAsync(_workspaceCoordinator.WorkspacePath!);
                    return;
                }

                StatusMessage = "No workspace loaded. Create or open a workspace first.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task LoadWorkspaceAsync(string workspacePath)
        {
            try
            {
                IsLoading = true;
                _workspacePath = workspacePath;
                StatusMessage = "Loading workspace...";

                _currentWorkspace = await _workspaceService.OpenAsync(workspacePath);
                Subject = _currentWorkspace.Template?.Subject ?? string.Empty;
                HtmlBody = _currentWorkspace.Template?.HtmlBody ?? string.Empty;
                EditorContentLoaded?.Invoke(this, HtmlBody);

                if (_currentWorkspace.DataFilePath is not null)
                {
                    var dataPath = _workspaceCoordinator.GetResolvedDataFilePath();
                    if (!string.IsNullOrEmpty(dataPath))
                    {
                        await LoadExcelHeadersAsync(
                            dataPath,
                            _workspaceCoordinator.GetDataSheetName(),
                            _workspaceCoordinator.GetDataHeaderRow());
                    }
                    else
                    {
                        ClearVariables();
                    }
                }
                else
                {
                    ClearVariables();
                }

                StatusMessage = "Ready";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading workspace: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(CanSend));
                SendCommand.NotifyCanExecuteChanged();
            }
        }

        private async Task LoadExcelHeadersAsync(string filePath, string? sheetName = null, int headerRow = 1)
        {
            try
            {
                var headers = await _excelImporter.GetHeadersAsync(filePath, sheetName, headerRow);
                AvailableVariables.Clear();

                foreach (var header in headers)
                {
                    AvailableVariables.Add(header);
                }

                UpdateVariablesUiState();
                StatusMessage = $"Loaded {headers.Count} variables from Excel";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading Excel: {ex.Message}";
            }
            finally
            {
                OnPropertyChanged(nameof(CanSend));
                SendCommand.NotifyCanExecuteChanged();
            }
        }

        partial void OnVariableSearchQueryChanged(string value) => RefreshFilteredVariables();

        partial void OnIsLoadingChanged(bool value)
        {
            OnPropertyChanged(nameof(CanSend));
            SendCommand.NotifyCanExecuteChanged();
        }

        private void ClearVariables()
        {
            AvailableVariables.Clear();
            VariableSearchQuery = string.Empty;
            UpdateVariablesUiState();
        }

        private void UpdateVariablesUiState()
        {
            var hasVariables = AvailableVariables.Count > 0;
            VariablesPanelVisibility = hasVariables ? Visibility.Visible : Visibility.Collapsed;
            NoVariablesVisibility = hasVariables ? Visibility.Collapsed : Visibility.Visible;
            RefreshFilteredVariables();
        }

        private void RefreshFilteredVariables()
        {
            FilteredVariables.Clear();
            var query = VariableSearchQuery?.Trim() ?? string.Empty;

            foreach (var variable in AvailableVariables)
            {
                if (string.IsNullOrEmpty(query)
                    || variable.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    FilteredVariables.Add(variable);
                }
            }

            NoSearchResultsVisibility = AvailableVariables.Count > 0 && FilteredVariables.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        [RelayCommand]
        public async Task SaveAsync()
        {
            try
            {
                if (_currentWorkspace == null || string.IsNullOrEmpty(_workspacePath))
                {
                    StatusMessage = "No workspace loaded";
                    return;
                }

                await SyncEditorContentAsync();

                IsLoading = true;
                StatusMessage = "Saving template...";

                _currentWorkspace.Template = new EmailTemplate(Subject, HtmlBody);
                await _workspaceService.SaveAsync(_currentWorkspace, _workspacePath);

                StatusMessage = $"Template saved - {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving template: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanSend))]
        public async Task SendAsync()
        {
            try
            {
                await SyncEditorContentAsync();

                if (_currentWorkspace == null)
                {
                    StatusMessage = "Workspace is not loaded.";
                    return;
                }

                var dataPath = _workspaceCoordinator.GetResolvedDataFilePath();
                if (string.IsNullOrEmpty(dataPath))
                {
                    StatusMessage = "Import an Excel database in the Database panel first.";
                    return;
                }

                var template = new EmailTemplate(Subject, HtmlBody);
                _currentWorkspace.Template = template;

                SendRequested?.Invoke(this, new SendEventArgs
                {
                    Template = template
                });

                StatusMessage = "Opened Send page.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to open Send page: {ex.Message}";
            }
        }

        public void InsertVariable(string variableName)
        {
            StatusMessage = $"Variable {{{variableName}}} ready to insert";
        }

        public async Task<EmailTemplate> GetCurrentTemplateAsync()
        {
            await SyncEditorContentAsync();
            return new EmailTemplate(Subject, HtmlBody);
        }

        public EmailTemplate GetCurrentTemplate() => new(Subject, HtmlBody);

        private async Task SyncEditorContentAsync()
        {
            if (PullEditorContentAsync is not null)
            {
                await PullEditorContentAsync();
            }
        }
    }
}

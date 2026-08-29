using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MBW.Core.Interfaces;
using MBW.Core.Models;
using MBW.Infrastructure.Excel;
using MBW.Infrastructure.Services;
using MBW.Infrastructure.Storage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace MBW.App.ViewModels
{
    public partial class EmailEditorViewModel : ObservableObject
    {
        private readonly IWorkspaceService _workspaceService;
        private readonly IExcelImporter _excelImporter;
        private readonly IStorageService _storageService;

        private WorkspaceModel? _currentWorkspace;
        private string _workspacePath = string.Empty;
        private RecipientRow? _lastPreviewRecipient;
        private EmailTemplate? _lastRenderedTemplate;

        [ObservableProperty]
        public partial string Subject { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string HtmlBody { get; set; } = string.Empty;

        [ObservableProperty]
        public partial ObservableCollection<string> AvailableVariables { get; set; } = new();

        [ObservableProperty]
        public partial string StatusMessage { get; set; } = "Ready";

        [ObservableProperty]
        public partial Microsoft.UI.Xaml.Visibility NoVariablesVisibility { get; set; } = Microsoft.UI.Xaml.Visibility.Collapsed;

        [ObservableProperty]
        public partial bool IsLoading { get; set; } = false;

        // Events for UI to subscribe to
        public event EventHandler<PreviewEventArgs>? PreviewRequested;

        public class PreviewEventArgs : EventArgs
        {
            public string FromEmail { get; set; } = "seminar@fti.edu";
            public string ToEmail { get; set; } = string.Empty;
            public string Subject { get; set; } = string.Empty;
            public string HtmlBody { get; set; } = string.Empty;
        }

        public EmailEditorViewModel()
        {
            _workspaceService = new WorkspaceService(new StorageService());
            _excelImporter = new ExcelImporter();
            _storageService = new StorageService();

            // Load demo data if available
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Loading workspace...";

                // Try to load most recent workspace (demo)
                // In real app, this would come from user selection
                var workspaceDir = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "mbw_demo_workspace"
                );

                if (System.IO.Directory.Exists(workspaceDir))
                {
                    await LoadWorkspaceAsync(workspaceDir);
                }
                else
                {
                    StatusMessage = "No workspace loaded. Create or open a workspace first.";
                }
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

        /// <summary>
        /// Load workspace from disk and populate variables from Excel
        /// </summary>
        public async Task LoadWorkspaceAsync(string workspacePath)
        {
            try
            {
                IsLoading = true;
                _workspacePath = workspacePath;
                StatusMessage = "Loading workspace...";

                // Load workspace
                _currentWorkspace = await _workspaceService.OpenAsync(workspacePath);
                Subject = _currentWorkspace.Template?.Subject ?? string.Empty;
                HtmlBody = _currentWorkspace.Template?.HtmlBody ?? string.Empty;

                // Load Excel headers
                if (!string.IsNullOrEmpty(_currentWorkspace.DataFilePath) 
                    && System.IO.File.Exists(_currentWorkspace.DataFilePath))
                {
                    await LoadExcelHeadersAsync(_currentWorkspace.DataFilePath);
                }
                else
                {
                    AvailableVariables.Clear();
                    NoVariablesVisibility = Microsoft.UI.Xaml.Visibility.Visible;
                }

                StatusMessage = "Workspace loaded successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading workspace: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Load Excel headers and populate available variables
        /// </summary>
        private async Task LoadExcelHeadersAsync(string filePath)
        {
            try
            {
                var headers = await _excelImporter.GetHeadersAsync(filePath);
                AvailableVariables.Clear();

                foreach (var header in headers)
                {
                    AvailableVariables.Add(header);
                }

                NoVariablesVisibility = AvailableVariables.Count == 0 
                    ? Microsoft.UI.Xaml.Visibility.Visible 
                    : Microsoft.UI.Xaml.Visibility.Collapsed;
                StatusMessage = $"Loaded {headers.Count} variables from Excel";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading Excel: {ex.Message}";
            }
        }

        /// <summary>
        /// Save current template to workspace
        /// </summary>
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

        /// <summary>
        /// Preview rendered email for first recipient
        /// </summary>
        [RelayCommand]
        public async Task PreviewAsync()
        {
            try
            {
                if (_currentWorkspace?.Template == null || string.IsNullOrEmpty(_currentWorkspace.DataFilePath))
                {
                    StatusMessage = "Template or data file not found";
                    return;
                }

                IsLoading = true;
                StatusMessage = "Loading preview...";

                // Load first recipient
                var preview = await _excelImporter.PreviewAsync(_currentWorkspace.DataFilePath, 1);

                if (preview.Count == 0)
                {
                    StatusMessage = "No recipients found in Excel file";
                    return;
                }

                var firstRecipient = preview[0];

                // Render template
                var template = _currentWorkspace.Template;
                var rendered = template.RenderForRecipient(firstRecipient);

                // Store for UI
                _lastPreviewRecipient = firstRecipient;
                _lastRenderedTemplate = rendered;

                // Raise event for code-behind to show dialog
                var args = new PreviewEventArgs
                {
                    FromEmail = "seminar@fti.edu",
                    ToEmail = firstRecipient.Get("Email") ?? string.Empty,
                    Subject = rendered.Subject,
                    HtmlBody = rendered.HtmlBody
                };
                PreviewRequested?.Invoke(this, args);

                StatusMessage = "Preview displayed";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error generating preview: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Continue to sending (placeholder for STEP 8)
        /// </summary>
        [RelayCommand]
        public void Continue()
        {
            StatusMessage = "Continue to Sending - Not yet implemented (STEP 8)";
        }

        /// <summary>
        /// Insert variable at cursor position in editor
        /// Called from code-behind when variable button clicked
        /// </summary>
        public void InsertVariable(string variableName)
        {
            StatusMessage = $"Variable {{{variableName}}} ready to insert";
        }
    }
}

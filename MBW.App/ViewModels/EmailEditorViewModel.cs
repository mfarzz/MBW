using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MBW.Core.Interfaces;
using MBW.Core.Models;
using MBW.Core.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace MBW.App.ViewModels
{
    public partial class EmailEditorViewModel : ObservableObject
    {
        private readonly IWorkspaceService _workspaceService;
        private readonly IExcelImporter _excelImporter;
        private readonly WorkspaceCoordinator _workspaceCoordinator;

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

        public event EventHandler<PreviewEventArgs>? PreviewRequested;

        public class PreviewEventArgs : EventArgs
        {
            public string FromEmail { get; set; } = "seminar@fti.edu";
            public string ToEmail { get; set; } = string.Empty;
            public string Subject { get; set; } = string.Empty;
            public string HtmlBody { get; set; } = string.Empty;
        }

        public EmailEditorViewModel(
            IWorkspaceService workspaceService,
            IExcelImporter excelImporter,
            WorkspaceCoordinator workspaceCoordinator)
        {
            _workspaceService = workspaceService;
            _excelImporter = excelImporter;
            _workspaceCoordinator = workspaceCoordinator;
            _workspaceCoordinator.Changed += (_, _) => _ = InitializeAsync();

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

                var preview = await _excelImporter.PreviewAsync(_currentWorkspace.DataFilePath, 1);

                if (preview.Count == 0)
                {
                    StatusMessage = "No recipients found in Excel file";
                    return;
                }

                var firstRecipient = preview[0];
                var template = _currentWorkspace.Template;
                var rendered = template.RenderForRecipient(firstRecipient);

                _lastPreviewRecipient = firstRecipient;
                _lastRenderedTemplate = rendered;

                PreviewRequested?.Invoke(this, new PreviewEventArgs
                {
                    FromEmail = "seminar@fti.edu",
                    ToEmail = firstRecipient.Get("Email") ?? string.Empty,
                    Subject = rendered.Subject,
                    HtmlBody = rendered.HtmlBody
                });

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

        [RelayCommand]
        public void Continue()
        {
            StatusMessage = "Continue to Sending - Not yet implemented (STEP 8)";
        }

        public void InsertVariable(string variableName)
        {
            StatusMessage = $"Variable {{{variableName}}} ready to insert";
        }

        public EmailTemplate GetCurrentTemplate() => new(Subject, HtmlBody);
    }
}

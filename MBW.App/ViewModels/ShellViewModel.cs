using CommunityToolkit.Mvvm.ComponentModel;
using MBW.Core.Interfaces;
using MBW.Core.Models;
using MBW.Core.Services;
using System;
using System.Threading.Tasks;

namespace MBW.App.ViewModels
{
    public partial class ShellViewModel : ObservableObject
    {
        private readonly WorkspaceCoordinator _workspaceCoordinator;
        private readonly SmtpSettingsCoordinator _smtpCoordinator;
        private readonly IRecentProjectsService _recentProjectsService;

        public ShellViewModel(
            WorkspaceCoordinator workspaceCoordinator,
            SmtpSettingsCoordinator smtpCoordinator,
            IRecentProjectsService recentProjectsService)
        {
            _workspaceCoordinator = workspaceCoordinator;
            _smtpCoordinator = smtpCoordinator;
            _recentProjectsService = recentProjectsService;
            _workspaceCoordinator.Changed += (_, _) => SyncFromCoordinator(saved: true);
            _smtpCoordinator.Changed += (_, _) => SyncSmtpFromCoordinator();
            SyncFromCoordinator(saved: false);
            SyncSmtpFromCoordinator();
        }

        [ObservableProperty]
        public partial string WorkspaceName { get; set; } = "Workspace";

        [ObservableProperty]
        public partial string StatusWorkspaceName { get; set; } = "Workspace";

        [ObservableProperty]
        public partial string WorkspaceSavedText { get; set; } = "No workspace loaded";

        [ObservableProperty]
        public partial string SmtpStatusText { get; set; } = "SMTP: Not connected";

        [ObservableProperty]
        public partial bool SmtpIsConnected { get; set; }

        public event EventHandler? WorkspaceChanged;

        public event EventHandler<string>? NavigationRequested;

        public async Task InitializeAsync()
        {
            await _smtpCoordinator.LoadAsync();
        }

        public async Task NewWorkspaceAsync()
        {
            if (await _workspaceCoordinator.CreateNewAsync())
            {
                await TrackRecentAsync();
                NotifyWorkspaceChanged();
            }
        }

        public async Task OpenWorkspaceAsync()
        {
            if (await _workspaceCoordinator.OpenExistingAsync())
            {
                await TrackRecentAsync();
                NotifyWorkspaceChanged();
            }
        }

        public async Task<bool> SaveWorkspaceAsync()
        {
            if (await _workspaceCoordinator.SaveCurrentAsync())
            {
                SyncFromCoordinator(saved: true);
                return true;
            }

            return false;
        }

        public async Task ShowSmtpSettingsAsync()
        {
            await _smtpCoordinator.ShowEditorAsync();
        }

        public void RequestNavigation(string tag)
        {
            NavigationRequested?.Invoke(this, tag);
        }

        public void ApplyEmailTemplate(EmailTemplate template)
        {
            _workspaceCoordinator.UpdateCurrentTemplate(template);
        }

        public string? CurrentWorkspacePath => _workspaceCoordinator.WorkspacePath;

        public bool HasWorkspace => _workspaceCoordinator.HasWorkspace;

        private void NotifyWorkspaceChanged()
        {
            SyncFromCoordinator(saved: true);
            WorkspaceChanged?.Invoke(this, EventArgs.Empty);
        }

        private async Task TrackRecentAsync()
        {
            if (!_workspaceCoordinator.HasWorkspace)
            {
                return;
            }

            await _recentProjectsService.AddOrUpdateAsync(
                _workspaceCoordinator.Current!.Name,
                _workspaceCoordinator.WorkspacePath!);
        }

        private void SyncFromCoordinator(bool saved)
        {
            if (_workspaceCoordinator.HasWorkspace)
            {
                var name = _workspaceCoordinator.Current!.Name;
                WorkspaceName = name;
                StatusWorkspaceName = name;
                WorkspaceSavedText = saved
                    ? $"Workspace saved · {DateTime.Now:HH:mm}"
                    : WorkspaceSavedText;
            }
            else
            {
                WorkspaceSavedText = "No workspace loaded";
            }
        }

        private void SyncSmtpFromCoordinator()
        {
            SmtpStatusText = _smtpCoordinator.StatusLabel;
            SmtpIsConnected = _smtpCoordinator.IsConnected;
        }
    }
}

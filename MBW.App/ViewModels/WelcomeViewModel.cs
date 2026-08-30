using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MBW.Core.Interfaces;
using MBW.Core.Models;
using MBW.Core.Services;
using Microsoft.UI.Xaml;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
namespace MBW.App.ViewModels
{
    public partial class WelcomeViewModel : ObservableObject
    {
        private readonly WorkspaceCoordinator _workspaceCoordinator;
        private readonly IRecentProjectsService _recentProjectsService;

        public WelcomeViewModel(WorkspaceCoordinator workspaceCoordinator, IRecentProjectsService recentProjectsService)
        {
            _workspaceCoordinator = workspaceCoordinator;
            _recentProjectsService = recentProjectsService;
        }

        public ObservableCollection<RecentProjectItemViewModel> RecentProjects { get; } = [];

        [ObservableProperty]
        public partial bool HasRecentProjects { get; set; }

        [ObservableProperty]
        public partial bool HasNoRecentProjects { get; set; } = true;

        [ObservableProperty]
        public partial Visibility RecentListVisibility { get; set; } = Visibility.Collapsed;

        [ObservableProperty]
        public partial Visibility EmptyRecentVisibility { get; set; } = Visibility.Visible;

        public event EventHandler? ProjectOpened;

        public async Task LoadAsync()
        {
            RecentProjects.Clear();
            var entries = await _recentProjectsService.LoadAsync();
            foreach (var entry in entries)
            {
                RecentProjects.Add(ToItem(entry));
            }

            UpdateRecentVisibility();
        }

        private void UpdateRecentVisibility()
        {
            HasRecentProjects = RecentProjects.Count > 0;
            HasNoRecentProjects = !HasRecentProjects;
            RecentListVisibility = HasRecentProjects ? Visibility.Visible : Visibility.Collapsed;
            EmptyRecentVisibility = HasNoRecentProjects ? Visibility.Visible : Visibility.Collapsed;
        }

        [RelayCommand]
        public async Task NewProjectAsync()
        {
            if (await _workspaceCoordinator.CreateNewAsync())
            {
                await TrackRecentAsync();
                ProjectOpened?.Invoke(this, EventArgs.Empty);
            }
        }

        [RelayCommand]
        public async Task OpenProjectAsync()
        {
            if (await _workspaceCoordinator.OpenExistingAsync())
            {
                await TrackRecentAsync();
                ProjectOpened?.Invoke(this, EventArgs.Empty);
            }
        }

        [RelayCommand]
        public async Task OpenRecentAsync(RecentProjectItemViewModel? item)
        {
            if (item is null)
            {
                return;
            }

            if (await _workspaceCoordinator.OpenFromPathAsync(item.Path))
            {
                await TrackRecentAsync();
                ProjectOpened?.Invoke(this, EventArgs.Empty);
                return;
            }

            await _recentProjectsService.RemoveAsync(item.Path);
            await LoadAsync();
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

        private static RecentProjectItemViewModel ToItem(RecentProjectEntry entry)
        {
            return new RecentProjectItemViewModel
            {
                Name = entry.Name,
                Path = entry.Path,
                LocationDisplay = entry.Path,
                OpenedDisplay = entry.LastOpenedAt.ToString("dd MMM yyyy · HH:mm")
            };
        }
    }

    public sealed class RecentProjectItemViewModel
    {
        public string Name { get; init; } = string.Empty;

        public string Path { get; init; } = string.Empty;

        public string LocationDisplay { get; init; } = string.Empty;

        public string OpenedDisplay { get; init; } = string.Empty;
    }
}

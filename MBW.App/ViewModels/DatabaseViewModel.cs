using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MBW.Core.Interfaces;
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
    public partial class DatabaseViewModel : ObservableObject
    {
        private const int PreviewLimit = 50;

        private readonly IExcelImporter _excelImporter;
        private readonly WorkspaceCoordinator _workspaceCoordinator;

        public DatabaseViewModel(IExcelImporter excelImporter, WorkspaceCoordinator workspaceCoordinator)
        {
            _excelImporter = excelImporter;
            _workspaceCoordinator = workspaceCoordinator;
            _workspaceCoordinator.Changed += (_, _) => _ = LoadAsync();
            _ = LoadAsync();
        }

        public Func<Task<string?>>? PickExcelFileAsync { get; set; }

        public ObservableCollection<string> ColumnHeaders { get; } = new();

        public ObservableCollection<DatabasePreviewRow> PreviewRows { get; } = new();

        [ObservableProperty]
        public partial string StatusMessage { get; set; } = "No database loaded yet.";

        [ObservableProperty]
        public partial string FileName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial long TotalRows { get; set; }

        [ObservableProperty]
        public partial bool HasData { get; set; }

        [ObservableProperty]
        public partial bool IsBusy { get; set; }

        public bool CanImport => !IsBusy;

        public Visibility BusyVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;

        [ObservableProperty]
        public partial Visibility EmptyVisibility { get; set; } = Visibility.Visible;

        [ObservableProperty]
        public partial Visibility PreviewVisibility { get; set; } = Visibility.Collapsed;

        public string PreviewCaption =>
            TotalRows > PreviewRows.Count
                ? $"Showing first {PreviewRows.Count} of {TotalRows:N0} rows"
                : $"{TotalRows:N0} rows";

        public string FileSummary =>
            string.IsNullOrEmpty(FileName) ? string.Empty : $"File: {FileName}";

        partial void OnFileNameChanged(string value)
        {
            OnPropertyChanged(nameof(FileSummary));
        }

        partial void OnIsBusyChanged(bool value)
        {
            OnPropertyChanged(nameof(CanImport));
            OnPropertyChanged(nameof(BusyVisibility));
            ImportExcelCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanImport))]
        private async Task ImportExcelAsync()
        {
            if (!_workspaceCoordinator.HasWorkspace)
            {
                StatusMessage = "Create or open a workspace before importing Excel.";
                return;
            }

            if (PickExcelFileAsync is null)
            {
                StatusMessage = "File picker is not available.";
                return;
            }

            var pickedPath = await PickExcelFileAsync();
            if (string.IsNullOrWhiteSpace(pickedPath))
            {
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = "Importing Excel file...";

                var workspacePath = _workspaceCoordinator.WorkspacePath!;
                var dataDir = Path.Combine(workspacePath, "data");
                Directory.CreateDirectory(dataDir);

                var fileName = Path.GetFileName(pickedPath);
                var destinationPath = Path.Combine(dataDir, fileName);
                File.Copy(pickedPath, destinationPath, overwrite: true);

                var relativePath = Path.Combine("data", fileName).Replace('\\', '/');
                _workspaceCoordinator.UpdateDataFilePath(relativePath);
                await _workspaceCoordinator.SaveCurrentAsync();

                await LoadAsync();
                StatusMessage = $"Imported {fileName} successfully.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Import failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadAsync()
        {
            ColumnHeaders.Clear();
            PreviewRows.Clear();
            FileName = string.Empty;
            TotalRows = 0;
            HasData = false;
            EmptyVisibility = Visibility.Visible;
            PreviewVisibility = Visibility.Collapsed;

            if (!_workspaceCoordinator.HasWorkspace)
            {
                StatusMessage = "No workspace loaded.";
                return;
            }

            var dataPath = _workspaceCoordinator.GetResolvedDataFilePath();
            if (string.IsNullOrWhiteSpace(dataPath))
            {
                StatusMessage = "No database loaded yet. Import an Excel file to get started.";
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = "Loading database...";

                FileName = Path.GetFileName(dataPath);
                var headers = await _excelImporter.GetHeadersAsync(dataPath);
                foreach (var header in headers)
                {
                    ColumnHeaders.Add(header);
                }

                TotalRows = await _excelImporter.GetRowCountAsync(dataPath);
                var preview = await _excelImporter.PreviewAsync(dataPath, PreviewLimit);
                foreach (var row in preview)
                {
                    var cells = headers
                        .Select(header => row.Get(header) ?? string.Empty)
                        .ToList();
                    PreviewRows.Add(new DatabasePreviewRow(cells));
                }

                HasData = ColumnHeaders.Count > 0;
                EmptyVisibility = HasData ? Visibility.Collapsed : Visibility.Visible;
                PreviewVisibility = HasData ? Visibility.Visible : Visibility.Collapsed;
                StatusMessage = HasData
                    ? $"Loaded {TotalRows:N0} recipients from {FileName}."
                    : "The Excel file has no columns.";
                OnPropertyChanged(nameof(PreviewCaption));
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading database: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}

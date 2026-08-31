using MBW.App.Composition;
using MBW.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace MBW.App.Views
{
    public sealed partial class AttachmentsPage : Page
    {
        private readonly AttachmentsViewModel _viewModel;
        private bool _syncingScroll;

        public AttachmentsPage()
        {
            InitializeComponent();

            _viewModel = AppServices.GetAttachmentsViewModel();
            _viewModel.PickFolderAsync = PickFolderAsync;
            _viewModel.PickFilesAsync = PickFilesAsync;
            _viewModel.PromptFolderNameAsync = PromptFolderNameAsync;
            _viewModel.ConfirmImportFolderAsync = ConfirmImportFolderAsync;
            _viewModel.ConfirmDeleteAsync = ConfirmDeleteAsync;
            DataContext = _viewModel;
            NavigationCacheMode = NavigationCacheMode.Enabled;
            Loaded += AttachmentsPage_Loaded;
        }

        private async void AttachmentsPage_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.EnsureLoadedAsync(force: false);
        }

        private async void ExplorerList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (_viewModel.SelectedItem is { IsFolder: true } item)
            {
                await _viewModel.OpenItemCommand.ExecuteAsync(item);
            }
        }

        private void HeaderScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (_syncingScroll)
            {
                return;
            }

            _syncingScroll = true;
            BodyScrollViewer.ChangeView(HeaderScrollViewer.HorizontalOffset, null, null, disableAnimation: true);
            _syncingScroll = false;
        }

        private void BodyScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (_syncingScroll)
            {
                return;
            }

            _syncingScroll = true;
            HeaderScrollViewer.ChangeView(BodyScrollViewer.HorizontalOffset, null, null, disableAnimation: true);
            _syncingScroll = false;
        }

        private async Task<string?> PickFolderAsync()
        {
            var picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                ViewMode = PickerViewMode.List
            };
            picker.FileTypeFilter.Add("*");

            var hwnd = WindowNative.GetWindowHandle(AppServices.GetMainWindow());
            InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();
            return folder?.Path;
        }

        private async Task<IReadOnlyList<string>> PickFilesAsync()
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                ViewMode = PickerViewMode.List
            };
            picker.FileTypeFilter.Add(".pdf");
            picker.FileTypeFilter.Add(".doc");
            picker.FileTypeFilter.Add(".docx");
            picker.FileTypeFilter.Add(".xls");
            picker.FileTypeFilter.Add(".xlsx");
            picker.FileTypeFilter.Add(".xlsm");
            picker.FileTypeFilter.Add(".ppt");
            picker.FileTypeFilter.Add(".pptx");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".zip");

            var hwnd = WindowNative.GetWindowHandle(AppServices.GetMainWindow());
            InitializeWithWindow.Initialize(picker, hwnd);

            var files = await picker.PickMultipleFilesAsync();
            return files.Select(file => file.Path).ToList();
        }

        private async Task<string?> PromptFolderNameAsync()
        {
            var textBox = new TextBox
            {
                PlaceholderText = "Nama folder individual",
                SelectionStart = 0
            };

            var dialog = new ContentDialog
            {
                Title = "Folder baru",
                Content = textBox,
                PrimaryButtonText = "Buat",
                CloseButtonText = "Batal",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary ? textBox.Text : null;
        }

        private async Task<bool> ConfirmImportFolderAsync(string folderName)
        {
            var dialog = new ContentDialog
            {
                Title = "Import folder?",
                Content = $"Salin semua file dari \"{folderName}\" ke workspace? File dengan nama sama akan ditimpa.",
                PrimaryButtonText = "Import",
                CloseButtonText = "Batal",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }

        private async Task<bool> ConfirmDeleteAsync(string itemName)
        {
            var dialog = new ContentDialog
            {
                Title = "Hapus item?",
                Content = $"Hapus \"{itemName}\" dari workspace? Tindakan ini tidak dapat dibatalkan.",
                PrimaryButtonText = "Hapus",
                CloseButtonText = "Batal",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }
    }
}

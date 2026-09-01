using MBW.App.Composition;
using MBW.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
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
            _viewModel.PromptRenameAsync = PromptRenameAsync;
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

        private void ExplorerList_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            SelectItemUnderPointer((ListView)sender, e.OriginalSource as DependencyObject);
        }

        private void ExplorerContextFlyout_Opening(object sender, object e)
        {
            if (sender is not MenuFlyout flyout)
            {
                return;
            }

            if (flyout.Target is ListViewItem { Content: AttachmentItemViewModel item })
            {
                ExplorerList.SelectedItem = item;
                return;
            }

            if (flyout.Target is FrameworkElement { DataContext: AttachmentItemViewModel contextItem })
            {
                ExplorerList.SelectedItem = contextItem;
            }
        }

        private void ExplorerContextCut_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.CutItemCommand.CanExecute(null))
            {
                _viewModel.CutItemCommand.Execute(null);
            }
        }

        private void ExplorerContextCopy_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.CopyItemCommand.CanExecute(null))
            {
                _viewModel.CopyItemCommand.Execute(null);
            }
        }

        private async void ExplorerContextPaste_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.PasteItemCommand.CanExecute(null))
            {
                await _viewModel.PasteItemCommand.ExecuteAsync(null);
            }
        }

        private async void ExplorerContextRename_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.RenameItemCommand.CanExecute(null))
            {
                await _viewModel.RenameItemCommand.ExecuteAsync(null);
            }
        }

        private async void ExplorerContextDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.DeleteItemCommand.CanExecute(null))
            {
                await _viewModel.DeleteItemCommand.ExecuteAsync(null);
            }
        }

        private static void SelectItemUnderPointer(ListView listView, DependencyObject? source)
        {
            var element = source;

            while (element is not null && element != listView)
            {
                if (element is ListViewItem listItem && listItem.Content is AttachmentItemViewModel item)
                {
                    listView.SelectedItem = item;
                    break;
                }

                element = VisualTreeHelper.GetParent(element);
            }
        }

        private void ExplorerCut_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!_viewModel.CutItemCommand.CanExecute(null))
            {
                return;
            }

            args.Handled = true;
            _viewModel.CutItemCommand.Execute(null);
        }

        private void ExplorerCopy_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!_viewModel.CopyItemCommand.CanExecute(null))
            {
                return;
            }

            args.Handled = true;
            _viewModel.CopyItemCommand.Execute(null);
        }

        private async void ExplorerPaste_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!_viewModel.PasteItemCommand.CanExecute(null))
            {
                return;
            }

            args.Handled = true;
            await _viewModel.PasteItemCommand.ExecuteAsync(null);
        }

        private async void ExplorerRename_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!_viewModel.RenameItemCommand.CanExecute(null))
            {
                return;
            }

            args.Handled = true;
            await _viewModel.RenameItemCommand.ExecuteAsync(null);
        }

        private async void ExplorerDelete_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!_viewModel.DeleteItemCommand.CanExecute(null))
            {
                return;
            }

            args.Handled = true;
            await _viewModel.DeleteItemCommand.ExecuteAsync(null);
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
                PlaceholderText = "Individual folder name",
                SelectionStart = 0
            };

            var dialog = new ContentDialog
            {
                Title = "New folder",
                Content = textBox,
                PrimaryButtonText = "Create",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary ? textBox.Text : null;
        }

        private async Task<string?> PromptRenameAsync(string currentName, string itemKind)
        {
            var isFile = string.Equals(itemKind, "file", StringComparison.OrdinalIgnoreCase);
            var extension = isFile ? Path.GetExtension(currentName) : string.Empty;
            var editableName = isFile ? Path.GetFileNameWithoutExtension(currentName) : currentName;

            var textBox = new TextBox
            {
                Text = editableName,
                PlaceholderText = $"{itemKind} name",
                SelectionStart = 0,
                SelectionLength = editableName.Length
            };

            var dialog = new ContentDialog
            {
                Title = "Rename",
                Content = textBox,
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return null;
            }

            var entered = textBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(entered))
            {
                return null;
            }

            if (isFile && !string.IsNullOrEmpty(extension) && string.IsNullOrEmpty(Path.GetExtension(entered)))
            {
                return entered + extension;
            }

            return entered;
        }

        private async Task<bool> ConfirmImportFolderAsync(string folderName)
        {
            var dialog = new ContentDialog
            {
                Title = "Import folder?",
                Content = $"Copy all files from \"{folderName}\" into the workspace? Files with the same name will be overwritten.",
                PrimaryButtonText = "Import",
                CloseButtonText = "Cancel",
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
                Title = "Delete item?",
                Content = $"Delete \"{itemName}\" from the workspace? This action cannot be undone.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }
    }
}

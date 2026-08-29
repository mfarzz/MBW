using MBW.Core.Interfaces;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace MBW.App.Platform
{
    /// <summary>
    /// WinUI implementation of workspace UI prompts (dialogs, folder picker).
    /// </summary>
    public sealed class WinUiWorkspaceUiGateway : IWorkspaceUiGateway
    {
        private readonly Window _window;

        public WinUiWorkspaceUiGateway(Window window)
        {
            _window = window;
        }

        public Task<string?> PromptWorkspaceNameAsync(string title, string defaultName, CancellationToken cancellationToken = default)
        {
            return RunOnUiThreadAsync(async () =>
            {
                var input = new TextBox
                {
                    Text = defaultName,
                    PlaceholderText = "Workspace name",
                    SelectionStart = 0,
                    SelectionLength = defaultName.Length
                };

                var dialog = new ContentDialog
                {
                    Title = title,
                    Content = input,
                    PrimaryButtonText = "Create",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = _window.Content.XamlRoot
                };

                var result = await dialog.ShowAsync();
                return result == ContentDialogResult.Primary ? input.Text : null;
            });
        }

        public Task ShowMessageAsync(string title, string message, CancellationToken cancellationToken = default)
        {
            return RunOnUiThreadAsync(async () =>
            {
                var dialog = new ContentDialog
                {
                    Title = title,
                    Content = new TextBlock { Text = message, TextWrapping = TextWrapping.WrapWholeWords },
                    CloseButtonText = "OK",
                    XamlRoot = _window.Content.XamlRoot
                };

                await dialog.ShowAsync();
            });
        }

        public Task<string?> PickFolderPathAsync(string title, CancellationToken cancellationToken = default)
        {
            return RunOnUiThreadAsync(async () =>
            {
                var picker = new FolderPicker
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    ViewMode = PickerViewMode.List
                };
                picker.FileTypeFilter.Add("*");

                var hwnd = WindowNative.GetWindowHandle(_window);
                InitializeWithWindow.Initialize(picker, hwnd);
                picker.SettingsIdentifier = title;

                var folder = await picker.PickSingleFolderAsync();
                return folder?.Path;
            });
        }

        private Task<T> RunOnUiThreadAsync<T>(Func<Task<T>> action)
        {
            var queue = _window.DispatcherQueue;
            if (queue.HasThreadAccess)
            {
                return action();
            }

            var tcs = new TaskCompletionSource<T>();
            queue.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
            {
                try
                {
                    tcs.SetResult(await action());
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
        }

        private Task RunOnUiThreadAsync(Func<Task> action)
        {
            return RunOnUiThreadAsync(async () =>
            {
                await action();
                return true;
            });
        }
    }
}

using MBW.App.Controls;
using MBW.App.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MBW.App.Platform
{
    public sealed class WinUiSendGateway
    {
        private readonly Window _window;

        public WinUiSendGateway(Window window)
        {
            _window = window;
        }

        public Task<bool> ConfirmSendAsync(int recipientCount, int rangeFrom, int rangeTo, int delaySeconds)
        {
            return RunOnUiThreadAsync(async () =>
            {
                var delayLabel = delaySeconds == 1 ? "second" : "seconds";
                var message =
                    $"Send {recipientCount:N0} email(s) for rows {rangeFrom:N0}–{rangeTo:N0} with a {delaySeconds:N0} {delayLabel} delay between each message?";

                var dialog = new ContentDialog
                {
                    Title = "Confirm send",
                    Content = message,
                    PrimaryButtonText = "Send",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = _window.Content.XamlRoot
                };

                var result = await dialog.ShowAsync();
                return result == ContentDialogResult.Primary;
            });
        }

        public Task RunProgressAsync(Func<SendProgressViewModel, CancellationToken, Task> work)
        {
            return RunOnUiThreadAsync(async () =>
            {
                var progressViewModel = new SendProgressViewModel(_window.DispatcherQueue);
                var form = new SendProgressForm { DataContext = progressViewModel };
                var contentHost = new Grid
                {
                    MinWidth = 440,
                    MaxWidth = 520,
                    MinHeight = 280
                };
                contentHost.Children.Add(form);

                using var cts = new CancellationTokenSource();
                var allowClose = false;
                var dialog = new ContentDialog
                {
                    Title = "Sending emails",
                    Content = contentHost,
                    PrimaryButtonText = "Cancel",
                    CloseButtonText = string.Empty,
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = _window.Content.XamlRoot
                };

                dialog.Closing += (_, e) =>
                {
                    if (!allowClose)
                    {
                        e.Cancel = true;
                    }
                };

                dialog.PrimaryButtonClick += (_, args) =>
                {
                    var deferral = args.GetDeferral();
                    cts.Cancel();
                    deferral.Complete();
                };

                dialog.Opened += async (_, _) =>
                {
                    var completedSuccessfully = false;
                    try
                    {
                        await work(progressViewModel, cts.Token);
                        await progressViewModel.MarkCompleteAsync("Send complete.");
                        completedSuccessfully = true;
                    }
                    catch (OperationCanceledException)
                    {
                        await progressViewModel.MarkIncompleteAsCancelledAsync();
                        await progressViewModel.MarkCompleteAsync("Send cancelled.");
                    }
                    catch (Exception ex)
                    {
                        await progressViewModel.MarkCompleteAsync($"Send failed: {ex.Message}");
                    }
                    finally
                    {
                        dialog.Title = completedSuccessfully
                            ? "Send complete"
                            : cts.IsCancellationRequested
                                ? "Send cancelled"
                                : "Send failed";
                        dialog.PrimaryButtonText = string.Empty;
                        dialog.IsPrimaryButtonEnabled = false;
                        dialog.CloseButtonText = "Close";
                        allowClose = true;
                    }
                };

                await dialog.ShowAsync();
            });
        }

        private Task RunOnUiThreadAsync(Func<Task> action)
        {
            var queue = _window.DispatcherQueue;
            if (queue.HasThreadAccess)
            {
                return action();
            }

            var tcs = new TaskCompletionSource();
            queue.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
            {
                try
                {
                    await action();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
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
    }
}

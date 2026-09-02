using MBW.App.Controls;
using MBW.App.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

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

        public Task<string?> RunProgressAsync(Func<SendProgressViewModel, CancellationToken, IReadOnlyList<int>?, Task> work)
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

                var allowClose = false;
                var isSendingPhase = true;
                CancellationTokenSource? activeCts = null;
                TaskCompletionSource<PostSendAction>? postActionTcs = null;
                IReadOnlyList<int>? rowFilter = null;
                string? finalSummary = null;
                var wasCancelled = false;

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
                    if (isSendingPhase)
                    {
                        activeCts?.Cancel();
                    }
                    else
                    {
                        postActionTcs?.TrySetResult(PostSendAction.RetryFailed);
                    }

                    deferral.Complete();
                };

                dialog.SecondaryButtonClick += (_, args) =>
                {
                    var deferral = args.GetDeferral();
                    if (!isSendingPhase)
                    {
                        postActionTcs?.TrySetResult(PostSendAction.ExportLog);
                    }

                    deferral.Complete();
                };

                dialog.CloseButtonClick += (_, args) =>
                {
                    var deferral = args.GetDeferral();
                    if (!isSendingPhase)
                    {
                        postActionTcs?.TrySetResult(PostSendAction.Close);
                    }

                    deferral.Complete();
                };

                dialog.Opened += async (_, _) =>
                {
                    while (true)
                    {
                        isSendingPhase = true;
                        wasCancelled = false;
                        ConfigureSendingButtons(dialog);

                        activeCts?.Dispose();
                        activeCts = new CancellationTokenSource();

                        try
                        {
                            await work(progressViewModel, activeCts.Token, rowFilter);
                            finalSummary = progressViewModel.BuildSummaryCaption();
                            await progressViewModel.MarkCompleteAsync(finalSummary);
                        }
                        catch (OperationCanceledException)
                        {
                            wasCancelled = true;
                            await progressViewModel.MarkIncompleteAsCancelledAsync();
                            finalSummary = progressViewModel.BuildSummaryCaption();
                            await progressViewModel.MarkCompleteAsync(finalSummary);
                        }
                        catch (Exception ex)
                        {
                            finalSummary = $"{progressViewModel.BuildSummaryCaption()} · Error: {ex.Message}";
                            await progressViewModel.MarkCompleteAsync(finalSummary);
                        }

                        isSendingPhase = false;
                        dialog.Title = GetDialogTitle(progressViewModel, wasCancelled);
                        ConfigureCompleteButtons(dialog, progressViewModel.HasRetryableFailures);

                        postActionTcs = new TaskCompletionSource<PostSendAction>(TaskCreationOptions.RunContinuationsAsynchronously);
                        var action = await postActionTcs.Task;

                        if (action == PostSendAction.Close)
                        {
                            break;
                        }

                        if (action == PostSendAction.ExportLog)
                        {
                            if (await TryExportSendLogAsync(progressViewModel))
                            {
                                finalSummary = $"{progressViewModel.BuildSummaryCaption()} · Log exported";
                                await progressViewModel.MarkCompleteAsync(finalSummary);
                            }

                            continue;
                        }

                        rowFilter = progressViewModel.GetFailedRowNumbers();
                        if (rowFilter.Count == 0)
                        {
                            continue;
                        }

                        progressViewModel.PrepareRetryFailed();
                        dialog.Title = "Sending emails";
                    }

                    allowClose = true;
                    dialog.Hide();
                };

                await dialog.ShowAsync();
                activeCts?.Dispose();
                return finalSummary;
            });
        }

        private static void ConfigureSendingButtons(ContentDialog dialog)
        {
            dialog.Title = "Sending emails";
            dialog.PrimaryButtonText = "Cancel";
            dialog.IsPrimaryButtonEnabled = true;
            dialog.SecondaryButtonText = string.Empty;
            dialog.IsSecondaryButtonEnabled = false;
            dialog.CloseButtonText = string.Empty;
        }

        private static void ConfigureCompleteButtons(ContentDialog dialog, bool hasFailures)
        {
            dialog.PrimaryButtonText = hasFailures ? "Retry failed" : string.Empty;
            dialog.IsPrimaryButtonEnabled = hasFailures;
            dialog.SecondaryButtonText = "Export log";
            dialog.IsSecondaryButtonEnabled = true;
            dialog.CloseButtonText = "Close";
        }

        private static string GetDialogTitle(SendProgressViewModel progress, bool wasCancelled)
        {
            if (wasCancelled)
            {
                return "Send cancelled";
            }

            var (_, failed, _, _) = progress.GetCounts();
            return failed > 0 ? "Send finished" : "Send complete";
        }

        private async Task<bool> TryExportSendLogAsync(SendProgressViewModel progress)
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = $"send-log-{DateTime.Now:yyyyMMdd-HHmmss}"
            };
            picker.FileTypeChoices.Add("CSV file", new List<string> { ".csv" });
            picker.FileTypeChoices.Add("Text file", new List<string> { ".txt" });

            var hwnd = WindowNative.GetWindowHandle(_window);
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                return false;
            }

            await FileIO.WriteTextAsync(file, progress.BuildExportContent());
            return true;
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

        private Task RunOnUiThreadAsync(Func<Task> action) =>
            RunOnUiThreadAsync(async () =>
            {
                await action();
                return true;
            });

        private enum PostSendAction
        {
            Close,
            RetryFailed,
            ExportLog
        }
    }
}

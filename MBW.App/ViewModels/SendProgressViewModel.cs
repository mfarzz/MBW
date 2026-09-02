using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace MBW.App.ViewModels
{
    public partial class SendProgressViewModel : ObservableObject
    {
        private readonly DispatcherQueue? _dispatcherQueue;
        private readonly Dictionary<int, SendProgressEntry> _entriesByRow = new();

        public SendProgressViewModel(DispatcherQueue? dispatcherQueue = null)
        {
            _dispatcherQueue = dispatcherQueue;
        }

        public ObservableCollection<SendProgressEntry> Entries { get; } = new();

        public event EventHandler<SendProgressEntry>? EntryUpdated;

        [ObservableProperty]
        public partial double ProgressValue { get; set; }

        [ObservableProperty]
        public partial bool IsIndeterminate { get; set; } = true;

        [ObservableProperty]
        public partial string ProgressCaption { get; set; } = "Preparing...";

        [ObservableProperty]
        public partial bool IsComplete { get; set; }

        public Task ReportAsync(int current, int total) =>
            InvokeOnUiThreadAsync(() => Report(current, total));

        public Task InitializeEntriesAsync(IEnumerable<(int RowNumber, string Email)> rows) =>
            InvokeOnUiThreadAsync(() => InitializeEntries(rows));

        public Task SetStatusAsync(int rowNumber, SendProgressStatus status, string? errorMessage = null) =>
            InvokeOnUiThreadAsync(() => SetStatus(rowNumber, status, errorMessage));

        public Task MarkIncompleteAsCancelledAsync() =>
            InvokeOnUiThreadAsync(MarkIncompleteAsCancelled);

        public Task MarkCompleteAsync(string caption) =>
            InvokeOnUiThreadAsync(() => MarkComplete(caption));

        public Task ResetAsync() =>
            InvokeOnUiThreadAsync(Reset);

        public void Report(int current, int total)
        {
            IsIndeterminate = false;
            ProgressValue = total <= 0 ? 0 : (double)current / total * 100d;
            ProgressCaption = $"{current:N0} / {total:N0} sent";
        }

        public void InitializeEntries(IEnumerable<(int RowNumber, string Email)> rows)
        {
            Entries.Clear();
            _entriesByRow.Clear();

            foreach (var (rowNumber, email) in rows)
            {
                var entry = new SendProgressEntry(rowNumber, email);
                _entriesByRow[rowNumber] = entry;
                Entries.Add(entry);
            }
        }

        public void SetStatus(int rowNumber, SendProgressStatus status, string? errorMessage = null)
        {
            if (!_entriesByRow.TryGetValue(rowNumber, out var entry))
            {
                return;
            }

            entry.Status = status;
            if (errorMessage is not null)
            {
                entry.ErrorMessage = errorMessage;
            }

            EntryUpdated?.Invoke(this, entry);
        }

        public void MarkIncompleteAsCancelled()
        {
            foreach (var entry in Entries)
            {
                if (entry.Status is SendProgressStatus.Pending or SendProgressStatus.Sending)
                {
                    entry.Status = SendProgressStatus.Cancelled;
                    EntryUpdated?.Invoke(this, entry);
                }
            }
        }

        public void MarkComplete(string caption)
        {
            IsComplete = true;
            IsIndeterminate = false;
            ProgressCaption = caption;
        }

        public void Reset()
        {
            Entries.Clear();
            _entriesByRow.Clear();
            ProgressValue = 0;
            IsIndeterminate = true;
            ProgressCaption = "Preparing...";
            IsComplete = false;
        }

        private Task InvokeOnUiThreadAsync(Action action)
        {
            if (_dispatcherQueue is null || _dispatcherQueue.HasThreadAccess)
            {
                action();
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var enqueued = _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
            {
                try
                {
                    action();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            if (!enqueued)
            {
                tcs.SetException(new InvalidOperationException("Failed to enqueue UI update."));
            }

            return tcs.Task;
        }
    }
}

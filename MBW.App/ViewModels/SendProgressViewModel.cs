using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
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

        public (int Succeeded, int Failed, int Skipped, int Cancelled) GetCounts()
        {
            var succeeded = 0;
            var failed = 0;
            var skipped = 0;
            var cancelled = 0;

            foreach (var entry in Entries)
            {
                switch (entry.Status)
                {
                    case SendProgressStatus.Succeeded:
                        succeeded++;
                        break;
                    case SendProgressStatus.Failed:
                        failed++;
                        break;
                    case SendProgressStatus.Skipped:
                        skipped++;
                        break;
                    case SendProgressStatus.Cancelled:
                        cancelled++;
                        break;
                }
            }

            return (succeeded, failed, skipped, cancelled);
        }

        public bool HasRetryableFailures => Entries.Any(entry => entry.Status == SendProgressStatus.Failed);

        public string BuildSummaryCaption()
        {
            var (succeeded, failed, skipped, cancelled) = GetCounts();
            var parts = new List<string>();

            if (succeeded > 0)
            {
                parts.Add($"{succeeded:N0} sent");
            }

            if (failed > 0)
            {
                parts.Add($"{failed:N0} failed");
            }

            if (skipped > 0)
            {
                parts.Add($"{skipped:N0} skipped");
            }

            if (cancelled > 0)
            {
                parts.Add($"{cancelled:N0} cancelled");
            }

            return parts.Count > 0 ? string.Join(" · ", parts) : "No recipients processed";
        }

        public string BuildExportContent()
        {
            var builder = new StringBuilder();
            builder.AppendLine("Row,Email,Status,Error");

            foreach (var entry in Entries.OrderBy(entry => entry.RowNumber))
            {
                var error = entry.ErrorMessage?.Replace("\"", "\"\"") ?? string.Empty;
                builder.AppendLine($"{entry.RowNumber},\"{entry.Email}\",{entry.Status},\"{error}\"");
            }

            return builder.ToString();
        }

        public IReadOnlyList<int> GetFailedRowNumbers() =>
            Entries
                .Where(entry => entry.Status == SendProgressStatus.Failed)
                .Select(entry => entry.RowNumber)
                .ToList();

        public void PrepareRetryFailed()
        {
            foreach (var entry in Entries)
            {
                if (entry.Status != SendProgressStatus.Failed)
                {
                    continue;
                }

                entry.Status = SendProgressStatus.Pending;
                entry.ErrorMessage = null;
                EntryUpdated?.Invoke(this, entry);
            }

            IsComplete = false;
            IsIndeterminate = false;
            ProgressValue = 0;
            ProgressCaption = "Retrying failed...";
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

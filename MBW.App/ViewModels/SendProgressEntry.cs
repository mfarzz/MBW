using CommunityToolkit.Mvvm.ComponentModel;

namespace MBW.App.ViewModels
{
    public partial class SendProgressEntry : ObservableObject
    {
        public SendProgressEntry(int rowNumber, string email)
        {
            RowNumber = rowNumber;
            Email = email;
        }

        public int RowNumber { get; }

        public string Email { get; }

        public string RowLabel => $"Row {RowNumber:N0}";

        [ObservableProperty]
        [NotifyPropertyChangedFor(
            nameof(IsPending),
            nameof(IsSending),
            nameof(IsSucceeded),
            nameof(IsFailed),
            nameof(IsSkipped),
            nameof(IsCancelled))]
        public partial SendProgressStatus Status { get; set; } = SendProgressStatus.Pending;

        [ObservableProperty]
        public partial string? ErrorMessage { get; set; }

        public bool IsPending => Status == SendProgressStatus.Pending;

        public bool IsSending => Status == SendProgressStatus.Sending;

        public bool IsSucceeded => Status == SendProgressStatus.Succeeded;

        public bool IsFailed => Status == SendProgressStatus.Failed;

        public bool IsSkipped => Status == SendProgressStatus.Skipped;

        public bool IsCancelled => Status == SendProgressStatus.Cancelled;
    }
}

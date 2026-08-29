using System;

namespace MBW.Core.Models
{
    public class SendResult
    {
        public long RecipientRowNumber { get; init; }
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
        public string? MessageId { get; init; }

        public SendResult(long recipientRowNumber, bool success, string? errorMessage = null, string? messageId = null)
        {
            RecipientRowNumber = recipientRowNumber;
            Success = success;
            ErrorMessage = errorMessage;
            MessageId = messageId;
        }
    }
}

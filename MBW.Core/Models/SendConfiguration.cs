using System;

namespace MBW.Core.Models
{
    public class SendConfiguration
    {
        public Guid? SmtpAccountId { get; set; }
        public int DelayMilliseconds { get; set; } = 1000;
        public int Concurrency { get; set; } = 1;
        public string? FromName { get; set; }
        public string? FromEmail { get; set; }
        public bool TestMode { get; set; }
        public string EmailColumn { get; set; } = string.Empty;
        public bool IncludeSharedAttachments { get; set; } = true;
        public bool IncludeIndividualAttachments { get; set; } = true;
        public string AttachmentRenamePattern { get; set; } = string.Empty;
        public bool SendAllRecipients { get; set; } = true;
        public int SendRangeFrom { get; set; } = 1;
        public int SendRangeTo { get; set; }
    }
}

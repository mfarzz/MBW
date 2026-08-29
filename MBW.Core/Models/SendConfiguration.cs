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
        public bool TestMode { get; set; } = true;
    }
}

using System;

namespace MBW.Core.Models
{
    public class AttachmentMatch
    {
        public string FileName { get; init; } = string.Empty;
        public string? RecipientKey { get; init; }
        public bool Matched { get; init; }

        public AttachmentMatch(string fileName, bool matched, string? recipientKey = null)
        {
            FileName = fileName ?? string.Empty;
            Matched = matched;
            RecipientKey = recipientKey;
        }
    }
}

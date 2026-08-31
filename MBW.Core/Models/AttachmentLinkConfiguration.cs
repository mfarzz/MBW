using System;

namespace MBW.Core.Models
{
    public sealed class AttachmentLinkConfiguration
    {
        public string IndividualFolderName { get; set; } = string.Empty;

        public string KeyColumn { get; set; } = string.Empty;

        public string FilePattern { get; set; } = string.Empty;

        public int? LastMatchedCount { get; set; }

        public int? LastMissingCount { get; set; }

        public DateTimeOffset? LastValidatedAt { get; set; }

        public static AttachmentLinkConfiguration CreateDefault() => new();

        public AttachmentLinkConfiguration Clone() => new()
        {
            IndividualFolderName = IndividualFolderName,
            KeyColumn = KeyColumn,
            FilePattern = FilePattern,
            LastMatchedCount = LastMatchedCount,
            LastMissingCount = LastMissingCount,
            LastValidatedAt = LastValidatedAt
        };
    }
}

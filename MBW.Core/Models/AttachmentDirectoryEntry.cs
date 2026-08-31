using System;

namespace MBW.Core.Models
{
    public sealed class AttachmentDirectoryEntry
    {
        public string Name { get; init; } = string.Empty;

        public string FullPath { get; init; } = string.Empty;

        public bool IsDirectory { get; init; }

        public long? SizeBytes { get; init; }

        public DateTimeOffset? ModifiedAt { get; init; }
    }
}

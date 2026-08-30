using System;

namespace MBW.Core.Models
{
    public class RecentProjectEntry
    {
        public string Name { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public DateTime LastOpenedAt { get; set; }
    }
}

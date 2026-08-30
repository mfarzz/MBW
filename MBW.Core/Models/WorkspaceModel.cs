using System;
using System.Collections.Generic;

namespace MBW.Core.Models
{
    public class WorkspaceModel
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public EmailTemplate? Template { get; set; }
        public string? DataFilePath { get; set; }
        public string? DataSheetName { get; set; }
        public int DataHeaderRow { get; set; } = 1;
        public string? AttachmentsFolder { get; set; }
        public SendConfiguration? Configuration { get; set; }
        public IDictionary<string, string> Metadata { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
        public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;

        public IReadOnlyCollection<string> GetVariableNames()
        {
            if (Template == null)
                return Array.Empty<string>();

            // Very small helper: variables may be extracted by the importer; keep placeholder empty
            return new List<string>();
        }
    }
}

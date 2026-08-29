using System;
using System.Collections.Generic;

namespace MBW.Core.Models
{
    public class RecipientRow
    {
        public long RowNumber { get; init; }
        public IReadOnlyDictionary<string, string> Fields { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public RecipientRow(long rowNumber, IReadOnlyDictionary<string, string> fields)
        {
            RowNumber = rowNumber;
            Fields = fields ?? throw new ArgumentNullException(nameof(fields));
        }

        public bool TryGet(string key, out string? value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            return Fields.TryGetValue(key, out value);
        }

        public string? Get(string key)
        {
            _ = key ?? throw new ArgumentNullException(nameof(key));
            Fields.TryGetValue(key, out var v);
            return v;
        }
    }
}

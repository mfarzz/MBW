using System.Collections.Generic;

namespace MBW.App.ViewModels
{
    public sealed class DatabasePreviewRow
    {
        public DatabasePreviewRow(IReadOnlyList<string> cells)
        {
            Cells = cells;
        }

        public IReadOnlyList<string> Cells { get; }
    }
}

using System.Collections.Generic;

namespace MBW.Core.Models
{
    public sealed class ExcelSheetPreview
    {
        public ExcelSheetPreview(
            string sheetName,
            IReadOnlyList<string> headers,
            IReadOnlyList<RecipientRow> rows,
            long totalRows,
            int headerRow)
        {
            SheetName = sheetName;
            Headers = headers;
            Rows = rows;
            TotalRows = totalRows;
            HeaderRow = headerRow;
        }

        public string SheetName { get; }

        public IReadOnlyList<string> Headers { get; }

        public IReadOnlyList<RecipientRow> Rows { get; }

        public long TotalRows { get; }

        public int HeaderRow { get; }
    }

    public sealed class ExcelPageResult
    {
        public ExcelPageResult(
            IReadOnlyList<string> headers,
            IReadOnlyList<RecipientRow> rows,
            long totalRows,
            int page,
            int pageSize)
        {
            Headers = headers;
            Rows = rows;
            TotalRows = totalRows;
            Page = page;
            PageSize = pageSize;
        }

        public IReadOnlyList<string> Headers { get; }

        public IReadOnlyList<RecipientRow> Rows { get; }

        public long TotalRows { get; }

        public int Page { get; }

        public int PageSize { get; }

        public int TotalPages =>
            TotalRows <= 0 || PageSize <= 0
                ? 0
                : (int)((TotalRows + PageSize - 1) / PageSize);
    }
}

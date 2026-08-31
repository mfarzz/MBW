using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using MBW.Core.Interfaces;
using MBW.Core.Models;

namespace MBW.Infrastructure.Excel
{
    public class ExcelImporter : IExcelImporter
    {
        public async Task<IReadOnlyList<string>> GetSheetNamesAsync(string filePath, CancellationToken cancellationToken = default)
        {
            EnsureFileExists(filePath);

            return await Task.Run(() =>
            {
                using var workbook = new XLWorkbook(filePath);
                return (IReadOnlyList<string>)workbook.Worksheets
                    .Select(ws => ws.Name)
                    .ToList()
                    .AsReadOnly();
            }, cancellationToken);
        }

        public async Task<IReadOnlyList<string>> GetHeadersAsync(
            string filePath,
            string? sheetName = null,
            int headerRow = 1,
            CancellationToken cancellationToken = default)
        {
            EnsureFileExists(filePath);
            EnsureHeaderRow(headerRow);

            return await Task.Run(() =>
            {
                using var workbook = new XLWorkbook(filePath);
                var worksheet = ResolveWorksheet(workbook, sheetName);
                return (IReadOnlyList<string>)ExtractHeaders(worksheet, headerRow).AsReadOnly();
            }, cancellationToken);
        }

        public async Task<IReadOnlyList<RecipientRow>> PreviewAsync(
            string filePath,
            int maxRows = 10,
            string? sheetName = null,
            int headerRow = 1,
            CancellationToken cancellationToken = default)
        {
            var preview = await PreviewSheetAsync(filePath, sheetName, maxRows, headerRow, cancellationToken);
            return preview.Rows;
        }

        public async Task<ExcelSheetPreview> PreviewSheetAsync(
            string filePath,
            string? sheetName = null,
            int maxRows = 10,
            int headerRow = 1,
            CancellationToken cancellationToken = default)
        {
            EnsureFileExists(filePath);
            EnsureHeaderRow(headerRow);
            if (maxRows <= 0)
            {
                throw new ArgumentException("MaxRows must be greater than 0", nameof(maxRows));
            }

            return await Task.Run(() =>
            {
                using var workbook = new XLWorkbook(filePath);
                var worksheet = ResolveWorksheet(workbook, sheetName);
                var headers = ExtractHeaders(worksheet, headerRow);
                var totalRows = CountDataRows(worksheet, headerRow);
                var rows = ReadRows(worksheet, headers, headerRow, skip: 0, take: maxRows);

                return new ExcelSheetPreview(worksheet.Name, headers, rows, totalRows, headerRow);
            }, cancellationToken);
        }

        public async Task<ExcelPageResult> GetPageAsync(
            string filePath,
            int page,
            int pageSize = 50,
            string? sheetName = null,
            int headerRow = 1,
            CancellationToken cancellationToken = default)
        {
            EnsureFileExists(filePath);
            EnsureHeaderRow(headerRow);
            if (page < 1)
            {
                throw new ArgumentException("Page must be greater than 0", nameof(page));
            }

            if (pageSize <= 0)
            {
                throw new ArgumentException("PageSize must be greater than 0", nameof(pageSize));
            }

            return await Task.Run(() =>
            {
                using var workbook = new XLWorkbook(filePath);
                var worksheet = ResolveWorksheet(workbook, sheetName);
                var headers = ExtractHeaders(worksheet, headerRow);
                var totalRows = CountDataRows(worksheet, headerRow);
                var skip = (page - 1) * pageSize;
                var rows = ReadRows(worksheet, headers, headerRow, skip, pageSize);

                return new ExcelPageResult(headers, rows, totalRows, page, pageSize);
            }, cancellationToken);
        }

        public async Task<long> GetRowCountAsync(
            string filePath,
            string? sheetName = null,
            int headerRow = 1,
            CancellationToken cancellationToken = default)
        {
            EnsureFileExists(filePath);
            EnsureHeaderRow(headerRow);

            return await Task.Run(() =>
            {
                using var workbook = new XLWorkbook(filePath);
                var worksheet = ResolveWorksheet(workbook, sheetName);
                return CountDataRows(worksheet, headerRow);
            }, cancellationToken);
        }

        public async IAsyncEnumerable<RecipientRow> ReadAllAsync(
            string filePath,
            string? sheetName = null,
            int headerRow = 1,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            EnsureFileExists(filePath);
            EnsureHeaderRow(headerRow);

            var rows = await Task.Run(() =>
            {
                using var workbook = new XLWorkbook(filePath);
                var worksheet = ResolveWorksheet(workbook, sheetName);
                var headers = ExtractHeaders(worksheet, headerRow);
                return ReadRows(worksheet, headers, headerRow, skip: 0, take: int.MaxValue);
            }, cancellationToken);

            foreach (var row in rows)
            {
                yield return row;
            }
        }

        private static IXLWorksheet ResolveWorksheet(XLWorkbook workbook, string? sheetName)
        {
            if (string.IsNullOrWhiteSpace(sheetName))
            {
                return workbook.Worksheet(1);
            }

            if (!workbook.Worksheets.TryGetWorksheet(sheetName, out var worksheet))
            {
                throw new ArgumentException($"Sheet \"{sheetName}\" was not found in the workbook.", nameof(sheetName));
            }

            return worksheet;
        }

        private static List<string> ExtractHeaders(IXLWorksheet worksheet, int headerRow)
        {
            var headers = new List<string>();
            var row = worksheet.Row(headerRow);
            var lastColumn = row.LastCellUsed()?.Address.ColumnNumber ?? 0;

            for (var column = 1; column <= lastColumn; column++)
            {
                var headerValue = row.Cell(column).GetFormattedString()?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(headerValue))
                {
                    break;
                }

                headers.Add(headerValue);
            }

            return headers;
        }

        private static long CountDataRows(IXLWorksheet worksheet, int headerRow)
        {
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? headerRow;
            return Math.Max(0L, lastRow - headerRow);
        }

        private static IReadOnlyList<RecipientRow> ReadRows(
            IXLWorksheet worksheet,
            IReadOnlyList<string> headers,
            int headerRow,
            int skip,
            int take)
        {
            if (headers.Count == 0 || take <= 0)
            {
                return Array.Empty<RecipientRow>();
            }

            var rows = new List<RecipientRow>();
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? headerRow;
            var startRow = headerRow + 1 + skip;

            if (startRow > lastRow)
            {
                return rows;
            }

            var endRow = (long)startRow + take - 1 >= lastRow
                ? lastRow
                : (int)((long)startRow + take - 1);

            for (var rowNumber = startRow; rowNumber <= endRow; rowNumber++)
            {
                var recipient = CreateRecipientRow(rowNumber, worksheet.Row(rowNumber), headers);
                if (recipient is not null)
                {
                    rows.Add(recipient);
                }
            }

            return rows;
        }

        private static RecipientRow? CreateRecipientRow(long rowNumber, IXLRow row, IReadOnlyList<string> headers)
        {
            if (headers.Count == 0)
            {
                return null;
            }

            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
            {
                fields[headers[i]] = row.Cell(i + 1).GetFormattedString()?.Trim() ?? string.Empty;
            }

            return new RecipientRow(rowNumber, fields);
        }

        private static void EnsureFileExists(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path cannot be empty", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Excel file not found: {filePath}");
            }
        }

        private static void EnsureHeaderRow(int headerRow)
        {
            if (headerRow < 1)
            {
                throw new ArgumentException("Header row must be greater than 0", nameof(headerRow));
            }
        }
    }
}

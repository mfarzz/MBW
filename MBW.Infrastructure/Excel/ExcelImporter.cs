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
        public async Task<IReadOnlyList<string>> GetHeadersAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be empty", nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Excel file not found: {filePath}");

            return await Task.Run(() =>
            {
                using var workbook = new XLWorkbook(filePath);
                var worksheet = workbook.Worksheet(1);

                var headers = new List<string>();
                var headerRow = worksheet.Row(1);

                foreach (var cell in headerRow.Cells())
                {
                    var headerValue = cell.GetValue<string>()?.Trim() ?? string.Empty;
                    if (string.IsNullOrEmpty(headerValue))
                        break; // Stop at first empty column header

                    headers.Add(headerValue);
                }

                return (IReadOnlyList<string>)headers.AsReadOnly();
            }, cancellationToken);
        }

        public async Task<IReadOnlyList<RecipientRow>> PreviewAsync(string filePath, int maxRows = 10, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be empty", nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Excel file not found: {filePath}");
            if (maxRows <= 0)
                throw new ArgumentException("MaxRows must be greater than 0", nameof(maxRows));

            return await Task.Run(() =>
            {
                using var workbook = new XLWorkbook(filePath);
                var worksheet = workbook.Worksheet(1);

                var headers = ExtractHeaders(worksheet);
                var rows = new List<RecipientRow>();

                int rowCount = 0;
                foreach (var row in worksheet.RowsUsed())
                {
                    if (row.RowNumber() == 1) continue; // Skip header
                    if (rowCount >= maxRows) break;

                    var recipientRow = CreateRecipientRow(row.RowNumber(), row, headers);
                    if (recipientRow != null)
                    {
                        rows.Add(recipientRow);
                        rowCount++;
                    }
                }

                return (IReadOnlyList<RecipientRow>)rows.AsReadOnly();
            }, cancellationToken);
        }

        public async IAsyncEnumerable<RecipientRow> ReadAllAsync(string filePath, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be empty", nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Excel file not found: {filePath}");

            var rows = await Task.Run(() =>
            {
                using var workbook = new XLWorkbook(filePath);
                var worksheet = workbook.Worksheet(1);

                var headers = ExtractHeaders(worksheet);
                var result = new List<RecipientRow>();

                foreach (var row in worksheet.RowsUsed())
                {
                    if (row.RowNumber() == 1) continue; // Skip header

                    var recipientRow = CreateRecipientRow(row.RowNumber(), row, headers);
                    if (recipientRow != null)
                        result.Add(recipientRow);
                }

                return result;
            }, cancellationToken);

            foreach (var row in rows)
            {
                yield return row;
            }
        }

        private static List<string> ExtractHeaders(IXLWorksheet worksheet)
        {
            var headers = new List<string>();
            var headerRow = worksheet.Row(1);

            foreach (var cell in headerRow.Cells())
            {
                var headerValue = cell.GetValue<string>()?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(headerValue))
                    break; // Stop at first empty column header

                headers.Add(headerValue);
            }

            return headers;
        }

        private static RecipientRow? CreateRecipientRow(long rowNumber, IXLRow row, List<string> headers)
        {
            if (headers.Count == 0)
                return null;

            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < headers.Count; i++)
            {
                var cellValue = row.Cell(i + 1).GetValue<string>()?.Trim() ?? string.Empty;
                fields[headers[i]] = cellValue;
            }

            // Return row even if all fields are empty (caller may filter)
            return new RecipientRow(rowNumber, fields);
        }
    }
}

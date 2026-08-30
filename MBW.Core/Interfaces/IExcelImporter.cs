using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MBW.Core.Models;

namespace MBW.Core.Interfaces
{
    public interface IExcelImporter
    {
        Task<IReadOnlyList<string>> GetSheetNamesAsync(string filePath, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<string>> GetHeadersAsync(
            string filePath,
            string? sheetName = null,
            int headerRow = 1,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<RecipientRow>> PreviewAsync(
            string filePath,
            int maxRows = 10,
            string? sheetName = null,
            int headerRow = 1,
            CancellationToken cancellationToken = default);

        Task<ExcelSheetPreview> PreviewSheetAsync(
            string filePath,
            string? sheetName = null,
            int maxRows = 10,
            int headerRow = 1,
            CancellationToken cancellationToken = default);

        Task<ExcelPageResult> GetPageAsync(
            string filePath,
            int page,
            int pageSize = 50,
            string? sheetName = null,
            int headerRow = 1,
            CancellationToken cancellationToken = default);

        Task<long> GetRowCountAsync(
            string filePath,
            string? sheetName = null,
            int headerRow = 1,
            CancellationToken cancellationToken = default);

        IAsyncEnumerable<RecipientRow> ReadAllAsync(
            string filePath,
            string? sheetName = null,
            int headerRow = 1,
            CancellationToken cancellationToken = default);
    }
}

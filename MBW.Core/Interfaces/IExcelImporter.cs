using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MBW.Core.Models;

namespace MBW.Core.Interfaces
{
    public interface IExcelImporter
    {
        Task<IReadOnlyList<string>> GetHeadersAsync(string filePath, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<RecipientRow>> PreviewAsync(string filePath, int maxRows = 10, CancellationToken cancellationToken = default);
        Task<long> GetRowCountAsync(string filePath, CancellationToken cancellationToken = default);
        IAsyncEnumerable<RecipientRow> ReadAllAsync(string filePath, CancellationToken cancellationToken = default);
    }
}

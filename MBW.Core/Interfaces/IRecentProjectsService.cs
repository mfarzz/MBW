using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MBW.Core.Models;

namespace MBW.Core.Interfaces
{
    public interface IRecentProjectsService
    {
        Task<IReadOnlyList<RecentProjectEntry>> LoadAsync(CancellationToken cancellationToken = default);

        Task AddOrUpdateAsync(string name, string path, CancellationToken cancellationToken = default);

        Task RemoveAsync(string path, CancellationToken cancellationToken = default);
    }
}

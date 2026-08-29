using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MBW.Core.Models;

namespace MBW.Core.Interfaces
{
    public interface IStorageService
    {
        Task SaveWorkspacePackageAsync(WorkspaceModel workspace, string destinationPath, CancellationToken cancellationToken = default);
        Task<WorkspaceModel> OpenWorkspacePackageAsync(string sourcePath, CancellationToken cancellationToken = default);
    }
}

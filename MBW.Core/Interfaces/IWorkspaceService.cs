using System;
using System.Threading;
using System.Threading.Tasks;
using MBW.Core.Models;

namespace MBW.Core.Interfaces
{
    public interface IWorkspaceService
    {
        Task<WorkspaceModel> CreateAsync(string name, string location, CancellationToken cancellationToken = default);
        Task<WorkspaceModel> OpenAsync(string path, CancellationToken cancellationToken = default);
        Task SaveAsync(WorkspaceModel workspace, string path, CancellationToken cancellationToken = default);
    }
}

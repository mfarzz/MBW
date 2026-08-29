using System;
using System.Threading;
using System.Threading.Tasks;
using MBW.Core.Interfaces;
using MBW.Core.Models;

namespace MBW.Infrastructure.Services
{
    public class WorkspaceService : IWorkspaceService
    {
        private readonly IStorageService _storageService;

        public WorkspaceService(IStorageService storageService)
        {
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        }

        public async Task<WorkspaceModel> CreateAsync(string name, string location, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name cannot be empty", nameof(name));
            if (string.IsNullOrWhiteSpace(location)) throw new ArgumentException("Location cannot be empty", nameof(location));

            var workspace = new WorkspaceModel
            {
                Name = name,
                Template = new EmailTemplate { Subject = "", HtmlBody = "" },
                Configuration = new SendConfiguration()
            };

            await _storageService.SaveWorkspacePackageAsync(workspace, location, cancellationToken);
            return workspace;
        }

        public async Task<WorkspaceModel> OpenAsync(string path, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be empty", nameof(path));
            return await _storageService.OpenWorkspacePackageAsync(path, cancellationToken);
        }

        public async Task SaveAsync(WorkspaceModel workspace, string path, CancellationToken cancellationToken = default)
        {
            if (workspace == null) throw new ArgumentNullException(nameof(workspace));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be empty", nameof(path));

            await _storageService.SaveWorkspacePackageAsync(workspace, path, cancellationToken);
        }
    }
}

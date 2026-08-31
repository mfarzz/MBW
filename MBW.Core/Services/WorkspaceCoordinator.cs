using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MBW.Core.Interfaces;
using MBW.Core.Models;

namespace MBW.Core.Services
{
    /// <summary>
    /// Orchestrates workspace create/open/save using Core contracts.
    /// Holds the active workspace session for the running application.
    /// </summary>
    public sealed class WorkspaceCoordinator
    {
        private readonly IWorkspaceService _workspaceService;
        private readonly IWorkspaceUiGateway _uiGateway;

        public WorkspaceCoordinator(IWorkspaceService workspaceService, IWorkspaceUiGateway uiGateway)
        {
            _workspaceService = workspaceService ?? throw new ArgumentNullException(nameof(workspaceService));
            _uiGateway = uiGateway ?? throw new ArgumentNullException(nameof(uiGateway));
        }

        public WorkspaceModel? Current { get; private set; }

        public string? WorkspacePath { get; private set; }

        public bool HasWorkspace => Current is not null && !string.IsNullOrWhiteSpace(WorkspacePath);

        public event EventHandler? Changed;

        public async Task<bool> CreateNewAsync(CancellationToken cancellationToken = default)
        {
            var name = await _uiGateway.PromptWorkspaceNameAsync("New Workspace", "My Workspace", cancellationToken);
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var parentFolder = await _uiGateway.PickFolderPathAsync("Choose folder for new workspace", cancellationToken);
            if (string.IsNullOrEmpty(parentFolder))
            {
                return false;
            }

            var workspacePath = System.IO.Path.Combine(parentFolder, $"{SanitizeFolderName(name)}.mbw");
            if (Directory.Exists(workspacePath))
            {
                await _uiGateway.ShowMessageAsync("Workspace exists", $"A workspace already exists at:\n{workspacePath}", cancellationToken);
                return false;
            }

            var workspace = await _workspaceService.CreateAsync(name.Trim(), workspacePath, cancellationToken);
            SetSession(workspace, workspacePath);
            return true;
        }

        public async Task<bool> OpenExistingAsync(CancellationToken cancellationToken = default)
        {
            var workspacePath = await _uiGateway.PickFolderPathAsync("Open workspace folder", cancellationToken);
            if (string.IsNullOrEmpty(workspacePath))
            {
                return false;
            }

            return await OpenFromPathAsync(workspacePath, cancellationToken);
        }

        public async Task<bool> OpenFromPathAsync(string workspacePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(workspacePath) || !Directory.Exists(workspacePath))
            {
                await _uiGateway.ShowMessageAsync("Not found", "The workspace folder could not be found.", cancellationToken);
                return false;
            }

            if (!File.Exists(System.IO.Path.Combine(workspacePath, "workspace.json")))
            {
                await _uiGateway.ShowMessageAsync("Invalid workspace", "The selected folder is not a valid MBW workspace.", cancellationToken);
                return false;
            }

            var workspace = await _workspaceService.OpenAsync(workspacePath, cancellationToken);
            SetSession(workspace, workspacePath);
            return true;
        }

        public async Task<bool> SaveCurrentAsync(CancellationToken cancellationToken = default)
        {
            if (!HasWorkspace)
            {
                await _uiGateway.ShowMessageAsync("No workspace", "Create or open a workspace before saving.", cancellationToken);
                return false;
            }

            await _workspaceService.SaveAsync(Current!, WorkspacePath!, cancellationToken);
            Changed?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public void UpdateCurrentTemplate(EmailTemplate template)
        {
            if (Current is null || template is null)
            {
                return;
            }

            Current.Template = template;
        }

        public void UpdateDataFilePath(string relativePath, string? sheetName = null, int headerRow = 1)
        {
            if (Current is null || string.IsNullOrWhiteSpace(relativePath))
            {
                return;
            }

            Current.DataFilePath = relativePath.Replace('\\', '/');
            Current.DataSheetName = string.IsNullOrWhiteSpace(sheetName) ? null : sheetName.Trim();
            Current.DataHeaderRow = headerRow > 0 ? headerRow : 1;
            Current.ModifiedAt = DateTimeOffset.UtcNow;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public string? GetResolvedDataFilePath()
        {
            if (!HasWorkspace || string.IsNullOrWhiteSpace(Current!.DataFilePath))
            {
                return null;
            }

            var path = Current.DataFilePath;
            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(WorkspacePath!, path);
            }

            return File.Exists(path) ? path : null;
        }

        public string? GetDataSheetName() => Current?.DataSheetName;

        public int GetDataHeaderRow() =>
            Current is null || Current.DataHeaderRow < 1 ? 1 : Current.DataHeaderRow;

        public AttachmentConfiguration GetAttachmentConfiguration()
        {
            if (Current is null)
            {
                return AttachmentConfiguration.CreateDefault();
            }

            return Current.AttachmentConfiguration;
        }

        public void UpdateAttachmentConfiguration(AttachmentConfiguration configuration)
        {
            if (Current is null || configuration is null)
            {
                return;
            }

            Current.AttachmentConfiguration = configuration.Clone();
            Current.ModifiedAt = DateTimeOffset.UtcNow;
        }

        public string GetSharedAttachmentsDirectory() =>
            Path.Combine(WorkspacePath!, "attachments", "shared");

        public string GetIndividualAttachmentsDirectory() =>
            Path.Combine(WorkspacePath!, "attachments", "individual");

        public string? ResolveWorkspacePath(string? relativeOrAbsolutePath)
        {
            if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath))
            {
                return null;
            }

            var path = relativeOrAbsolutePath.Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(path))
            {
                return Directory.Exists(path) || File.Exists(path) ? path : null;
            }

            if (!HasWorkspace)
            {
                return null;
            }

            var resolved = Path.Combine(WorkspacePath!, path);
            return Directory.Exists(resolved) || File.Exists(resolved) ? resolved : null;
        }

        public string ToRelativePath(string absolutePath)
        {
            if (!HasWorkspace || string.IsNullOrWhiteSpace(absolutePath))
            {
                return absolutePath;
            }

            var workspace = Path.GetFullPath(WorkspacePath!);
            var full = Path.GetFullPath(absolutePath);
            if (full.StartsWith(workspace, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetRelativePath(workspace, full).Replace('\\', '/');
            }

            return absolutePath;
        }

        public void EnsureAttachmentDirectories()
        {
            if (!HasWorkspace)
            {
                return;
            }

            Directory.CreateDirectory(GetSharedAttachmentsDirectory());
            Directory.CreateDirectory(GetIndividualAttachmentsDirectory());
        }

        private void SetSession(WorkspaceModel workspace, string path)
        {
            Current = workspace;
            WorkspacePath = path;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private static string SanitizeFolderName(string name)
        {
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            return name.Trim();
        }
    }
}

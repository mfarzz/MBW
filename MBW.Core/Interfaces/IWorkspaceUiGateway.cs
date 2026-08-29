using System.Threading;
using System.Threading.Tasks;

namespace MBW.Core.Interfaces
{
    /// <summary>
    /// Platform-agnostic UI prompts for workspace file operations.
    /// Implemented by the presentation layer (WinUI).
    /// </summary>
    public interface IWorkspaceUiGateway
    {
        Task<string?> PromptWorkspaceNameAsync(string title, string defaultName, CancellationToken cancellationToken = default);

        Task<string?> PickFolderPathAsync(string title, CancellationToken cancellationToken = default);

        Task ShowMessageAsync(string title, string message, CancellationToken cancellationToken = default);
    }
}

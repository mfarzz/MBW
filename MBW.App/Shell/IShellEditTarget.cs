using System.Threading.Tasks;

namespace MBW.App.Shell
{
    /// <summary>
    /// Optional contract for pages that accept top-bar Edit commands.
    /// </summary>
    public interface IShellEditTarget
    {
        bool CanUndo { get; }
        bool CanRedo { get; }
        bool CanCut { get; }
        bool CanCopy { get; }
        bool CanPaste { get; }
        bool CanPastePlain { get; }
        bool CanSelectAll { get; }

        Task UndoAsync();
        Task RedoAsync();
        Task CutAsync();
        Task CopyAsync();
        Task PasteAsync();
        Task PastePlainAsync();
        Task SelectAllAsync();
    }

    public interface IShellRefreshable
    {
        Task RefreshAsync();
    }
}

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MBW.Core.Models;

namespace MBW.Core.Interfaces
{
    public interface IAttachmentService
    {
        Task<IReadOnlyList<string>> ListAttachmentsAsync(string folderPath, CancellationToken cancellationToken = default);

        Task<int> CountAttachmentsAsync(string folderPath, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<AttachmentMatch>> MatchAsync(
            string folderPath,
            IEnumerable<RecipientRow> recipients,
            string pattern,
            CancellationToken cancellationToken = default);

        Task<string> CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);

        Task<int> ImportFolderAsync(string sourceFolder, string destinationFolder, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<AttachmentDirectoryEntry>> ListDirectoryEntriesAsync(
            string folderPath,
            bool directoriesOnly = false,
            CancellationToken cancellationToken = default);

        Task CreateFolderAsync(string folderPath, CancellationToken cancellationToken = default);

        Task DeletePathAsync(string path, CancellationToken cancellationToken = default);

        string ResolvePattern(string pattern, IReadOnlyDictionary<string, string> fields);
    }
}

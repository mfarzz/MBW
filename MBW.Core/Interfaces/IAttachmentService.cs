using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MBW.Core.Models;

namespace MBW.Core.Interfaces
{
    public interface IAttachmentService
    {
        /// <summary>
        /// List attachment files from a folder.
        /// </summary>
        Task<IReadOnlyList<string>> ListAttachmentsAsync(string folderPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Match attachments by a pattern template (e.g. "{NIM}.pdf") against recipient rows.
        /// Returns list of matches for each file discovered.
        /// </summary>
        Task<IReadOnlyList<AttachmentMatch>> MatchAsync(string folderPath, IEnumerable<RecipientRow> recipients, string pattern, CancellationToken cancellationToken = default);
    }
}

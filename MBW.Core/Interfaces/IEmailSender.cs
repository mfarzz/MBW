using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MBW.Core.Models;

namespace MBW.Core.Interfaces
{
    public interface IEmailSender
    {
        /// <summary>
        /// Test connectivity to SMTP given the send configuration.
        /// </summary>
        Task TestConnectionAsync(SendConfiguration config, CancellationToken cancellationToken = default);

        /// <summary>
        /// Send a single email for the recipient using the provided template and configuration.
        /// </summary>
        Task<SendResult> SendAsync(
            RecipientRow recipient,
            EmailTemplate template,
            SendConfiguration config,
            IReadOnlyList<SendEmailAttachment> attachments,
            CancellationToken cancellationToken = default);
    }
}

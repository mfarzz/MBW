using System.Threading;
using System.Threading.Tasks;
using MBW.Core.Models;

namespace MBW.Core.Interfaces
{
    public interface ISmtpSettingsService
    {
        Task<SmtpSettings> LoadAsync(CancellationToken cancellationToken = default);

        Task SaveAsync(SmtpSettings settings, string password, CancellationToken cancellationToken = default);

        Task<string?> LoadPasswordAsync(CancellationToken cancellationToken = default);

        Task TestConnectionAsync(SmtpSettings settings, string password, CancellationToken cancellationToken = default);
    }
}

using System.Threading;
using System.Threading.Tasks;

namespace MBW.Core.Interfaces
{
    public interface ISmtpSettingsUiGateway
    {
        /// <returns>True if saved, false if cancelled.</returns>
        Task<bool> ShowEditorAsync(CancellationToken cancellationToken = default);
    }
}

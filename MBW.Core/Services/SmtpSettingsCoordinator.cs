using System;
using System.Threading;
using System.Threading.Tasks;
using MBW.Core.Interfaces;
using MBW.Core.Models;

namespace MBW.Core.Services
{
    public sealed class SmtpSettingsCoordinator
    {
        private readonly ISmtpSettingsService _settingsService;
        private readonly ISmtpSettingsUiGateway _uiGateway;

        public SmtpSettingsCoordinator(ISmtpSettingsService settingsService, ISmtpSettingsUiGateway uiGateway)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _uiGateway = uiGateway ?? throw new ArgumentNullException(nameof(uiGateway));
        }

        public SmtpSettings Current { get; private set; } = new();

        public bool IsConnected { get; private set; }

        public string StatusLabel => IsConnected ? "SMTP: Connected" : "SMTP: Not connected";

        public event EventHandler? Changed;

        public async Task LoadAsync(CancellationToken cancellationToken = default)
        {
            Current = await _settingsService.LoadAsync(cancellationToken);
            IsConnected = Current.IsConfigured;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public async Task<bool> ShowEditorAsync(CancellationToken cancellationToken = default)
        {
            var saved = await _uiGateway.ShowEditorAsync(cancellationToken);
            if (saved)
            {
                await LoadAsync(cancellationToken);
            }

            return saved;
        }

        public void MarkConnected(bool connected)
        {
            IsConnected = connected;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}

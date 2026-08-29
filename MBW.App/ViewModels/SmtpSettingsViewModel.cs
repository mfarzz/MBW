using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MBW.Core.Interfaces;
using MBW.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MBW.App.ViewModels
{
    public partial class SmtpSettingsViewModel : ObservableObject
    {
        private readonly ISmtpSettingsService _settingsService;

        public SmtpSettingsViewModel(ISmtpSettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        [ObservableProperty]
        public partial string Server { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string PortText { get; set; } = "587";

        [ObservableProperty]
        public partial string Username { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Password { get; set; } = string.Empty;

        [ObservableProperty]
        public partial int SelectedSecurityIndex { get; set; } = 1;

        [ObservableProperty]
        public partial string StatusMessage { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsBusy { get; set; }

        public IReadOnlyList<string> SecurityOptions { get; } =
        [
            "None",
            "STARTTLS",
            "SSL/TLS"
        ];

        public async Task LoadAsync()
        {
            var settings = await _settingsService.LoadAsync();
            Server = settings.Server;
            PortText = settings.Port.ToString();
            Username = settings.Username;
            SelectedSecurityIndex = (int)settings.Security;
            Password = await _settingsService.LoadPasswordAsync() ?? string.Empty;
            StatusMessage = string.Empty;
        }

        public async Task<bool> SaveAsync()
        {
            if (!TryBuildSettings(out var settings, out var error))
            {
                StatusMessage = error;
                return false;
            }

            try
            {
                IsBusy = true;
                await _settingsService.SaveAsync(settings, Password);
                StatusMessage = "Settings saved.";
                return true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Save failed: {ex.Message}";
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task TestConnectionAsync()
        {
            if (!TryBuildSettings(out var settings, out var error))
            {
                StatusMessage = error;
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = "Testing connection...";
                await _settingsService.TestConnectionAsync(settings, Password);
                StatusMessage = "Connection successful.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Connection failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool TryBuildSettings(out SmtpSettings settings, out string error)
        {
            settings = new SmtpSettings();
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(Server))
            {
                error = "SMTP server is required.";
                return false;
            }

            if (!int.TryParse(PortText, out var port) || port <= 0)
            {
                error = "Port must be a valid number.";
                return false;
            }

            settings.Server = Server.Trim();
            settings.Port = port;
            settings.Username = Username.Trim();
            settings.Security = SelectedSecurityIndex switch
            {
                0 => SmtpSecurityMode.None,
                2 => SmtpSecurityMode.SslTls,
                _ => SmtpSecurityMode.StartTls
            };
            return true;
        }
    }
}

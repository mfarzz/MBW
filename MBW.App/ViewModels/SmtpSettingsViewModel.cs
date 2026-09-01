using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MBW.Core.Interfaces;
using MBW.Core.Models;
using Microsoft.UI.Xaml;
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
        public partial string FromName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string FromEmail { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool UseReplyToAddress { get; set; }

        [ObservableProperty]
        public partial string ReplyToEmail { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Server { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string PortText { get; set; } = "587";

        [ObservableProperty]
        public partial string Username { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Password { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool RequiresAuthentication { get; set; } = true;

        [ObservableProperty]
        public partial bool IsServerSectionExpanded { get; set; }

        [ObservableProperty]
        public partial bool UseSecureConnection { get; set; } = true;

        public Visibility ServerSectionVisibility =>
            IsServerSectionExpanded ? Visibility.Visible : Visibility.Collapsed;

        public string ServerSectionChevronGlyph =>
            IsServerSectionExpanded ? "\uE70E" : "\uE70D";

        partial void OnIsServerSectionExpandedChanged(bool value)
        {
            OnPropertyChanged(nameof(ServerSectionVisibility));
            OnPropertyChanged(nameof(ServerSectionChevronGlyph));
        }

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

        partial void OnUseSecureConnectionChanged(bool value)
        {
            if (value && SelectedSecurityIndex == 0)
            {
                SelectedSecurityIndex = 1;
            }
            else if (!value)
            {
                SelectedSecurityIndex = 0;
            }
        }

        public async Task LoadAsync()
        {
            var settings = await _settingsService.LoadAsync();
            FromName = settings.FromName;
            FromEmail = settings.FromEmail;
            UseReplyToAddress = settings.UseReplyToAddress;
            ReplyToEmail = settings.ReplyToEmail;
            Server = settings.Server;
            PortText = settings.Port.ToString();
            Username = settings.Username;
            RequiresAuthentication = settings.RequiresAuthentication;
            SelectedSecurityIndex = (int)settings.Security;
            UseSecureConnection = settings.Security != SmtpSecurityMode.None;
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
                StatusMessage = $"Failed to save: {ex.Message}";
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
                error = "SMTP server name is required.";
                return false;
            }

            if (!int.TryParse(PortText, out var port) || port <= 0)
            {
                error = "Port must be a valid number.";
                return false;
            }

            settings.FromName = FromName.Trim();
            settings.FromEmail = FromEmail.Trim();
            settings.UseReplyToAddress = UseReplyToAddress;
            settings.ReplyToEmail = ReplyToEmail.Trim();
            settings.Server = Server.Trim();
            settings.Port = port;
            settings.RequiresAuthentication = RequiresAuthentication;
            settings.Username = RequiresAuthentication ? Username.Trim() : string.Empty;
            settings.Security = UseSecureConnection
                ? SelectedSecurityIndex switch
                {
                    2 => SmtpSecurityMode.SslTls,
                    _ => SmtpSecurityMode.StartTls
                }
                : SmtpSecurityMode.None;
            return true;
        }
    }
}

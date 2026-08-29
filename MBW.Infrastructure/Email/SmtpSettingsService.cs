using System;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MBW.Core.Interfaces;
using MBW.Core.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Windows.Security.Credentials;

namespace MBW.Infrastructure.Email
{
    public sealed class SmtpSettingsService : ISmtpSettingsService
    {
        private const string CredentialResource = "MBW.Smtp";
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        private readonly string _settingsFilePath;

        public SmtpSettingsService()
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MBW");
            Directory.CreateDirectory(folder);
            _settingsFilePath = Path.Combine(folder, "smtp-settings.json");
        }

        public async Task<SmtpSettings> LoadAsync(CancellationToken cancellationToken = default)
        {
            if (!File.Exists(_settingsFilePath))
            {
                return new SmtpSettings();
            }

            await using var stream = File.OpenRead(_settingsFilePath);
            var settings = await JsonSerializer.DeserializeAsync<SmtpSettings>(stream, JsonOptions, cancellationToken);
            return settings ?? new SmtpSettings();
        }

        public async Task SaveAsync(SmtpSettings settings, string password, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(settings);

            await using (var stream = File.Create(_settingsFilePath))
            {
                await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(settings.Username) && !string.IsNullOrEmpty(password))
            {
                SavePassword(settings.Username, password);
            }
        }

        public Task<string?> LoadPasswordAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(LoadPassword());
        }

        public async Task TestConnectionAsync(SmtpSettings settings, string password, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var server = NormalizeServer(settings.Server, out var embeddedPort);
            if (string.IsNullOrWhiteSpace(server))
            {
                throw new InvalidOperationException("SMTP server is required.");
            }

            var port = embeddedPort ?? settings.Port;
            if (port <= 0)
            {
                throw new InvalidOperationException("Port must be a valid number.");
            }

            var secureSocketOptions = ResolveSecurity(settings.Security, port);

            try
            {
                await ConnectAndAuthenticateAsync(server, port, secureSocketOptions, settings.Username, password, cancellationToken);
            }
            catch (Exception ex) when (ShouldRetryWithAuto(secureSocketOptions, ex))
            {
                await ConnectAndAuthenticateAsync(server, port, SecureSocketOptions.Auto, settings.Username, password, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(FormatConnectionError(ex, server, port, secureSocketOptions), ex);
            }
        }

        private static async Task ConnectAndAuthenticateAsync(
            string server,
            int port,
            SecureSocketOptions secureSocketOptions,
            string username,
            string password,
            CancellationToken cancellationToken)
        {
            using var client = new SmtpClient();
            client.Timeout = 15000;

            await client.ConnectAsync(server, port, secureSocketOptions, cancellationToken);

            if (!string.IsNullOrWhiteSpace(username))
            {
                await client.AuthenticateAsync(username, password, cancellationToken);
            }

            await client.DisconnectAsync(true, cancellationToken);
        }

        private static string NormalizeServer(string server, out int? embeddedPort)
        {
            embeddedPort = null;
            var value = server.Trim();

            if (value.StartsWith("smtp://", StringComparison.OrdinalIgnoreCase))
            {
                value = value["smtp://".Length..];
            }

            if (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                value = value["https://".Length..];
            }

            if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                value = value["http://".Length..];
            }

            value = value.Trim().TrimEnd('/');

            var separatorIndex = value.LastIndexOf(':');
            if (separatorIndex > 0 && int.TryParse(value[(separatorIndex + 1)..], out var port))
            {
                embeddedPort = port;
                value = value[..separatorIndex];
            }

            return value;
        }

        private static SecureSocketOptions ResolveSecurity(SmtpSecurityMode security, int port) =>
            security switch
            {
                SmtpSecurityMode.SslTls => SecureSocketOptions.SslOnConnect,
                SmtpSecurityMode.StartTls when port == 465 => SecureSocketOptions.SslOnConnect,
                SmtpSecurityMode.StartTls => SecureSocketOptions.StartTls,
                SmtpSecurityMode.None when port is 465 or 993 => SecureSocketOptions.SslOnConnect,
                SmtpSecurityMode.None when port is 587 or 25 => SecureSocketOptions.StartTlsWhenAvailable,
                _ => SecureSocketOptions.None
            };

        private static bool ShouldRetryWithAuto(SecureSocketOptions options, Exception ex)
        {
            if (options == SecureSocketOptions.Auto)
            {
                return false;
            }

            var root = ex;
            while (root.InnerException is not null)
            {
                root = root.InnerException;
            }

            return root is SocketException or IOException or SslHandshakeException;
        }

        private static string FormatConnectionError(Exception ex, string server, int port, SecureSocketOptions security)
        {
            var root = ex;
            while (root.InnerException is not null)
            {
                root = root.InnerException;
            }

            if (root is SocketException { SocketErrorCode: SocketError.HostNotFound })
            {
                return $"Server \"{server}\" was not found. Check the SMTP host name.";
            }

            var message = root.Message;
            if (message.Contains("no data of the requested type", StringComparison.OrdinalIgnoreCase))
            {
                return $"Cannot resolve \"{server}\". Use the SMTP host (for example smtp.gmail.com), not an email address.";
            }

            if (message.Contains("authentication", StringComparison.OrdinalIgnoreCase))
            {
                return "Authentication failed. Check username and password.";
            }

            if (message.Contains("connection refused", StringComparison.OrdinalIgnoreCase))
            {
                return $"Connection refused on port {port}. Check port and security settings.";
            }

            return $"Could not connect to {server}:{port} ({DescribeSecurity(security)}). {message}";
        }

        private static string DescribeSecurity(SecureSocketOptions security) =>
            security switch
            {
                SecureSocketOptions.SslOnConnect => "SSL/TLS",
                SecureSocketOptions.StartTls => "STARTTLS",
                SecureSocketOptions.StartTlsWhenAvailable => "STARTTLS when available",
                SecureSocketOptions.Auto => "Auto",
                _ => "None"
            };

        private static void SavePassword(string username, string password)
        {
            var vault = new PasswordVault();
            RemoveExistingCredentials(vault, username);
            vault.Add(new PasswordCredential(CredentialResource, username, password));
        }

        private static string? LoadPassword()
        {
            var vault = new PasswordVault();
            var credentials = vault.RetrieveAll();
            foreach (var credential in credentials)
            {
                if (!string.Equals(credential.Resource, CredentialResource, StringComparison.Ordinal))
                {
                    continue;
                }

                var retrieved = vault.Retrieve(credential.Resource, credential.UserName);
                return retrieved.Password;
            }

            return null;
        }

        private static void RemoveExistingCredentials(PasswordVault vault, string username)
        {
            var credentials = vault.RetrieveAll();
            foreach (var credential in credentials)
            {
                if (string.Equals(credential.Resource, CredentialResource, StringComparison.Ordinal)
                    && string.Equals(credential.UserName, username, StringComparison.Ordinal))
                {
                    vault.Remove(credential);
                }
            }
        }
    }
}

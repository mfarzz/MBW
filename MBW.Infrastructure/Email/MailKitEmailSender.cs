using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using MBW.Core.Interfaces;
using MBW.Core.Models;
using MimeKit;

namespace MBW.Infrastructure.Email
{
    public sealed class MailKitEmailSender : IEmailSender
    {
        private readonly ISmtpSettingsService _settingsService;

        public MailKitEmailSender(ISmtpSettingsService settingsService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        }

        public async Task TestConnectionAsync(SendConfiguration config, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(config);

            var settings = await _settingsService.LoadAsync(cancellationToken);
            var password = await _settingsService.LoadPasswordAsync(cancellationToken) ?? string.Empty;
            await _settingsService.TestConnectionAsync(settings, password, cancellationToken);
        }

        public async Task<SendResult> SendAsync(
            RecipientRow recipient,
            EmailTemplate template,
            SendConfiguration config,
            IReadOnlyList<SendEmailAttachment> attachments,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(recipient);
            ArgumentNullException.ThrowIfNull(template);
            ArgumentNullException.ThrowIfNull(config);
            attachments ??= Array.Empty<SendEmailAttachment>();

            try
            {
                var settings = await _settingsService.LoadAsync(cancellationToken);
                if (!settings.IsConfigured)
                {
                    return new SendResult(recipient.RowNumber, false, "SMTP is not configured.");
                }

                var password = await _settingsService.LoadPasswordAsync(cancellationToken) ?? string.Empty;
                var recipientEmail = ResolveRecipientEmail(recipient, config);
                if (string.IsNullOrWhiteSpace(recipientEmail))
                {
                    return new SendResult(recipient.RowNumber, false, "No email address.");
                }

                var rendered = template.RenderForRecipient(recipient);
                var deliveryEmail = recipientEmail;
                if (config.TestMode)
                {
                    deliveryEmail = ResolveSenderEmail(settings, config);
                    if (string.IsNullOrWhiteSpace(deliveryEmail))
                    {
                        return new SendResult(recipient.RowNumber, false, "Test mode requires a from email in SMTP settings.");
                    }

                    rendered = new EmailTemplate($"[TEST] {rendered.Subject}", rendered.HtmlBody)
                    {
                        PlainTextBody = rendered.PlainTextBody
                    };
                }

                var message = BuildMessage(settings, config, deliveryEmail, rendered, attachments);
                await SendMessageAsync(settings, password, message, cancellationToken);

                return new SendResult(recipient.RowNumber, true);
            }
            catch (Exception ex)
            {
                return new SendResult(recipient.RowNumber, false, ex.Message);
            }
        }

        private static async Task SendMessageAsync(
            SmtpSettings settings,
            string password,
            MimeMessage message,
            CancellationToken cancellationToken)
        {
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
                await ConnectSendAndDisconnectAsync(server, port, secureSocketOptions, settings, password, message, cancellationToken);
            }
            catch (Exception ex) when (ShouldRetryWithAuto(secureSocketOptions, ex))
            {
                await ConnectSendAndDisconnectAsync(server, port, SecureSocketOptions.Auto, settings, password, message, cancellationToken);
            }
        }

        private static async Task ConnectSendAndDisconnectAsync(
            string server,
            int port,
            SecureSocketOptions secureSocketOptions,
            SmtpSettings settings,
            string password,
            MimeMessage message,
            CancellationToken cancellationToken)
        {
            using var client = new SmtpClient { Timeout = 30000 };

            await client.ConnectAsync(server, port, secureSocketOptions, cancellationToken);

            if (settings.RequiresAuthentication && !string.IsNullOrWhiteSpace(settings.Username))
            {
                await client.AuthenticateAsync(settings.Username, password, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }

        private static MimeMessage BuildMessage(
            SmtpSettings settings,
            SendConfiguration config,
            string toEmail,
            EmailTemplate rendered,
            IReadOnlyList<SendEmailAttachment> attachments)
        {
            var fromEmail = ResolveSenderEmail(settings, config);
            if (string.IsNullOrWhiteSpace(fromEmail))
            {
                throw new InvalidOperationException("From email is required.");
            }

            var fromName = !string.IsNullOrWhiteSpace(config.FromName)
                ? config.FromName.Trim()
                : settings.FromName.Trim();

            var message = new MimeMessage();
            message.From.Add(string.IsNullOrWhiteSpace(fromName)
                ? MailboxAddress.Parse(fromEmail)
                : new MailboxAddress(fromName, fromEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = rendered.Subject ?? string.Empty;

            if (settings.UseReplyToAddress && !string.IsNullOrWhiteSpace(settings.ReplyToEmail))
            {
                message.ReplyTo.Add(MailboxAddress.Parse(settings.ReplyToEmail.Trim()));
            }

            message.Date = DateTimeOffset.Now;

            var (html, plainText) = EmailBodyFormatter.Format(rendered.HtmlBody, rendered.PlainTextBody);
            var bodyBuilder = new BodyBuilder();

            if (!string.IsNullOrWhiteSpace(plainText))
            {
                bodyBuilder.TextBody = plainText;
            }

            if (!string.IsNullOrWhiteSpace(html))
            {
                bodyBuilder.HtmlBody = html;
            }

            AddAttachments(bodyBuilder, attachments);

            message.Body = bodyBuilder.ToMessageBody();
            return message;
        }

        private static void AddAttachments(BodyBuilder bodyBuilder, IReadOnlyList<SendEmailAttachment> attachments)
        {
            foreach (var attachment in attachments)
            {
                if (string.IsNullOrWhiteSpace(attachment.FilePath) || !File.Exists(attachment.FilePath))
                {
                    continue;
                }

                var entity = bodyBuilder.Attachments.Add(attachment.FilePath);
                if (!string.IsNullOrWhiteSpace(attachment.FileName))
                {
                    if (entity.ContentDisposition is not null)
                    {
                        entity.ContentDisposition.FileName = attachment.FileName;
                    }

                    entity.ContentType.Name = attachment.FileName;
                }
            }
        }

        private static string? ResolveRecipientEmail(RecipientRow recipient, SendConfiguration config)
        {
            if (string.IsNullOrWhiteSpace(config.EmailColumn))
            {
                return null;
            }

            var value = recipient.Get(config.EmailColumn)?.Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static string ResolveSenderEmail(SmtpSettings settings, SendConfiguration config)
        {
            if (!string.IsNullOrWhiteSpace(config.FromEmail))
            {
                return config.FromEmail.Trim();
            }

            return settings.GetSenderEmail();
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
    }
}

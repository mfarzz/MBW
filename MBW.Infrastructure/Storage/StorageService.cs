using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MBW.Core.Interfaces;
using MBW.Core.Models;

namespace MBW.Infrastructure.Storage
{
    public class StorageService : IStorageService
    {
        private const string WorkspaceMetadataFile = "workspace.json";
        private const string EmailTemplateFile = "email.html";
        private const string DataFolder = "data";
        private const string AttachmentsFolder = "attachments";
        private const string LogsFolder = "logs";

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public async Task SaveWorkspacePackageAsync(WorkspaceModel workspace, string destinationPath, CancellationToken cancellationToken = default)
        {
            if (workspace == null) throw new ArgumentNullException(nameof(workspace));
            if (string.IsNullOrWhiteSpace(destinationPath)) throw new ArgumentException("Path cannot be empty", nameof(destinationPath));

            // Ensure destination folder exists
            Directory.CreateDirectory(destinationPath);

            // Create subdirectories
            Directory.CreateDirectory(Path.Combine(destinationPath, DataFolder));
            Directory.CreateDirectory(Path.Combine(destinationPath, AttachmentsFolder));
            Directory.CreateDirectory(Path.Combine(destinationPath, AttachmentsFolder, "shared"));
            Directory.CreateDirectory(Path.Combine(destinationPath, AttachmentsFolder, "individual"));
            Directory.CreateDirectory(Path.Combine(destinationPath, LogsFolder));

            // Serialize and save workspace.json
            var metadataPath = Path.Combine(destinationPath, WorkspaceMetadataFile);
            var serializableModel = new SerializableWorkspaceModel(workspace);
            var json = JsonSerializer.Serialize(serializableModel, _jsonOptions);
            await File.WriteAllTextAsync(metadataPath, json, cancellationToken);

            // Save email.html
            if (workspace.Template != null)
            {
                var emailPath = Path.Combine(destinationPath, EmailTemplateFile);
                await File.WriteAllTextAsync(emailPath, workspace.Template.HtmlBody ?? "", cancellationToken);
            }

            workspace.ModifiedAt = DateTimeOffset.UtcNow;
        }

        public async Task<WorkspaceModel> OpenWorkspacePackageAsync(string sourcePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourcePath)) throw new ArgumentException("Path cannot be empty", nameof(sourcePath));
            if (!Directory.Exists(sourcePath)) throw new DirectoryNotFoundException($"Workspace folder not found: {sourcePath}");

            // Read workspace.json
            var metadataPath = Path.Combine(sourcePath, WorkspaceMetadataFile);
            if (!File.Exists(metadataPath)) throw new FileNotFoundException($"workspace.json not found in {sourcePath}");

            var json = await File.ReadAllTextAsync(metadataPath, cancellationToken);
            var serializableModel = JsonSerializer.Deserialize<SerializableWorkspaceModel>(json, _jsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize workspace.json");

            // Read email.html if it exists
            var emailPath = Path.Combine(sourcePath, EmailTemplateFile);
            var htmlBody = File.Exists(emailPath) ? await File.ReadAllTextAsync(emailPath, cancellationToken) : string.Empty;

            // Reconstruct WorkspaceModel
            var workspace = serializableModel.ToWorkspaceModel();
            if (workspace.Template != null)
            {
                workspace.Template.HtmlBody = htmlBody;
            }

            return workspace;
        }

        /// <summary>
        /// Internal serializable model to preserve all WorkspaceModel data in JSON.
        /// </summary>
        private class SerializableWorkspaceModel
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public EmailTemplateDto? Template { get; set; }
            public string? DataFilePath { get; set; }
            public string? DataSheetName { get; set; }
            public int DataHeaderRow { get; set; } = 1;
            public string? AttachmentsFolder { get; set; }
            public AttachmentConfigurationDto? AttachmentConfiguration { get; set; }
            public SendConfigurationDto? Configuration { get; set; }
            public Dictionary<string, string>? Metadata { get; set; }
            public DateTimeOffset CreatedAt { get; set; }
            public DateTimeOffset ModifiedAt { get; set; }

            public SerializableWorkspaceModel() { }

            public SerializableWorkspaceModel(WorkspaceModel model)
            {
                Id = model.Id;
                Name = model.Name;
                Description = model.Description;
                Template = model.Template != null ? new EmailTemplateDto
                {
                    Subject = model.Template.Subject,
                    PlainTextBody = model.Template.PlainTextBody
                    // HtmlBody saved separately to email.html
                } : null;
                DataFilePath = model.DataFilePath;
                DataSheetName = model.DataSheetName;
                DataHeaderRow = model.DataHeaderRow > 0 ? model.DataHeaderRow : 1;
                AttachmentsFolder = model.AttachmentsFolder;
                AttachmentConfiguration = AttachmentConfigurationDto.FromModel(model.AttachmentConfiguration);
                Configuration = model.Configuration != null ? new SendConfigurationDto
                {
                    SmtpAccountId = model.Configuration.SmtpAccountId,
                    DelayMilliseconds = model.Configuration.DelayMilliseconds,
                    Concurrency = model.Configuration.Concurrency,
                    FromName = model.Configuration.FromName,
                    FromEmail = model.Configuration.FromEmail,
                    TestMode = model.Configuration.TestMode,
                    EmailColumn = model.Configuration.EmailColumn,
                    IncludeSharedAttachments = model.Configuration.IncludeSharedAttachments,
                    IncludeIndividualAttachments = model.Configuration.IncludeIndividualAttachments,
                    AttachmentRenamePattern = model.Configuration.AttachmentRenamePattern,
                    SendAllRecipients = model.Configuration.SendAllRecipients,
                    SendRangeFrom = model.Configuration.SendRangeFrom,
                    SendRangeTo = model.Configuration.SendRangeTo
                } : null;
                Metadata = model.Metadata != null ? new Dictionary<string, string>(model.Metadata) : null;
                CreatedAt = model.CreatedAt;
                ModifiedAt = model.ModifiedAt;
            }

            public WorkspaceModel ToWorkspaceModel()
            {
                var model = new WorkspaceModel
                {
                    Name = Name,
                    Description = Description,
                    DataFilePath = DataFilePath,
                    DataSheetName = DataSheetName,
                    DataHeaderRow = DataHeaderRow > 0 ? DataHeaderRow : 1,
                    AttachmentsFolder = AttachmentsFolder,
                    AttachmentConfiguration = AttachmentConfiguration?.ToModel()
                        ?? global::MBW.Core.Models.AttachmentConfiguration.CreateDefault()
                };

                // Use reflection to set read-init properties
                var idProperty = typeof(WorkspaceModel).GetProperty(nameof(WorkspaceModel.Id));
                var createdAtProperty = typeof(WorkspaceModel).GetProperty(nameof(WorkspaceModel.CreatedAt));
                idProperty?.SetValue(model, Id);
                createdAtProperty?.SetValue(model, CreatedAt);

                if (Template != null)
                {
                    model.Template = new EmailTemplate
                    {
                        Subject = Template.Subject,
                        PlainTextBody = Template.PlainTextBody
                    };
                }

                if (Configuration != null)
                {
                    model.Configuration = new SendConfiguration
                    {
                        SmtpAccountId = Configuration.SmtpAccountId,
                        DelayMilliseconds = Configuration.DelayMilliseconds,
                        Concurrency = Configuration.Concurrency,
                        FromName = Configuration.FromName,
                        FromEmail = Configuration.FromEmail,
                        TestMode = Configuration.TestMode,
                        EmailColumn = Configuration.EmailColumn,
                        IncludeSharedAttachments = Configuration.IncludeSharedAttachments,
                        IncludeIndividualAttachments = Configuration.IncludeIndividualAttachments,
                        AttachmentRenamePattern = Configuration.AttachmentRenamePattern ?? string.Empty,
                        SendAllRecipients = Configuration.SendAllRecipients,
                        SendRangeFrom = Configuration.SendRangeFrom,
                        SendRangeTo = Configuration.SendRangeTo
                    };
                }

                if (Metadata != null)
                {
                    foreach (var kvp in Metadata)
                    {
                        model.Metadata[kvp.Key] = kvp.Value;
                    }
                }

                model.ModifiedAt = ModifiedAt;

                return model;
            }
        }

        private class EmailTemplateDto
        {
            public string Subject { get; set; } = string.Empty;
            public string? PlainTextBody { get; set; }
        }

        private class SendConfigurationDto
        {
            public Guid? SmtpAccountId { get; set; }
            public int DelayMilliseconds { get; set; }
            public int Concurrency { get; set; }
            public string? FromName { get; set; }
            public string? FromEmail { get; set; }
            public bool TestMode { get; set; }
            public string EmailColumn { get; set; } = string.Empty;
            public bool IncludeSharedAttachments { get; set; } = true;
            public bool IncludeIndividualAttachments { get; set; } = true;
            public string AttachmentRenamePattern { get; set; } = string.Empty;
            public bool SendAllRecipients { get; set; } = true;
            public int SendRangeFrom { get; set; } = 1;
            public int SendRangeTo { get; set; }
        }

        private class AttachmentConfigurationDto
        {
            public bool Enabled { get; set; }

            public AttachmentLinkConfigurationDto? Link { get; set; }

            public static AttachmentConfigurationDto FromModel(AttachmentConfiguration model) => new()
            {
                Enabled = model.Enabled,
                Link = model.Link != null ? AttachmentLinkConfigurationDto.FromModel(model.Link) : null
            };

            public AttachmentConfiguration ToModel() => new()
            {
                Enabled = Enabled,
                Link = Link?.ToModel() ?? AttachmentLinkConfiguration.CreateDefault()
            };
        }

        private class AttachmentLinkConfigurationDto
        {
            public string IndividualFolderName { get; set; } = string.Empty;

            public string KeyColumn { get; set; } = string.Empty;

            public string FilePattern { get; set; } = string.Empty;

            public int? LastMatchedCount { get; set; }

            public int? LastMissingCount { get; set; }

            public DateTimeOffset? LastValidatedAt { get; set; }

            public static AttachmentLinkConfigurationDto FromModel(AttachmentLinkConfiguration model) => new()
            {
                IndividualFolderName = model.IndividualFolderName,
                KeyColumn = model.KeyColumn,
                FilePattern = model.FilePattern,
                LastMatchedCount = model.LastMatchedCount,
                LastMissingCount = model.LastMissingCount,
                LastValidatedAt = model.LastValidatedAt
            };

            public AttachmentLinkConfiguration ToModel() => new()
            {
                IndividualFolderName = IndividualFolderName,
                KeyColumn = KeyColumn,
                FilePattern = FilePattern,
                LastMatchedCount = LastMatchedCount,
                LastMissingCount = LastMissingCount,
                LastValidatedAt = LastValidatedAt
            };
        }
    }
}

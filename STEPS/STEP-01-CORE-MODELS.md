STEP 1 — Core domain models and interfaces

Goal

Implement minimal domain POCOs and service interfaces inside MBW.Core so other projects can depend on stable contracts. Keep MBW.Core free of external packages.

What was added (paths relative to repo root)

- MBW.Core/Models/WorkspaceModel.cs
- MBW.Core/Models/EmailTemplate.cs
- MBW.Core/Models/RecipientRow.cs
- MBW.Core/Models/AttachmentMatch.cs
- MBW.Core/Models/SendConfiguration.cs
- MBW.Core/Models/SendResult.cs

- MBW.Core/Interfaces/IWorkspaceService.cs
- MBW.Core/Interfaces/IExcelImporter.cs
- MBW.Core/Interfaces/IEmailSender.cs
- MBW.Core/Interfaces/IAttachmentService.cs
- MBW.Core/Interfaces/IStorageService.cs

- MBW.Tests/CoreModelsTests.cs

Summary of each file

Models

- WorkspaceModel.cs
  - Represents a workspace metadata container.
  - Properties: Id (Guid), Name, Description, Template (EmailTemplate), DataFilePath, AttachmentsFolder, Configuration (SendConfiguration), Metadata (IDictionary<string,string>), CreatedAt, ModifiedAt.
  - Provides GetVariableNames() helper stub for future variable extraction.

- EmailTemplate.cs
  - Simple template DTO containing Subject, HtmlBody, and optional PlainTextBody.
  - Constructors for empty and parameterized initialization.

- RecipientRow.cs
  - Represents a single recipient row read from Excel.
  - Properties: RowNumber (long), Fields (IReadOnlyDictionary<string,string>).
  - Methods: TryGet(string key, out string? value), Get(string key).

- AttachmentMatch.cs
  - Represents discovered attachment file and matching result against recipient key.
  - Properties: FileName, RecipientKey, Matched (bool).

- SendConfiguration.cs
  - Send-time configuration DTO.
  - Properties: SmtpAccountId (Guid?), DelayMilliseconds, Concurrency, FromName, FromEmail, TestMode.

- SendResult.cs
  - Immutable result record for a single send attempt.
  - Properties: RecipientRowNumber, Success, ErrorMessage, Timestamp, MessageId.

Interfaces (contracts)

- IWorkspaceService.cs
  - Task<WorkspaceModel> CreateAsync(string name, string location)
  - Task<WorkspaceModel> OpenAsync(string path)
  - Task SaveAsync(WorkspaceModel workspace, string path)
  - Purpose: abstract workspace create/open/save logic so MBW.Infrastructure implements storage details.

- IExcelImporter.cs
  - Task<IReadOnlyList<string>> GetHeadersAsync(string filePath, CancellationToken)
  - Task<IReadOnlyList<RecipientRow>> PreviewAsync(string filePath, int maxRows, CancellationToken)
  - IAsyncEnumerable<RecipientRow> ReadAllAsync(string filePath, CancellationToken)
  - Purpose: read Excel headers and rows; streaming API for large files.

- IEmailSender.cs
  - Task TestConnectionAsync(SendConfiguration config, CancellationToken)
  - Task<SendResult> SendAsync(RecipientRow recipient, EmailTemplate template, SendConfiguration config, CancellationToken)
  - Purpose: abstract MailKit or other SMTP implementations.

- IAttachmentService.cs
  - Task<IReadOnlyList<string>> ListAttachmentsAsync(string folderPath, CancellationToken)
  - Task<IReadOnlyList<AttachmentMatch>> MatchAsync(string folderPath, IEnumerable<RecipientRow> recipients, string pattern, CancellationToken)
  - Purpose: list and match attachments to recipients using pattern templates.

- IStorageService.cs
  - Task SaveWorkspacePackageAsync(WorkspaceModel workspace, string destinationPath, CancellationToken)
  - Task<WorkspaceModel> OpenWorkspacePackageAsync(string sourcePath, CancellationToken)
  - Purpose: package/unpackage workspace (.mbw) handling.

Unit tests

- MBW.Tests/CoreModelsTests.cs
  - Verifies basic construction and property expectations for models.
  - Confirms that interfaces exist in MBW.Core (Type.IsInterface assertions).

How to validate locally

1. From repo root (PowerShell):
   - dotnet restore MBW.slnx
   - dotnet build MBW.slnx
   - dotnet test MBW.slnx

2. Open solution in Visual Studio and run tests from Test Explorer.

Design notes and constraints

- MBW.Core has no third-party dependencies; only uses System libraries.
- Interfaces are intentionally minimal; implementations will be provided in MBW.Infrastructure.
- Models are mutable where appropriate to support MVVM editing in MBW.App but keep simple initialization semantics.

Next recommended tasks

- Implement Workspace package storage (STEP 2) in MBW.Infrastructure using IStorageService and IWorkspaceService contracts.
- Implement Excel importer (STEP 3) using ClosedXML to satisfy IExcelImporter.

If you want, I can open a follow-up PR-style change that adds XML comments and example usage snippets for each interface method.

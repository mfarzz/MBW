# STEP 2 — Workspace File Handling

## Goal

Implement workspace create/open/save functionality in MBW.Infrastructure using the contracts defined in MBW.Core (IWorkspaceService, IStorageService). Workspaces are stored as portable folders (*.mbw/) containing workspace.json metadata and email.html template, with subdirectories for data, attachments, and logs.

## What was added (paths relative to repo root)

**Implementation:**
- MBW.Infrastructure/Storage/StorageService.cs
- MBW.Infrastructure/Services/WorkspaceService.cs

**Tests:**
- MBW.Tests/Infrastructure/WorkspaceServiceTests.cs

**Interface Update:**
- MBW.Core/Interfaces/IWorkspaceService.cs (added CancellationToken parameters)

## Detailed breakdown

### StorageService (MBW.Infrastructure/Storage/StorageService.cs)

Handles low-level I/O for workspace persistence.

**Key methods:**

- `SaveWorkspacePackageAsync(WorkspaceModel, string destinationPath, CancellationToken)`
  - Creates or overwrites workspace folder at `destinationPath`
  - Ensures subdirectories exist: `data/`, `attachments/`, `logs/`
  - Serializes `WorkspaceModel` to `workspace.json` using System.Text.Json
	- Uses camelCase property naming
	- Ignores null properties
	- Pretty-prints JSON for readability
  - Saves email template HTML body to `email.html` (separate file for easy editing)
  - Updates `ModifiedAt` timestamp

- `OpenWorkspacePackageAsync(string sourcePath, CancellationToken)`
  - Validates workspace folder exists
  - Reads and deserializes `workspace.json`
  - Loads `email.html` and reconstructs template
  - Throws `DirectoryNotFoundException` if workspace not found
  - Throws `FileNotFoundException` if workspace.json missing

**Internal design:**

- Uses nested `SerializableWorkspaceModel` class during JSON I/O
- Separates HtmlBody (stored in email.html) from metadata (in workspace.json) for cleaner structure
- Handles round-trip serialization via reflection for init-only properties (Id, CreatedAt)

### WorkspaceService (MBW.Infrastructure/Services/WorkspaceService.cs)

Implements `IWorkspaceService` interface with business logic.

**Key methods:**

- `CreateAsync(string name, string location, CancellationToken)`
  - Validates name and location are non-empty
  - Allocates new `WorkspaceModel` with default `EmailTemplate` and `SendConfiguration`
  - Delegates persistence to `StorageService.SaveWorkspacePackageAsync`
  - Returns newly created workspace

- `OpenAsync(string path, CancellationToken)`
  - Delegates to `StorageService.OpenWorkspacePackageAsync`
  - Simple pass-through with null validation

- `SaveAsync(WorkspaceModel workspace, string path, CancellationToken)`
  - Validates inputs
  - Delegates to `StorageService.SaveWorkspacePackageAsync`

### Unit Tests (MBW.Tests/Infrastructure/WorkspaceServiceTests.cs)

6 test methods covering lifecycle and edge cases:

1. **CreateWorkspace_ShouldPersistAndReopen**
   - Verifies create → save → reopen round-trip
   - Asserts folder structure (workspace.json, data/, attachments/, logs/)
   - Confirms metadata equality after reopen (Id, Name, CreatedAt)

2. **SaveWorkspace_ShouldUpdateMetadata**
   - Modifies template (subject, HTML body) and configuration
   - Verifies changes persist after close/reopen

3. **OpenWorkspace_ShouldThrowIfNotFound**
   - Confirms `DirectoryNotFoundException` on missing workspace

4. **CreateWorkspace_ShouldValidateInputs**
   - Data-driven test: empty string and null inputs
   - Confirms `ArgumentException` for invalid inputs

5. **WorkspaceMetadata_ShouldPreserveComplexObjects**
   - Tests preservation of nested configuration and custom metadata
   - Verifies delay, concurrency, email, and test mode flags survive round-trip

All tests use temporary directories with cleanup—no side effects on file system.

## Folder structure created

When a workspace is created or saved, this structure is ensured:

```
WorkspaceName.mbw/
├── workspace.json       (WorkspaceModel metadata in JSON)
│                        ├── id
│                        ├── name
│                        ├── description
│                        ├── template (Subject, PlainTextBody only; HtmlBody separate)
│                        ├── dataFilePath
│                        ├── attachmentsFolder
│                        ├── configuration (SMTP, delay, concurrency, sender info)
│                        ├── metadata (custom key-value pairs)
│                        ├── createdAt, modifiedAt
│
├── email.html           (EmailTemplate HtmlBody as plain HTML)
├── data/                (for imported Excel files)
├── attachments/         (for attachment files)
└── logs/                (for send logs)
```

## Design decisions

1. **Folder vs. Zip**: Chose folder for portability and ease of manual inspection. Can upgrade to zip in future if needed.

2. **Separate email.html**: Stores template HTML body as standalone file for easier external editing (e.g., in WebView2 or IDE).

3. **System.Text.Json**: No external JSON library; uses .NET built-in serialization.

4. **CancellationToken throughout**: Included in all async methods for future cancellation support.

5. **No encryption**: Workspace files are plaintext; credentials stored separately via Windows Credential Manager (deferred to STEP 7).

## Build and test results

```
dotnet build MBW.slnx        ✅ Succeeded
dotnet test MBW.slnx         ✅ All tests passed
```

## Next steps

- **STEP 3**: Implement Excel importer in MBW.Infrastructure using ClosedXML to satisfy `IExcelImporter` contract.
- **STEP 4**: Integrate variables and template replacement in MBW.Core.
- **STEP 5**: Build HTML editor UI in MBW.App using WebView2.

## Notes for maintainers

- If workspace.json is corrupted, `JsonSerializer.Deserialize` will throw; consider adding validation/recovery layer in future.
- ModifiedAt is always updated on save; consider adding change-detection to avoid unnecessary updates.
- Metadata dictionary is case-insensitive for keys; ensure documentation reflects this.

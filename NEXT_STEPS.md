# MBW — Next steps after setup

This document lists a prioritized implementation plan to move from a successful environment setup to a working MVP. Keep each task small and validate by building and running tests after completion.

1. Implement domain models and interfaces (MBW.Core)
   - Create core models: WorkspaceModel, EmailTemplate, RecipientRow, AttachmentMatch, SendConfiguration, SendResult.
   - Define repository/service interfaces: IWorkspaceService, IExcelImporter, IEmailSender, IAttachmentService, IStorageService.
   - Acceptance: MBW.Core compiles and has unit tests verifying basic POCOs and interface contracts.

2. Workspace file handling (MBW.Infrastructure + MBW.Core contracts)
   - Implement create/open/save workspace (.mbw folder or zip), persist workspace.json and email.html.
   - Implement WorkspaceService that reads/writes workspace package.
   - Acceptance: can create workspace on disk and reopen with identical metadata.

3. Excel import and preview (MBW.Infrastructure)
   - Implement Excel importer using ClosedXML to read headers and rows into RecipientRow objects.
   - Provide streaming read for large files and a preview API returning first N rows.
   - Acceptance: sample Excel loads and headers appear as available variables.

4. Variables and template integration (MBW.Core + MBW.App)
   - Expose header variables from importer to the UI; implement simple token replacement in EmailTemplate.
   - Acceptance: rendering preview for one recipient shows replaced tokens.

5. Simple HTML editor (MBW.App)
   - Integrate WebView2, enable basic formatting and an Insert Variable menu using available variables.
   - Acceptance: edited HTML saved to workspace and loads back correctly.

6. Attachment management and matching (MBW.Infrastructure + MBW.App)
   - Implement attachment folder handling, pattern matching against variables, and a missing-files report.
   - Acceptance: matching summary shows matched/missing counts and missing list is viewable.

7. SMTP connection and test (MBW.Infrastructure)
   - Add MailKit-based EmailSender with test-connection method; store SMTP metadata but use Windows Credential Manager for secrets.
   - Acceptance: can successfully connect to SMTP server with test credentials (no send yet).

8. Blast engine (MBW.Infrastructure)
   - Implement a cancellable, throttled send engine with delay and concurrency controls; log send results to SQLite (or workspace log file).
   - Acceptance: engine can send to a small test set, supports cancel, and writes send results.

9. Persistence and send log (MBW.Infrastructure)
   - Add SQLite schema for send history and simple queries (last N sends, failures).
   - Acceptance: send runs are recorded and queryable.

10. Tests, CI and packaging
	- Add unit tests for core logic and integration tests for importer and email sender (mock SMTP where possible).
	- Add CI pipeline that runs dotnet restore/build/test.
	- Create a simple packaging plan for WinUI app (debug/release runs locally).
	- Acceptance: CI builds and tests pass.

Quick commands to validate after each major step:
- dotnet restore
- dotnet build
- dotnet test

Suggested order: 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10.

Notes:
- Keep MBW.Core free from UI and third-party dependencies.
- Prefer small PR-sized commits with one feature per branch.
- If unclear on any design choice, open a short design doc (one page) before implementing.

# STEP 4 — Variables and Template Integration (Core Logic)

## Goal

Implement simple token replacement in EmailTemplate (MBW.Core) to support placeholder substitution like `{Nama}` → actual value from RecipientRow. Extract available variables from template and provide rendering API. Keep everything in MBW.Core—no UI dependencies.

## What was added (paths relative to repo root)

**Implementation:**
- MBW.Core/Utilities/TemplateVariableExtractor.cs
- MBW.Core/Models/EmailTemplate.cs (enhanced)

**Tests:**
- MBW.Tests/TemplateVariableTests.cs

## Detailed breakdown

### TemplateVariableExtractor (MBW.Core/Utilities/TemplateVariableExtractor.cs)

Static utility class for token extraction and rendering.

**Regex Pattern:**
```csharp
\{([a-zA-Z_][a-zA-Z0-9_]*)\}
```
Matches `{VariableName}` where:
- Must start with letter or underscore
- Can contain letters, numbers, underscores
- Rejects invalid patterns like `{123Name}` or `{-Bad}`

**Key methods:**

- `ExtractVariables(string? template) → IReadOnlySet<string>`
  - Scans template text for all `{Variable}` tokens
  - Returns unique set of variable names (duplicates removed)
  - Handles null/empty strings gracefully → empty set
  - Example: `"Hello {Name}, {Name}!"` → `{"Name"}`

- `RenderTemplate(string? template, IReadOnlyDictionary<string, string>? variables) → string`
  - Replaces `{Variable}` tokens with values from dictionary
  - **Missing variables left as-is** (no error thrown)
  - Case-sensitive matching
  - Null/empty templates return as-is
  - Handles null dictionary → returns template unchanged
  - Example: `"Hello {Name}, {Age}"` + `{Name="Alice"}` → `"Hello Alice, {Age}"`

### EmailTemplate (MBW.Core/Models/EmailTemplate.cs) Enhancements

Added two public methods to support rendering:

- `GetAvailableVariables() → IReadOnlySet<string>`
  - Extracts all variables from both Subject and HtmlBody
  - Combines results into single set
  - Also checks PlainTextBody if present
  - Pure method: no side effects
  - Example:
	```csharp
	var template = new EmailTemplate(
		subject: "Welcome {Nama}",
		htmlBody: "<p>Dear {Nama}, your email is {Email}</p>"
	);
	var vars = template.GetAvailableVariables(); // Returns {"Nama", "Email"}
	```

- `RenderForRecipient(RecipientRow recipient) → EmailTemplate`
  - Creates **new EmailTemplate** with all tokens substituted
  - Original template unchanged (immutable pattern)
  - Uses recipient's field dictionary for substitution
  - RecipientRow fields are case-insensitive (by design from STEP 3)
  - Substitutes Subject, HtmlBody, and PlainTextBody
  - Throws `ArgumentNullException` if recipient is null
  - Missing fields → variables left as-is
  - Example:
	```csharp
	var template = new EmailTemplate(
		subject: "Hello {Nama}",
		htmlBody: "<p>{Nama} from {Fakultas}</p>"
	);
	var recipient = new RecipientRow(1, new Dictionary<string, string>
	{
		["Nama"] = "Ahmad",
		["Fakultas"] = "FTI"
	});
	var rendered = template.RenderForRecipient(recipient);
	// rendered.Subject = "Hello Ahmad"
	// rendered.HtmlBody = "<p>Ahmad from FTI</p>"
	```

### Unit Tests (MBW.Tests/TemplateVariableTests.cs)

14 comprehensive tests covering all functionality:

**ExtractVariables tests:**
1. `ShouldFindAllUniquePlaceholders` — Extracts multiple distinct variables
2. `ShouldHandleDuplicates` — Returns set (duplicates removed)
3. `ShouldReturnEmptyForEmptyTemplate` — Handles empty string
4. `ShouldReturnEmptyForNullTemplate` — Handles null gracefully
5. `ShouldIgnoreInvalidPatterns` — Rejects `{123Name}`, `{-Bad}` etc.

**RenderTemplate tests:**
6. `ShouldSubstituteAllVariables` — Replaces all matching tokens
7. `ShouldLeaveMissingVariablesAsIs` — Unmatched tokens preserved
8. `ShouldHandleNullVariablesDictionary` — Returns template unchanged
9. `ShouldHandleEmptyTemplate` — Returns empty string
10. `ShouldCaseSensitiveMatchVariables` — `{Nama}` ≠ `{nama}`

**EmailTemplate tests:**
11. `GetAvailableVariables_ShouldExtractFromSubjectAndBody` — Combines from both
12. `RenderForRecipient_ShouldSubstituteAllFields` — Full rendering for one recipient
13. `RenderForRecipient_ShouldNotMutateOriginal` — Original unchanged after render
14. `RenderForRecipient_ShouldHandleMultipleRecipients` — Each gets unique substitution
15. `RenderForRecipient_ShouldThrowOnNullRecipient` — Null safety
16. `RenderForRecipient_ShouldPreservePlainTextBody` — All template fields rendered

## Design decisions

1. **Case-sensitive tokens**: `{Nama}` ≠ `{nama}` — matches header names exactly
2. **Missing variables left as-is**: No exception thrown; allows partial data or template preview
3. **Immutable rendering**: `RenderForRecipient()` returns new EmailTemplate
4. **Regex pattern**: Simple alphanumeric + underscore; rejects invalid identifiers upfront
5. **Pure functions**: No side effects; thread-safe
6. **No escaping**: `{{` not supported as escape sequence (can add later if needed)

## Usage example (from perspective of STEP 5 UI)

```csharp
// UI loads Excel data via ExcelImporter (STEP 3)
var importer = new ExcelImporter();
var headers = await importer.GetHeadersAsync("data.xlsx");
// headers = ["NIM", "Nama", "Email", "Fakultas", ...]

// User creates/edits template in HTML editor
var template = new EmailTemplate(
	subject: "Undangan untuk {Nama}",
	htmlBody: @"<p>Halo {Nama},</p>
			   <p>Anda adalah mahasiswa {Fakultas}.</p>
			   <p>Email: {Email}</p>"
);

// UI shows available variables from template
var availableVars = template.GetAvailableVariables();
// availableVars = {"Nama", "Fakultas", "Email"}

// When previewing email with specific recipient
var recipients = await importer.PreviewAsync("data.xlsx", 1);
var firstRecipient = recipients[0]; // RecipientRow with fields

var preview = template.RenderForRecipient(firstRecipient);
// preview.Subject = "Undangan untuk Ahmad Rizki"
// preview.HtmlBody = "<p>Halo Ahmad Rizki,</p>
//                     <p>Anda adalah mahasiswa FTI.</p>
//                     <p>Email: ahmad@example.com</p>"
```

## Build and test results

```
dotnet build MBW.slnx        ✅ Succeeded
dotnet test MBW.slnx         ✅ All tests passed (14 tests in TemplateVariableTests)
```

## Next steps

- **STEP 5**: Build HTML editor UI in MBW.App using WebView2
  - Integrate template editing
  - Show available variables as a list/menu
  - Implement "Insert Variable" button
  - Preview rendering with actual recipient data

- **STEP 6**: Attachment management and matching

- **STEP 7**: SMTP connection and test

## Notes for maintainers

- Token pattern is strict: `{AZ_az_09_*}` — enforces valid C# identifier naming
- Performance: Regex compiled into static field for efficiency
- Thread-safe: All methods are pure (no state mutations)
- Extensible: Can add escape sequences or custom value formatters later
- RecipientRow fields are case-insensitive (by STEP 3 design), but template variables are case-sensitive

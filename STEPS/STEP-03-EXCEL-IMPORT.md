# STEP 3 — Excel Import and Preview

## Goal

Implement Excel file reading in MBW.Infrastructure using ClosedXML to satisfy IExcelImporter contract from MBW.Core. Provide both preview (first N rows) and streaming (all rows) capabilities for large files. Extract headers as column names that become available variables for email templates.

## What was added (paths relative to repo root)

**Implementation:**
- MBW.Infrastructure/Excel/ExcelImporter.cs

**Test Fixtures & Tests:**
- MBW.Tests/Fixtures/ExcelFixtures.cs
- MBW.Tests/Infrastructure/ExcelImporterTests.cs

## Detailed breakdown

### ExcelImporter (MBW.Infrastructure/Excel/ExcelImporter.cs)

Implements `IExcelImporter` interface using ClosedXML for Excel file handling.

**Key methods:**

- `GetHeadersAsync(string filePath, CancellationToken)`
  - Opens Excel workbook and reads first row
  - Extracts column names (headers) until first empty cell
  - Returns `IReadOnlyList<string>` of header names
  - Example: ["NIM", "Nama", "Email", "Fakultas", "Program_Studi"]
  - Throws `FileNotFoundException` if file missing
  - Throws `ArgumentException` if path is empty

- `PreviewAsync(string filePath, int maxRows = 10, CancellationToken)`
  - Reads first maxRows data rows (excluding header row)
  - Maps each row to `RecipientRow` with headers as keys
  - Returns `IReadOnlyList<RecipientRow>` for bounded memory usage
  - Default maxRows = 10
  - Throws `ArgumentException` if maxRows <= 0
  - Uses `RowsUsed()` to efficiently iterate only used rows

- `ReadAllAsync(string filePath, [EnumeratorCancellation] CancellationToken)`
  - Async enumerable for streaming all data rows
  - Memory-efficient for large files (deferred enumeration)
  - Decorated with `[EnumeratorCancellation]` to support external cancellation tokens
  - Yields `RecipientRow` one at a time

**Internal design:**

- `ExtractHeaders(IXLWorksheet)`: Helper to extract headers from row 1
- `CreateRecipientRow(long rowNumber, IXLRow, List<string>)`: Maps Excel row to RecipientRow with case-insensitive field dictionary
- Uses `cell.GetValue<string>()` for proper ClosedXML type handling
- Excel row numbers preserved in RecipientRow.RowNumber for logging/debugging

### ExcelFixtures (MBW.Tests/Fixtures/ExcelFixtures.cs)

Test helper that generates sample Excel file on-demand.

**Key features:**

- `EnsureFixtures()`: Static method to initialize test data
- `SampleRecipientsPath`: Property pointing to temp location of sample Excel
- Auto-generates `sample-recipients.xlsx` with 5 recipient rows if missing
- Sample data columns: NIM, Nama, Email, Fakultas, Program_Studi
- Uses ClosedXML to create well-formed Excel files

**Sample data:**
```
NIM     Nama           Email                  Fakultas    Program_Studi
001     Ahmad Rizki    ahmad@example.com      FTI         Informatika
002     Budi Santoso   budi@example.com       FTI         Rekayasa Perangkat Lunak
003     Citra Dewi     citra@example.com      FTI         Sistem Informasi
004     Dian Kusuma    dian@example.com       FEB         Akuntansi
005     Eka Putri      eka@example.com        FEB         Manajemen
```

### Unit Tests (MBW.Tests/Infrastructure/ExcelImporterTests.cs)

10 comprehensive tests covering all functionality:

1. **GetHeadersAsync_ShouldExtractHeadersFromFirstRow**
   - Verifies 5 headers extracted correctly
   - Asserts order: NIM, Nama, Email, Fakultas, Program_Studi

2. **PreviewAsync_ShouldReturnFirstNRows**
   - Requests 3 rows from 5-row Excel
   - Verifies row count = 3
   - Checks RowNumber starts at 2 (first data row after header)
   - Validates field mapping for first row

3. **PreviewAsync_ShouldPreserveAllFields**
   - Loads one row and confirms all 5 fields accessible
   - Tests both `TryGet()` and `Get()` accessor methods

4. **ReadAllAsync_ShouldStreamAllRows**
   - Iterates all rows using async enumerable
   - Verifies count = 5
   - Validates first row (RowNumber = 2) and last row (RowNumber = 6)
   - Checks data integrity

5. **GetHeadersAsync_ShouldThrowOnMissingFile**
   - Confirms `FileNotFoundException` for nonexistent path

6. **PreviewAsync_ShouldThrowOnEmptyPath**
   - Confirms `ArgumentException` for empty file path

7. **PreviewAsync_ShouldThrowOnInvalidMaxRows**
   - Confirms `ArgumentException` for maxRows <= 0

8. **RecipientRow_FieldsAreCaseInsensitive**
   - Verifies field lookups work with any casing
   - Tests: "nama", "NAMA", "Nama" all resolve to "Ahmad Rizki"

9. **PreviewAsync_DefaultMaxRowsIs10**
   - Calls `PreviewAsync()` without maxRows parameter
   - Expects default behavior returns all 5 available rows

All tests use `ExcelFixtures.EnsureFixtures()` in `[ClassInitialize]` to prepare sample data.

## Design decisions

1. **ClosedXML API**: Used `GetValue<string>()` for type-safe cell reading instead of `ToString()` conversions
2. **RowNumber() method call**: ClosedXML's `IXLRow.RowNumber()` is a method, not a property
3. **RowsUsed() for iteration**: Efficiently iterates only populated rows, avoiding null reference issues
4. **Case-insensitive fields**: `RecipientRow` uses `StringComparer.OrdinalIgnoreCase` for column name lookups
5. **Streaming support**: `ReadAllAsync` returns `IAsyncEnumerable<T>` for memory efficiency on large files
6. **Separate sample file**: `ExcelFixtures` generates test data programmatically to avoid binary file storage

## Build and test results

```
dotnet build MBW.slnx        ✅ Succeeded
dotnet test MBW.slnx         ✅ All tests passed (10 tests in ExcelImporterTests)
```

## Next steps

- **STEP 4**: Implement variable extraction and template token replacement in MBW.Core (EmailTemplate with simple placeholder logic)
- **STEP 5**: Build HTML editor UI in MBW.App using WebView2 with Insert Variable support
- **STEP 6**: Implement attachment file handling and pattern matching

## Notes for maintainers

- Excel files must have headers in row 1; no built-in validation for missing headers yet
- Empty cells are treated as empty strings, not null
- Very large Excel files (100k+ rows) will load entirely into memory before streaming; consider chunked reading if needed
- ClosedXML loads entire workbook into memory; for truly large files, consider third-party streaming APIs
- Column count determined by first non-empty header; trailing empty columns ignored

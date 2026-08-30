using CommunityToolkit.Mvvm.ComponentModel;
using MBW.Core.Interfaces;
using MBW.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace MBW.App.ViewModels
{
    public sealed class ExcelImportSelection
    {
        public ExcelImportSelection(string sourcePath, string sheetName, int headerRow)
        {
            SourcePath = sourcePath;
            SheetName = sheetName;
            HeaderRow = headerRow;
        }

        public string SourcePath { get; }

        public string SheetName { get; }

        public int HeaderRow { get; }
    }

    public partial class ExcelImportDialogViewModel : ObservableObject
    {
        private readonly IExcelImporter _excelImporter;
        private readonly string _sourcePath;

        public ExcelImportDialogViewModel(IExcelImporter excelImporter, string sourcePath)
        {
            _excelImporter = excelImporter;
            _sourcePath = sourcePath;
            FileName = System.IO.Path.GetFileName(sourcePath);
        }

        public ObservableCollection<string> SheetNames { get; } = new();

        public ObservableCollection<string> PreviewHeaders { get; } = new();

        public ObservableCollection<DatabasePreviewRow> PreviewRows { get; } = new();

        [ObservableProperty]
        public partial string FileName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string? SelectedSheetName { get; set; }

        [ObservableProperty]
        public partial int SelectedHeaderRow { get; set; } = 1;

        [ObservableProperty]
        public partial string StatusMessage { get; set; } = "Memuat sheet...";

        [ObservableProperty]
        public partial string SummaryText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsBusy { get; set; }

        [ObservableProperty]
        public partial bool CanConfirm { get; set; }

        public event EventHandler? PreviewChanged;

        partial void OnSelectedSheetNameChanged(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value) && !IsBusy)
            {
                _ = RefreshPreviewAsync();
            }
        }

        partial void OnSelectedHeaderRowChanged(int value)
        {
            if (value > 0 && !IsBusy)
            {
                _ = RefreshPreviewAsync();
            }
        }

        public async Task InitializeAsync()
        {
            try
            {
                IsBusy = true;
                StatusMessage = "Memuat daftar sheet...";
                CanConfirm = false;

                SheetNames.Clear();
                var sheets = await _excelImporter.GetSheetNamesAsync(_sourcePath);
                foreach (var sheet in sheets)
                {
                    SheetNames.Add(sheet);
                }

                if (SheetNames.Count == 0)
                {
                    StatusMessage = "File Excel tidak memiliki sheet.";
                    return;
                }

                SelectedHeaderRow = 1;
                SelectedSheetName = SheetNames[0];
                await RefreshPreviewAsync(force: true);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Gagal membaca Excel: {ex.Message}";
                CanConfirm = false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public ExcelImportSelection? CreateSelection()
        {
            if (!CanConfirm || string.IsNullOrWhiteSpace(SelectedSheetName))
            {
                return null;
            }

            return new ExcelImportSelection(_sourcePath, SelectedSheetName, SelectedHeaderRow);
        }

        private async Task RefreshPreviewAsync(bool force = false)
        {
            if (string.IsNullOrWhiteSpace(SelectedSheetName))
            {
                return;
            }

            if (IsBusy && !force)
            {
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = $"Memuat preview \"{SelectedSheetName}\"...";

                var preview = await _excelImporter.PreviewSheetAsync(
                    _sourcePath,
                    SelectedSheetName,
                    maxRows: 10,
                    headerRow: SelectedHeaderRow);

                PreviewHeaders.Clear();
                PreviewRows.Clear();

                foreach (var header in preview.Headers)
                {
                    PreviewHeaders.Add(header);
                }

                foreach (var row in preview.Rows)
                {
                    var cells = new System.Collections.Generic.List<string>(preview.Headers.Count);
                    foreach (var header in preview.Headers)
                    {
                        cells.Add(row.Get(header) ?? string.Empty);
                    }

                    PreviewRows.Add(new DatabasePreviewRow(cells));
                }

                SummaryText = preview.Headers.Count == 0
                    ? "Tidak ada header pada baris yang dipilih."
                    : $"{preview.TotalRows:N0} baris data · {preview.Headers.Count} kolom";

                CanConfirm = preview.Headers.Count > 0;
                StatusMessage = CanConfirm
                    ? $"Sheet \"{preview.SheetName}\" siap digunakan."
                    : "Pilih baris header yang berisi nama kolom.";
                PreviewChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                PreviewHeaders.Clear();
                PreviewRows.Clear();
                SummaryText = string.Empty;
                CanConfirm = false;
                StatusMessage = $"Preview gagal: {ex.Message}";
                PreviewChanged?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}

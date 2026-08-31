using MBW.Core.Models;
using System;
using System.IO;

namespace MBW.App.ViewModels
{
    public sealed class AttachmentItemViewModel
    {
        private AttachmentItemViewModel(
            string name,
            string fullPath,
            bool isFolder,
            AttachmentItemType itemType,
            long? sizeBytes,
            DateTimeOffset? modifiedAt)
        {
            Name = name;
            DisplayName = isFolder || itemType != AttachmentItemType.File
                ? name
                : Path.GetFileNameWithoutExtension(name);
            FullPath = fullPath;
            IsFolder = isFolder;
            ItemType = itemType;
            TypeLabel = itemType switch
            {
                AttachmentItemType.SharedFolder => "Shared",
                AttachmentItemType.IndividualFolder => "Individual",
                AttachmentItemType.File => GetFileTypeLabel(name),
                _ => "—"
            };
            KindLabel = isFolder ? "Folder" : "File";
            SizeDisplay = isFolder ? "—" : FormatSize(sizeBytes ?? 0);
            ModifiedDisplay = modifiedAt?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "—";
            IconGlyph = itemType switch
            {
                AttachmentItemType.SharedFolder => "\uE753",
                AttachmentItemType.IndividualFolder => "\uE8B7",
                _ => "\uE8A5"
            };
            IsSharedRoot = itemType == AttachmentItemType.SharedFolder;
            IsDeletable = itemType != AttachmentItemType.SharedFolder;
            SizeBytes = sizeBytes;
            ModifiedAt = modifiedAt;
        }

        public string Name { get; }

        public string DisplayName { get; }

        public string FullPath { get; }

        public bool IsFolder { get; }

        public AttachmentItemType ItemType { get; }

        public string TypeLabel { get; }

        public string KindLabel { get; }

        public string SizeDisplay { get; }

        public string ModifiedDisplay { get; }

        public string IconGlyph { get; }

        public bool IsSharedRoot { get; }

        public bool IsDeletable { get; }

        public long? SizeBytes { get; }

        public DateTimeOffset? ModifiedAt { get; }

        public static AttachmentItemViewModel FromEntry(AttachmentDirectoryEntry entry, AttachmentItemType itemType) =>
            new(
                entry.Name,
                entry.FullPath,
                entry.IsDirectory,
                entry.IsDirectory ? itemType : AttachmentItemType.File,
                entry.SizeBytes,
                entry.ModifiedAt);

        public static AttachmentItemViewModel CreateSharedFolder(string fullPath) =>
            new("shared", fullPath, isFolder: true, AttachmentItemType.SharedFolder, null, null);

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024)
            {
                return $"{bytes} B";
            }

            if (bytes < 1024 * 1024)
            {
                return $"{bytes / 1024.0:0.#} KB";
            }

            return $"{bytes / (1024.0 * 1024.0):0.#} MB";
        }

        private static string GetFileTypeLabel(string fileName)
        {
            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                return "File";
            }

            return extension.TrimStart('.').ToUpperInvariant() switch
            {
                "JPG" or "JPEG" => "JPEG",
                "DOC" => "Word",
                "DOCX" => "Word",
                "XLS" => "Excel",
                "XLSX" or "XLSM" => "Excel",
                "PPT" => "PowerPoint",
                "PPTX" => "PowerPoint",
                "PDF" => "PDF",
                "PNG" => "PNG",
                "GIF" => "GIF",
                "TXT" => "Text",
                "ZIP" => "ZIP",
                "RAR" => "RAR",
                "7Z" => "7-Zip",
                _ => extension.TrimStart('.').ToUpperInvariant()
            };
        }
    }
}

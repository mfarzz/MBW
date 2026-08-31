using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MBW.Core.Interfaces;
using MBW.Core.Models;
using MBW.Core.Utilities;

namespace MBW.Infrastructure.Attachments
{
    public sealed class AttachmentService : IAttachmentService
    {
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".xlsm", ".ppt", ".pptx",
            ".png", ".jpg", ".jpeg", ".gif", ".txt", ".zip", ".rar", ".7z"
        };

        public Task<IReadOnlyList<string>> ListAttachmentsAsync(string folderPath, CancellationToken cancellationToken = default)
        {
            EnsureFolderExists(folderPath);

            return Task.Run(() =>
            {
                if (!Directory.Exists(folderPath))
                {
                    return (IReadOnlyList<string>)Array.Empty<string>();
                }

                var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(path => AllowedExtensions.Contains(Path.GetExtension(path)))
                    .Select(Path.GetFileName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Cast<string>()
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return (IReadOnlyList<string>)files.AsReadOnly();
            }, cancellationToken);
        }

        public Task<int> CountAttachmentsAsync(string folderPath, CancellationToken cancellationToken = default)
        {
            return ListAttachmentsAsync(folderPath, cancellationToken)
                .ContinueWith(task => task.Result.Count, cancellationToken);
        }

        public Task<IReadOnlyList<AttachmentMatch>> MatchAsync(
            string folderPath,
            IEnumerable<RecipientRow> recipients,
            string pattern,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                throw new ArgumentException("Folder path cannot be empty", nameof(folderPath));
            }

            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new ArgumentException("Pattern cannot be empty", nameof(pattern));
            }

            return Task.Run(() =>
            {
                var matches = new List<AttachmentMatch>();
                foreach (var recipient in recipients)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var fileName = TemplateVariableExtractor.RenderTemplate(pattern, recipient.Fields);
                    var fullPath = Path.Combine(folderPath, fileName);
                    var matched = File.Exists(fullPath);
                    matches.Add(new AttachmentMatch(fileName, matched, recipient.RowNumber.ToString()));
                }

                return (IReadOnlyList<AttachmentMatch>)matches.AsReadOnly();
            }, cancellationToken);
        }

        public Task<string> CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("Source path cannot be empty", nameof(sourcePath));
            }

            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                throw new ArgumentException("Destination path cannot be empty", nameof(destinationPath));
            }

            return Task.Run(() =>
            {
                var directory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Copy(sourcePath, destinationPath, overwrite: true);
                return destinationPath;
            }, cancellationToken);
        }

        public Task<int> ImportFolderAsync(
            string sourceFolder,
            string destinationFolder,
            CancellationToken cancellationToken = default)
        {
            EnsureFolderExists(sourceFolder);
            Directory.CreateDirectory(destinationFolder);

            return Task.Run(() =>
            {
                var count = 0;
                foreach (var sourcePath in Directory.EnumerateFiles(sourceFolder, "*.*", SearchOption.TopDirectoryOnly))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!AllowedExtensions.Contains(Path.GetExtension(sourcePath)))
                    {
                        continue;
                    }

                    var destinationPath = Path.Combine(destinationFolder, Path.GetFileName(sourcePath));
                    File.Copy(sourcePath, destinationPath, overwrite: true);
                    count++;
                }

                return count;
            }, cancellationToken);
        }

        public Task<IReadOnlyList<AttachmentDirectoryEntry>> ListDirectoryEntriesAsync(
            string folderPath,
            bool directoriesOnly = false,
            CancellationToken cancellationToken = default)
        {
            EnsureFolderExists(folderPath);

            return Task.Run(() =>
            {
                if (!Directory.Exists(folderPath))
                {
                    return (IReadOnlyList<AttachmentDirectoryEntry>)Array.Empty<AttachmentDirectoryEntry>();
                }

                var entries = new List<AttachmentDirectoryEntry>();

                foreach (var directory in Directory.EnumerateDirectories(folderPath))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var info = new DirectoryInfo(directory);
                    entries.Add(new AttachmentDirectoryEntry
                    {
                        Name = info.Name,
                        FullPath = info.FullName,
                        IsDirectory = true,
                        ModifiedAt = info.LastWriteTimeUtc
                    });
                }

                if (!directoriesOnly)
                {
                    foreach (var file in Directory.EnumerateFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!AllowedExtensions.Contains(Path.GetExtension(file)))
                        {
                            continue;
                        }

                        var info = new FileInfo(file);
                        entries.Add(new AttachmentDirectoryEntry
                        {
                            Name = info.Name,
                            FullPath = info.FullName,
                            IsDirectory = false,
                            SizeBytes = info.Length,
                            ModifiedAt = info.LastWriteTimeUtc
                        });
                    }
                }

                entries.Sort((left, right) =>
                {
                    if (left.IsDirectory != right.IsDirectory)
                    {
                        return left.IsDirectory ? -1 : 1;
                    }

                    return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
                });

                return (IReadOnlyList<AttachmentDirectoryEntry>)entries.AsReadOnly();
            }, cancellationToken);
        }

        public Task CreateFolderAsync(string folderPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                throw new ArgumentException("Folder path cannot be empty", nameof(folderPath));
            }

            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.CreateDirectory(folderPath);
            }, cancellationToken);
        }

        public Task DeletePathAsync(string path, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path cannot be empty", nameof(path));
            }

            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                    return;
                }

                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }, cancellationToken);
        }

        public string ResolvePattern(string pattern, IReadOnlyDictionary<string, string> fields) =>
            TemplateVariableExtractor.RenderTemplate(pattern, fields);

        private static void EnsureFolderExists(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                throw new ArgumentException("Folder path cannot be empty", nameof(folderPath));
            }
        }
    }
}

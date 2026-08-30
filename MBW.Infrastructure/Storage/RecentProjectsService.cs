using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MBW.Core.Interfaces;
using MBW.Core.Models;

namespace MBW.Infrastructure.Storage
{
    public sealed class RecentProjectsService : IRecentProjectsService
    {
        private const int MaxEntries = 10;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        private readonly string _storeFilePath;

        public RecentProjectsService()
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MBW");
            Directory.CreateDirectory(folder);
            _storeFilePath = Path.Combine(folder, "recent-projects.json");
        }

        public async Task<IReadOnlyList<RecentProjectEntry>> LoadAsync(CancellationToken cancellationToken = default)
        {
            if (!File.Exists(_storeFilePath))
            {
                return Array.Empty<RecentProjectEntry>();
            }

            await using var stream = File.OpenRead(_storeFilePath);
            var entries = await JsonSerializer.DeserializeAsync<List<RecentProjectEntry>>(stream, JsonOptions, cancellationToken);
            if (entries is null || entries.Count == 0)
            {
                return Array.Empty<RecentProjectEntry>();
            }

            return entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Path))
                .OrderByDescending(entry => entry.LastOpenedAt)
                .Take(MaxEntries)
                .ToList();
        }

        public async Task AddOrUpdateAsync(string name, string path, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var entries = (await LoadAsync(cancellationToken)).ToList();
            entries.RemoveAll(entry => string.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase));
            entries.Insert(0, new RecentProjectEntry
            {
                Name = string.IsNullOrWhiteSpace(name) ? Path.GetFileName(path) : name.Trim(),
                Path = path,
                LastOpenedAt = DateTime.Now
            });

            if (entries.Count > MaxEntries)
            {
                entries = entries.Take(MaxEntries).ToList();
            }

            await SaveAsync(entries, cancellationToken);
        }

        public async Task RemoveAsync(string path, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var entries = (await LoadAsync(cancellationToken)).ToList();
            var removed = entries.RemoveAll(entry => string.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase));
            if (removed == 0)
            {
                return;
            }

            await SaveAsync(entries, cancellationToken);
        }

        private async Task SaveAsync(List<RecentProjectEntry> entries, CancellationToken cancellationToken)
        {
            await using var stream = File.Create(_storeFilePath);
            await JsonSerializer.SerializeAsync(stream, entries, JsonOptions, cancellationToken);
        }
    }
}

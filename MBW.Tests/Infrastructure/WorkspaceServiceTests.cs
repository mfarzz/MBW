using Microsoft.VisualStudio.TestTools.UnitTesting;
using MBW.Core.Interfaces;
using MBW.Core.Models;
using MBW.Infrastructure.Services;
using MBW.Infrastructure.Storage;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MBW.Tests.Infrastructure
{
    [TestClass]
    public sealed class WorkspaceServiceTests
    {
        private string _tempDir = null!;

        [TestInitialize]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"mbw_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir))
            {
                try { Directory.Delete(_tempDir, true); }
                catch { /* ignore */ }
            }
        }

        [TestMethod]
        public async Task CreateWorkspace_ShouldPersistAndReopen()
        {
            // Arrange
            IStorageService storage = new StorageService();
            IWorkspaceService service = new WorkspaceService(storage);
            var workspaceName = "TestWorkspace";
            var workspacePath = Path.Combine(_tempDir, $"{workspaceName}.mbw");

            // Act - create
            var created = await service.CreateAsync(workspaceName, workspacePath);
            Assert.IsNotNull(created);
            Assert.AreEqual(workspaceName, created.Name);
            Assert.IsNotNull(created.Template);

            // Assert.folder structure
            Assert.IsTrue(Directory.Exists(workspacePath));
            Assert.IsTrue(File.Exists(Path.Combine(workspacePath, "workspace.json")));
            Assert.IsTrue(Directory.Exists(Path.Combine(workspacePath, "data")));
            Assert.IsTrue(Directory.Exists(Path.Combine(workspacePath, "attachments")));
            Assert.IsTrue(Directory.Exists(Path.Combine(workspacePath, "logs")));

            // Act - reopen
            var reopened = await service.OpenAsync(workspacePath);

            // Assert
            Assert.IsNotNull(reopened);
            Assert.AreEqual(created.Id, reopened.Id);
            Assert.AreEqual(created.Name, reopened.Name);
            Assert.AreEqual(created.CreatedAt, reopened.CreatedAt);
        }

        [TestMethod]
        public async Task SaveWorkspace_ShouldUpdateMetadata()
        {
            // Arrange
            IStorageService storage = new StorageService();
            IWorkspaceService service = new WorkspaceService(storage);
            var workspaceName = "UpdateTest";
            var workspacePath = Path.Combine(_tempDir, $"{workspaceName}.mbw");
            var created = await service.CreateAsync(workspaceName, workspacePath);

            // Act - modify and save
            created.Name = "UpdatedName";
            created.Description = "Updated description";
            if (created.Template != null)
            {
                created.Template.Subject = "Test Subject";
                created.Template.HtmlBody = "<p>Test HTML</p>";
            }
            await service.SaveAsync(created, workspacePath);

            // Act - reopen
            var reopened = await service.OpenAsync(workspacePath);

            // Assert
            Assert.AreEqual("UpdatedName", reopened.Name);
            Assert.AreEqual("Updated description", reopened.Description);
            Assert.IsNotNull(reopened.Template);
            Assert.AreEqual("Test Subject", reopened.Template.Subject);
            Assert.AreEqual("<p>Test HTML</p>", reopened.Template.HtmlBody);
        }

        [TestMethod]
        public async Task OpenWorkspace_ShouldThrowIfNotFound()
        {
            // Arrange
            IStorageService storage = new StorageService();
            IWorkspaceService service = new WorkspaceService(storage);
            var nonexistentPath = Path.Combine(_tempDir, "nonexistent.mbw");

            // Act & Assert
            await Assert.ThrowsExceptionAsync<DirectoryNotFoundException>(
                () => service.OpenAsync(nonexistentPath));
        }

        [TestMethod]
        [DataRow("")]
        [DataRow(null)]
        public async Task CreateWorkspace_ShouldValidateInputs(string invalidName)
        {
            // Arrange
            IStorageService storage = new StorageService();
            IWorkspaceService service = new WorkspaceService(storage);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => service.CreateAsync(invalidName, Path.Combine(_tempDir, "test")));
        }

        [TestMethod]
        public async Task WorkspaceMetadata_ShouldPreserveComplexObjects()
        {
            // Arrange
            IStorageService storage = new StorageService();
            IWorkspaceService service = new WorkspaceService(storage);
            var workspacePath = Path.Combine(_tempDir, "ComplexTest.mbw");
            var created = await service.CreateAsync("ComplexTest", workspacePath);

            // Add metadata
            created.Metadata["CustomKey"] = "CustomValue";

            // Add config
            if (created.Configuration != null)
            {
                created.Configuration.DelayMilliseconds = 5000;
                created.Configuration.Concurrency = 3;
                created.Configuration.FromEmail = "test@example.com";
                created.Configuration.TestMode = false;
            }

            // Act - save and reopen
            await service.SaveAsync(created, workspacePath);
            var reopened = await service.OpenAsync(workspacePath);

            // Assert
            Assert.IsTrue(reopened.Metadata.TryGetValue("CustomKey", out var customValue));
            Assert.AreEqual("CustomValue", customValue);
            Assert.IsNotNull(reopened.Configuration);
            Assert.AreEqual(5000, reopened.Configuration.DelayMilliseconds);
            Assert.AreEqual(3, reopened.Configuration.Concurrency);
            Assert.AreEqual("test@example.com", reopened.Configuration.FromEmail);
            Assert.IsFalse(reopened.Configuration.TestMode);
        }
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using MBW.Core.Interfaces;
using MBW.Infrastructure.Excel;
using MBW.Tests.Fixtures;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MBW.Tests.Infrastructure
{
    [TestClass]
    public sealed class ExcelImporterTests
    {
        [ClassInitialize]
        public static void ClassSetup(TestContext? context)
        {
            // Ensure test Excel files exist
            ExcelFixtures.EnsureFixtures();
        }

        [TestMethod]
        public async Task GetHeadersAsync_ShouldExtractHeadersFromFirstRow()
        {
            // Arrange
            IExcelImporter importer = new ExcelImporter();

            // Act
            var headers = await importer.GetHeadersAsync(ExcelFixtures.SampleRecipientsPath);

            // Assert
            Assert.IsNotNull(headers);
            Assert.AreEqual(5, headers.Count);
            Assert.AreEqual("NIM", headers[0]);
            Assert.AreEqual("Nama", headers[1]);
            Assert.AreEqual("Email", headers[2]);
            Assert.AreEqual("Fakultas", headers[3]);
            Assert.AreEqual("Program_Studi", headers[4]);
        }

        [TestMethod]
        public async Task PreviewAsync_ShouldReturnFirstNRows()
        {
            // Arrange
            IExcelImporter importer = new ExcelImporter();
            var maxRows = 3;

            // Act
            var preview = await importer.PreviewAsync(ExcelFixtures.SampleRecipientsPath, maxRows);

            // Assert
            Assert.IsNotNull(preview);
            Assert.AreEqual(maxRows, preview.Count);

            // Check first row
            var firstRow = preview[0];
            Assert.AreEqual(2, firstRow.RowNumber); // Row 2 in Excel (after header)
            Assert.IsTrue(firstRow.TryGet("NIM", out var nim));
            Assert.AreEqual("001", nim);
            Assert.IsTrue(firstRow.TryGet("Nama", out var nama));
            Assert.AreEqual("Ahmad Rizki", nama);
        }

        [TestMethod]
        public async Task PreviewAsync_ShouldPreserveAllFields()
        {
            // Arrange
            IExcelImporter importer = new ExcelImporter();

            // Act
            var preview = await importer.PreviewAsync(ExcelFixtures.SampleRecipientsPath, 1);

            // Assert
            Assert.AreEqual(1, preview.Count);
            var row = preview[0];

            // Verify all fields are accessible
            Assert.IsTrue(row.TryGet("NIM", out _));
            Assert.IsTrue(row.TryGet("Nama", out _));
            Assert.IsTrue(row.TryGet("Email", out _));
            Assert.IsTrue(row.TryGet("Fakultas", out _));
            Assert.IsTrue(row.TryGet("Program_Studi", out _));

            // Verify Get accessor as well
            Assert.IsNotNull(row.Get("Nama"));
        }

        [TestMethod]
        public async Task ReadAllAsync_ShouldStreamAllRows()
        {
            // Arrange
            IExcelImporter importer = new ExcelImporter();
            var collectedRows = new List<MBW.Core.Models.RecipientRow>();

            // Act
            await foreach (var row in importer.ReadAllAsync(ExcelFixtures.SampleRecipientsPath))
            {
                collectedRows.Add(row);
            }

            // Assert
            Assert.AreEqual(5, collectedRows.Count); // 5 data rows

            // Verify first and last
            Assert.AreEqual(2, collectedRows[0].RowNumber);
            Assert.AreEqual(6, collectedRows[4].RowNumber);

            // Verify data integrity
            var firstRow = collectedRows[0];
            Assert.IsTrue(firstRow.TryGet("NIM", out var nim));
            Assert.AreEqual("001", nim);

            var lastRow = collectedRows[4];
            Assert.IsTrue(lastRow.TryGet("NIM", out var lastNim));
            Assert.AreEqual("005", lastNim);
        }

        [TestMethod]
        [ExpectedException(typeof(FileNotFoundException))]
        public async Task GetHeadersAsync_ShouldThrowOnMissingFile()
        {
            // Arrange
            IExcelImporter importer = new ExcelImporter();

            // Act
            await importer.GetHeadersAsync("/nonexistent/path/file.xlsx");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task PreviewAsync_ShouldThrowOnEmptyPath()
        {
            // Arrange
            IExcelImporter importer = new ExcelImporter();

            // Act
            await importer.PreviewAsync("", 10);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task PreviewAsync_ShouldThrowOnInvalidMaxRows()
        {
            // Arrange
            IExcelImporter importer = new ExcelImporter();

            // Act
            await importer.PreviewAsync(ExcelFixtures.SampleRecipientsPath, 0);
        }

        [TestMethod]
        public async Task RecipientRow_FieldsAreCaseInsensitive()
        {
            // Arrange
            IExcelImporter importer = new ExcelImporter();

            // Act
            var preview = await importer.PreviewAsync(ExcelFixtures.SampleRecipientsPath, 1);

            // Assert
            var row = preview[0];

            // All these should work with case-insensitivity
            Assert.IsTrue(row.TryGet("nama", out var value1));
            Assert.AreEqual("Ahmad Rizki", value1);

            Assert.IsTrue(row.TryGet("NAMA", out var value2));
            Assert.AreEqual("Ahmad Rizki", value2);

            Assert.IsTrue(row.TryGet("Nama", out var value3));
            Assert.AreEqual("Ahmad Rizki", value3);
        }

        [TestMethod]
        public async Task PreviewAsync_DefaultMaxRowsIs10()
        {
            // Arrange
            IExcelImporter importer = new ExcelImporter();

            // Act - call without maxRows parameter (uses default 10)
            var preview = await importer.PreviewAsync(ExcelFixtures.SampleRecipientsPath);

            // Assert
            // Sample file only has 5 rows, so all should be returned
            Assert.AreEqual(5, preview.Count);
        }
    }
}

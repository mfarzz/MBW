using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ClosedXML.Excel;

namespace MBW.Tests.Fixtures
{
    public static class ExcelFixtures
    {
        private static readonly string FixturesDir = Path.Combine(
            Path.GetDirectoryName(typeof(ExcelFixtures).Assembly.Location) ?? AppContext.BaseDirectory,
            "Fixtures");

        /// <summary>
        /// Path to sample-recipients.xlsx test file.
        /// </summary>
        public static string SampleRecipientsPath => Path.Combine(FixturesDir, "sample-recipients.xlsx");

        /// <summary>
        /// Ensure test fixtures directory and sample Excel file exist.
        /// Call once before tests run.
        /// </summary>
        public static void EnsureFixtures()
        {
            Directory.CreateDirectory(FixturesDir);

            if (!File.Exists(SampleRecipientsPath))
            {
                CreateSampleRecipientsExcel();
            }
        }

        private static void CreateSampleRecipientsExcel()
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheet("Recipients");

            // Headers
            worksheet.Cell(1, 1).Value = "NIM";
            worksheet.Cell(1, 2).Value = "Nama";
            worksheet.Cell(1, 3).Value = "Email";
            worksheet.Cell(1, 4).Value = "Fakultas";
            worksheet.Cell(1, 5).Value = "Program_Studi";

            // Sample data
            var data = new[]
            {
                ("001", "Ahmad Rizki", "ahmad@example.com", "FTI", "Informatika"),
                ("002", "Budi Santoso", "budi@example.com", "FTI", "Rekayasa Perangkat Lunak"),
                ("003", "Citra Dewi", "citra@example.com", "FTI", "Sistem Informasi"),
                ("004", "Dian Kusuma", "dian@example.com", "FEB", "Akuntansi"),
                ("005", "Eka Putri", "eka@example.com", "FEB", "Manajemen"),
            };

            int row = 2;
            foreach (var (nim, nama, email, fakultas, prodi) in data)
            {
                worksheet.Cell(row, 1).Value = nim;
                worksheet.Cell(row, 2).Value = nama;
                worksheet.Cell(row, 3).Value = email;
                worksheet.Cell(row, 4).Value = fakultas;
                worksheet.Cell(row, 5).Value = prodi;
                row++;
            }

            // Adjust column widths
            worksheet.Column(1).Width = 8;
            worksheet.Column(2).Width = 18;
            worksheet.Column(3).Width = 25;
            worksheet.Column(4).Width = 12;
            worksheet.Column(5).Width = 25;

            workbook.SaveAs(SampleRecipientsPath);
        }
    }
}

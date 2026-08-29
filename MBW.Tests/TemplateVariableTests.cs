using Microsoft.VisualStudio.TestTools.UnitTesting;
using MBW.Core.Models;
using MBW.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MBW.Tests
{
    [TestClass]
    public sealed class TemplateVariableTests
    {
        [TestMethod]
        public void ExtractVariables_ShouldFindAllUniquePlaceholders()
        {
            // Arrange
            var template = "Halo {Nama}, email Anda adalah {Email}. Fakultas: {Fakultas}";

            // Act
            var variables = TemplateVariableExtractor.ExtractVariables(template);

            // Assert
            Assert.AreEqual(3, variables.Count);
            Assert.IsTrue(variables.Contains("Nama"));
            Assert.IsTrue(variables.Contains("Email"));
            Assert.IsTrue(variables.Contains("Fakultas"));
        }

        [TestMethod]
        public void ExtractVariables_ShouldHandleDuplicates()
        {
            // Arrange
            var template = "Dear {Nama}, your first name is {Nama} and last name is {Nama}.";

            // Act
            var variables = TemplateVariableExtractor.ExtractVariables(template);

            // Assert
            // Should return set with only one "Nama" (duplicates removed)
            Assert.AreEqual(1, variables.Count);
            Assert.IsTrue(variables.Contains("Nama"));
        }

        [TestMethod]
        public void ExtractVariables_ShouldReturnEmptyForEmptyTemplate()
        {
            // Act
            var variables = TemplateVariableExtractor.ExtractVariables("");

            // Assert
            Assert.AreEqual(0, variables.Count);
        }

        [TestMethod]
        public void ExtractVariables_ShouldReturnEmptyForNullTemplate()
        {
            // Act
            var variables = TemplateVariableExtractor.ExtractVariables(null);

            // Assert
            Assert.AreEqual(0, variables.Count);
        }

        [TestMethod]
        public void RenderTemplate_ShouldSubstituteAllVariables()
        {
            // Arrange
            var template = "Halo {Nama}, email Anda: {Email}";
            var variables = new Dictionary<string, string>
            {
                ["Nama"] = "Ahmad",
                ["Email"] = "ahmad@example.com"
            };

            // Act
            var rendered = TemplateVariableExtractor.RenderTemplate(template, variables);

            // Assert
            Assert.AreEqual("Halo Ahmad, email Anda: ahmad@example.com", rendered);
        }

        [TestMethod]
        public void RenderTemplate_ShouldLeaveMissingVariablesAsIs()
        {
            // Arrange
            var template = "Nama: {Nama}, Email: {Email}, Phone: {Phone}";
            var variables = new Dictionary<string, string>
            {
                ["Nama"] = "Budi",
                ["Email"] = "budi@example.com"
                // Phone deliberately missing
            };

            // Act
            var rendered = TemplateVariableExtractor.RenderTemplate(template, variables);

            // Assert
            Assert.AreEqual("Nama: Budi, Email: budi@example.com, Phone: {Phone}", rendered);
        }

        [TestMethod]
        public void RenderTemplate_ShouldHandleNullVariablesDictionary()
        {
            // Arrange
            var template = "Hello {Nama}";

            // Act
            var rendered = TemplateVariableExtractor.RenderTemplate(template, null);

            // Assert
            Assert.AreEqual(template, rendered); // No substitution, return as-is
        }

        [TestMethod]
        public void RenderTemplate_ShouldHandleEmptyTemplate()
        {
            // Arrange
            var variables = new Dictionary<string, string> { ["Nama"] = "Ahmad" };

            // Act
            var rendered = TemplateVariableExtractor.RenderTemplate("", variables);

            // Assert
            Assert.AreEqual("", rendered);
        }

        [TestMethod]
        public void EmailTemplate_GetAvailableVariables_ShouldExtractFromSubjectAndBody()
        {
            // Arrange
            var template = new EmailTemplate(
                subject: "Undangan untuk {Nama}",
                htmlBody: "<p>Halo {Nama}, Anda dari {Fakultas}</p>"
            );

            // Act
            var variables = template.GetAvailableVariables();

            // Assert
            Assert.AreEqual(2, variables.Count);
            Assert.IsTrue(variables.Contains("Nama"));
            Assert.IsTrue(variables.Contains("Fakultas"));
        }

        [TestMethod]
        public void EmailTemplate_RenderForRecipient_ShouldSubstituteAllFields()
        {
            // Arrange
            var template = new EmailTemplate(
                subject: "Undangan untuk {Nama}",
                htmlBody: "<p>Halo {Nama},<br/>Email: {Email}<br/>Fakultas: {Fakultas}</p>"
            );
            var recipient = new RecipientRow(1, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Nama"] = "Ahmad Rizki",
                ["Email"] = "ahmad@example.com",
                ["Fakultas"] = "FTI"
            });

            // Act
            var rendered = template.RenderForRecipient(recipient);

            // Assert
            Assert.AreEqual("Undangan untuk Ahmad Rizki", rendered.Subject);
            Assert.AreEqual("<p>Halo Ahmad Rizki,<br/>Email: ahmad@example.com<br/>Fakultas: FTI</p>", rendered.HtmlBody);
        }

        [TestMethod]
        public void EmailTemplate_RenderForRecipient_ShouldNotMutateOriginal()
        {
            // Arrange
            var template = new EmailTemplate(
                subject: "Halo {Nama}",
                htmlBody: "Body {Nama}"
            );
            var recipient = new RecipientRow(1, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Nama"] = "Budi"
            });

            // Act
            var rendered = template.RenderForRecipient(recipient);

            // Assert
            // Original should be unchanged
            Assert.AreEqual("Halo {Nama}", template.Subject);
            Assert.AreEqual("Body {Nama}", template.HtmlBody);

            // Rendered should have substitutions
            Assert.AreEqual("Halo Budi", rendered.Subject);
            Assert.AreEqual("Body Budi", rendered.HtmlBody);
        }

        [TestMethod]
        public void EmailTemplate_RenderForRecipient_ShouldHandleMultipleRecipients()
        {
            // Arrange
            var template = new EmailTemplate(
                subject: "Halo {Nama}",
                htmlBody: "<p>{Nama} ({Email})</p>"
            );

            var recipient1 = new RecipientRow(1, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Nama"] = "Ahmad",
                ["Email"] = "ahmad@example.com"
            });

            var recipient2 = new RecipientRow(2, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Nama"] = "Budi",
                ["Email"] = "budi@example.com"
            });

            // Act
            var rendered1 = template.RenderForRecipient(recipient1);
            var rendered2 = template.RenderForRecipient(recipient2);

            // Assert
            Assert.AreEqual("Halo Ahmad", rendered1.Subject);
            Assert.AreEqual("<p>Ahmad (ahmad@example.com)</p>", rendered1.HtmlBody);

            Assert.AreEqual("Halo Budi", rendered2.Subject);
            Assert.AreEqual("<p>Budi (budi@example.com)</p>", rendered2.HtmlBody);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void EmailTemplate_RenderForRecipient_ShouldThrowOnNullRecipient()
        {
            // Arrange
            var template = new EmailTemplate("Subject", "Body");

            // Act
            template.RenderForRecipient(null!);
        }

        [TestMethod]
        public void EmailTemplate_RenderForRecipient_ShouldPreservePlainTextBody()
        {
            // Arrange
            var template = new EmailTemplate(
                subject: "Subject {Nama}",
                htmlBody: "<p>HTML {Nama}</p>"
            )
            {
                PlainTextBody = "Plain text {Nama}"
            };

            var recipient = new RecipientRow(1, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Nama"] = "Ahmad"
            });

            // Act
            var rendered = template.RenderForRecipient(recipient);

            // Assert
            Assert.AreEqual("Subject Ahmad", rendered.Subject);
            Assert.AreEqual("<p>HTML Ahmad</p>", rendered.HtmlBody);
            Assert.AreEqual("Plain text Ahmad", rendered.PlainTextBody);
        }

        [TestMethod]
        public void ExtractVariables_ShouldIgnoreInvalidPatterns()
        {
            // Arrange
            var template = "Valid {ValidName} but invalid {123Number} and {-BadStart}";

            // Act
            var variables = TemplateVariableExtractor.ExtractVariables(template);

            // Assert
            // Only "{ValidName}" should match; {123Number} and {-BadStart} don't match the pattern
            Assert.AreEqual(1, variables.Count);
            Assert.IsTrue(variables.Contains("ValidName"));
        }

        [TestMethod]
        public void RenderTemplate_ShouldCaseSensitiveMatchVariables()
        {
            // Arrange
            var template = "Hello {Nama} and {nama}";
            var variables = new Dictionary<string, string>
            {
                ["Nama"] = "Ahmad",
                ["nama"] = "ahmad" // Different case
            };

            // Act
            var rendered = TemplateVariableExtractor.RenderTemplate(template, variables);

            // Assert
            // Should respect case sensitivity
            Assert.AreEqual("Hello Ahmad and ahmad", rendered);
        }
    }
}

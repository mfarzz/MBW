using Microsoft.VisualStudio.TestTools.UnitTesting;
using MBW.Core.Models;
using MBW.Core.Interfaces;
using System.Collections.Generic;

namespace MBW.Tests
{
    [TestClass]
    public sealed class CoreModelsTests
    {
        [TestMethod]
        public void WorkspaceModel_DefaultsAndProperties()
        {
            var w = new WorkspaceModel { Name = "Test" };
            Assert.AreEqual("Test", w.Name);
            Assert.IsNotNull(w.Metadata);
            Assert.IsNotNull(w.Id);
        }

        [TestMethod]
        public void EmailTemplate_CanConstructAndAssign()
        {
            var t = new EmailTemplate("S", "<p>hi</p>");
            Assert.AreEqual("S", t.Subject);
            Assert.AreEqual("<p>hi</p>", t.HtmlBody);
        }

        [TestMethod]
        public void RecipientRow_BasicAccessors()
        {
            var dict = new Dictionary<string,string> { ["Nama"] = "Budi" };
            var r = new RecipientRow(1, dict);
            Assert.AreEqual(1, r.RowNumber);
            Assert.IsTrue(r.TryGet("Nama", out var val));
            Assert.AreEqual("Budi", val);
        }

        [TestMethod]
        public void Interfaces_AreDefined()
        {
            Assert.IsTrue(typeof(IWorkspaceService).IsInterface);
            Assert.IsTrue(typeof(IExcelImporter).IsInterface);
            Assert.IsTrue(typeof(IEmailSender).IsInterface);
            Assert.IsTrue(typeof(IAttachmentService).IsInterface);
            Assert.IsTrue(typeof(IStorageService).IsInterface);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TheMaxLab.SqlServerDiagnostics.App.Presentation;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.Diagnostics.Tests
{
    [TestClass]
    public sealed class PresentationTests
    {
        [TestMethod]
        public void ExplorerFilter_SearchesNameDescriptionAndTagsCaseInsensitively()
        {
            var diagnostics = new[] { Definition("missing-indexes", "Missing Indexes", "Candidate indexes", DiagnosticCategory.Indexes, "dmv"), Definition("waits", "Wait Statistics", "Server waits", DiagnosticCategory.Performance, "cpu") };
            var presentation = new DiagnosticExplorerPresentation();
            Assert.AreEqual("missing-indexes", presentation.Filter(diagnostics, null, "MISSING").Single().Id);
            Assert.AreEqual("waits", presentation.Filter(diagnostics, null, "server").Single().Id);
            Assert.AreEqual("waits", presentation.Filter(diagnostics, null, "CPU").Single().Id);
            Assert.AreEqual(0, presentation.Filter(diagnostics, DiagnosticCategory.Schema, string.Empty).Count);
        }

        [TestMethod]
        public void ExplorerCounts_AreCalculatedFromDefinitions()
        {
            var diagnostics = new[] { Definition("a", "A", "A", DiagnosticCategory.Performance, "one"), Definition("b", "B", "B", DiagnosticCategory.Performance, "two"), Definition("c", "C", "C", DiagnosticCategory.Schema, "three") };
            var counts = new DiagnosticExplorerPresentation().GetCategoryCounts(diagnostics);
            Assert.AreEqual(2, counts[DiagnosticCategory.Performance]); Assert.AreEqual(1, counts[DiagnosticCategory.Schema]); Assert.AreEqual(0, counts[DiagnosticCategory.Indexes]);
        }

        [TestMethod]
        public void ResultPresentation_MapsStatusAndTruncationWithoutInventingPass()
        {
            var presentation = new DiagnosticResultPresentation();
            var result = new DiagnosticResult { Status = DiagnosticExecutionStatus.Succeeded };
            Assert.AreEqual("Succeeded", presentation.GetDisplayStatus(result));
            result.Findings.Add(new DiagnosticFinding { Severity = DiagnosticSeverity.Warning });
            Assert.AreEqual("Warning", presentation.GetDisplayStatus(result));
            var set = new DiagnosticResultSet { Index = 1, Name = "Result 2", RowsRead = 125432, IsTruncated = true };
            for (int i = 0; i < 10000; i++) set.Rows.Add(new Dictionary<string, object>());
            StringAssert.Contains(presentation.GetResultSetTitle(set), "truncated");
            StringAssert.Contains(presentation.GetTruncationMessage(set), set.RowsRead.ToString("N0"));
        }

        [TestMethod]
        public void HealthSummary_CountsFindingsAndStatusesWithoutScore()
        {
            var report = new HealthCheckReport();
            var success = new DiagnosticResult { Status = DiagnosticExecutionStatus.Succeeded }; success.Findings.Add(new DiagnosticFinding { Severity = DiagnosticSeverity.Critical }); success.Findings.Add(new DiagnosticFinding { Severity = DiagnosticSeverity.Warning });
            report.Results.Add(success); report.Results.Add(new DiagnosticResult { Status = DiagnosticExecutionStatus.Skipped }); report.Results.Add(new DiagnosticResult { Status = DiagnosticExecutionStatus.Failed }); report.Results.Add(new DiagnosticResult { Status = DiagnosticExecutionStatus.Cancelled });
            HealthCheckSummary summary = HealthCheckSummary.FromReport(report, 16);
            Assert.AreEqual(16, summary.Selected); Assert.AreEqual(4, summary.Completed); Assert.AreEqual(1, summary.Critical); Assert.AreEqual(1, summary.Warnings); Assert.AreEqual(1, summary.Skipped); Assert.AreEqual(1, summary.Failed); Assert.AreEqual(1, summary.Cancelled); Assert.IsNull(report.Score);
        }

        [TestMethod]
        public void ConnectionProfilePresentation_MapsWindowsAndRememberedSqlConnections()
        {
            var presentation = new ConnectionProfilePresentation();
            var windows = new DatabaseConnection { ServerName = "server", DatabaseName = "db", AuthenticationType = AuthenticationType.Windows, CredentialKey = "ignored", ConnectionTimeoutSeconds = 15 };
            ConnectionProfile windowsProfile = presentation.CreateProfile(windows, " Windows profile ");
            Assert.AreEqual("Windows profile", windowsProfile.Name);
            Assert.AreEqual(AuthenticationType.Windows, windowsProfile.AuthenticationType);
            Assert.IsNull(windowsProfile.CredentialKey);

            var sql = new DatabaseConnection { ServerName = "server", DatabaseName = "db", AuthenticationType = AuthenticationType.SqlServer, UserName = "user", CredentialKey = "saved:key", ConnectionTimeoutSeconds = 30 };
            ConnectionProfile sqlProfile = presentation.CreateProfile(sql, "SQL profile", "existing-id");
            Assert.AreEqual("existing-id", sqlProfile.Id);
            Assert.AreEqual("saved:key", sqlProfile.CredentialKey);
            Assert.AreEqual("user", sqlProfile.UserName);
        }

        [TestMethod]
        public void ConnectionProfilePresentation_RejectsSessionCredentialPersistence()
        {
            var connection = new DatabaseConnection { ServerName = "server", DatabaseName = "db", AuthenticationType = AuthenticationType.SqlServer, UserName = "user", CredentialKey = "session:key", ConnectionTimeoutSeconds = 15 };
            try
            {
                new ConnectionProfilePresentation().CreateProfile(connection, "Unsafe profile");
                Assert.Fail("A session-only credential must not be persisted in a profile.");
            }
            catch (InvalidOperationException exception)
            {
                StringAssert.Contains(exception.Message, "Remember credentials");
            }
        }

        private static DiagnosticDefinition Definition(string id, string name, string description, DiagnosticCategory category, string tag) { var value = new DiagnosticDefinition { Id = id, Name = name, Description = description, Category = category, ScriptPath = category + "/" + id + ".sql" }; value.Tags.Add(tag); return value; }
    }
}
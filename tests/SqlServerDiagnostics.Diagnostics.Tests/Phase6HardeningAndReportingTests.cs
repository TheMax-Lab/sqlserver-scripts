using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;
using TheMaxLab.SqlServerDiagnostics.Core.Models;
using TheMaxLab.SqlServerDiagnostics.Infrastructure.Profiles;
using TheMaxLab.SqlServerDiagnostics.Infrastructure.Security;
using TheMaxLab.SqlServerDiagnostics.Reporting;
using TheMaxLab.SqlServerDiagnostics.App.Presentation;

namespace TheMaxLab.SqlServerDiagnostics.Diagnostics.Tests
{
    [TestClass]
    public sealed class Phase6HardeningAndReportingTests
    {
        [TestMethod]
        public void AssessmentPolicy_ClassifiesCompletePartialInconclusiveAndFailed()
        {
            Assert.AreEqual(AssessmentStatus.Complete, Status(100));
            Assert.AreEqual(AssessmentStatus.PartiallyComplete, Status(70));
            Assert.AreEqual(AssessmentStatus.PartiallyComplete, Status(99.99m));
            Assert.AreEqual(AssessmentStatus.Inconclusive, Status(69.99m));
            Assert.AreEqual(AssessmentStatus.Failed, Status(0));
        }

        [TestMethod]
        public void PerfectScoreWithIncompleteCoverage_IsExplicitlyPartial()
        {
            HealthReport report = SampleReport();
            Assert.AreEqual(100m, report.HealthScore.Percentage);
            Assert.AreEqual(AssessmentStatus.PartiallyComplete, report.AssessmentStatus);
            Assert.IsTrue(report.Coverage.CoveragePercentage < 100m);
        }

        [TestMethod]
        public void Reports_UseStableSchemaEscapeHostileHtmlAndExcludeSecrets()
        {
            using (var directory = new TemporaryDirectory())
            {
                HealthReport report = SampleReport(); var service = new HealthReportService(); var options = new ReportOptions();
                string jsonPath = Path.Combine(directory.Path, "report.json"), htmlPath = Path.Combine(directory.Path, "report.html");
                service.ExportJsonAsync(report, options, jsonPath, CancellationToken.None).GetAwaiter().GetResult();
                service.ExportHtmlAsync(report, options, htmlPath, CancellationToken.None).GetAwaiter().GetResult();
                string json = File.ReadAllText(jsonPath), html = File.ReadAllText(htmlPath);
                StringAssert.Contains(json, "\"schemaVersion\":\"1.0\""); StringAssert.Contains(json, "SqlServerHealthReport");
                StringAssert.Contains(json, "ALTER TABLE dbo.Safe ADD C int"); StringAssert.Contains(html, "&lt;script&gt;alert(&#39;x&#39;)&lt;/script&gt;");
                Assert.IsFalse(html.Contains("<script>alert('x')</script>")); AssertNoSecret(json); AssertNoSecret(html);
                Assert.IsFalse(html.Contains("http://")); Assert.IsFalse(html.Contains("https://"));
            }
        }

        [TestMethod]
        public void Csv_EscapesCommasQuotesNewLinesAndExcludesSecrets()
        {
            using (var directory = new TemporaryDirectory())
            {
                string path = Path.Combine(directory.Path, "findings.csv");
                new HealthReportService().ExportFindingsCsvAsync(SampleReport(), new ReportOptions(), path, CancellationToken.None).GetAwaiter().GetResult();
                string csv = File.ReadAllText(path);
                StringAssert.Contains(csv, "\"Title, with \"\"quotes\"\"\"");
                StringAssert.Contains(csv, "line one\r\nline two"); AssertNoSecret(csv);
            }
        }

        [TestMethod]
        public void Json_RawResultsAreOptInAndSuggestedSqlCanBeExcluded()
        {
            using (var directory = new TemporaryDirectory())
            {
                var service = new HealthReportService(); string compact = Path.Combine(directory.Path, "compact.json"), full = Path.Combine(directory.Path, "full.json");
                service.ExportJsonAsync(SampleReport(), new ReportOptions { IncludeRawResults = false, IncludeSuggestedSql = false }, compact, CancellationToken.None).Wait();
                service.ExportJsonAsync(SampleReport(), new ReportOptions { IncludeRawResults = true, IncludeSuggestedSql = true }, full, CancellationToken.None).Wait();
                Assert.IsFalse(File.ReadAllText(compact).Contains("RAW_RESULT_ONLY_MARKER"));
                StringAssert.Contains(File.ReadAllText(full), "RAW_RESULT_ONLY_MARKER");
            }
        }

        [TestMethod]
        public void ReportCancellation_DoesNotReplaceExistingDestination()
        {
            using (var directory = new TemporaryDirectory()) using (var cancellation = new CancellationTokenSource())
            {
                string path = Path.Combine(directory.Path, "report.json"); File.WriteAllText(path, "existing"); cancellation.Cancel();
                try { new HealthReportService().ExportJsonAsync(SampleReport(), new ReportOptions(), path, cancellation.Token).GetAwaiter().GetResult(); Assert.Fail("Cancellation expected."); }
                catch (OperationCanceledException) { }
                Assert.AreEqual("existing", File.ReadAllText(path));
            }
        }

        [TestMethod]
        public void Dpapi_MissingDeletedAndCorruptedCredentialsAreControlled()
        {
            using (var directory = new TemporaryDirectory())
            {
                var service = new DpapiCredentialService(directory.Path);
                AssertControlled(() => service.GetPasswordAsync("missing", CancellationToken.None).GetAwaiter().GetResult());
                service.SaveAsync("saved", "user", "secret", CancellationToken.None).Wait(); service.DeleteAsync("saved", CancellationToken.None).Wait();
                Assert.IsFalse(service.ExistsAsync("saved", CancellationToken.None).Result); AssertControlled(() => service.GetPasswordAsync("saved", CancellationToken.None).GetAwaiter().GetResult());
                service.SaveAsync("corrupt", "user", "secret", CancellationToken.None).Wait(); File.WriteAllBytes(Directory.GetFiles(directory.Path, "*.credential").Single(), Encoding.UTF8.GetBytes("not-dpapi"));
                AssertControlled(() => service.GetPasswordAsync("corrupt", CancellationToken.None).GetAwaiter().GetResult());
            }
        }

        [TestMethod]
        public void Profiles_AreVersionedPasswordFreeAndDeleteProtectedCredential()
        {
            using (var directory = new TemporaryDirectory())
            {
                var credentials = new DpapiCredentialService(Path.Combine(directory.Path, "credentials")); string key = "profile-key";
                credentials.SaveAsync(key, "user", "DO_NOT_EXPORT", CancellationToken.None).Wait(); string path = Path.Combine(directory.Path, "profiles.json"); var profiles = new JsonConnectionProfileService(path, credentials);
                profiles.SaveAsync(new ConnectionProfile { Id="id", Name="name", Server="server", Database="db", AuthenticationType=AuthenticationType.SqlServer, UserName="user", CredentialKey=key, TimeoutSeconds=15 }, CancellationToken.None).Wait();
                string json = File.ReadAllText(path); StringAssert.Contains(json, "schemaVersion"); Assert.IsFalse(json.Contains("DO_NOT_EXPORT"));
                profiles.DeleteAsync("id", CancellationToken.None).Wait(); Assert.IsFalse(credentials.ExistsAsync(key, CancellationToken.None).Result);
                File.WriteAllText(path, "{ malformed"); AssertControlled(() => profiles.GetAllAsync(CancellationToken.None).GetAwaiter().GetResult());
            }
        }

        [TestMethod]
        public void ReportSession_PreventsExportAfterDatabaseSwitchOrDisconnect()
        {
            var session = new HealthReportSession(); var first = Context("server", "DatabaseA"); var second = Context("server", "DatabaseB"); var report = SampleReport();
            session.Set(report, first); Assert.AreSame(report, session.GetFor(first)); Assert.IsNull(session.GetFor(second)); Assert.IsNull(session.GetFor(null)); session.Clear(); Assert.IsNull(session.GetFor(first));
        }

        private static AssessmentStatus Status(decimal coverage) { return HealthAssessmentPolicy.GetStatus(new HealthCoverage { CoveragePercentage = coverage }); }
        private static DiagnosticExecutionContext Context(string server, string database) { return new DiagnosticExecutionContext { Server = new SqlServerInfo { ServerName=server }, Database = new DatabaseInfo { Name=database } }; }
        private static void AssertNoSecret(string value) { Assert.IsFalse(value.Contains("DO_NOT_EXPORT")); Assert.IsFalse(value.Contains("profile-key")); Assert.IsFalse(value.Contains("Password=")); Assert.IsFalse(value.Contains("Data Source=")); }
        private static void AssertControlled(Action action) { try { action(); Assert.Fail("A controlled exception was expected."); } catch (InvalidOperationException exception) { Assert.IsFalse(exception.Message.Contains("CryptographicException")); } }

        private static HealthReport SampleReport()
        {
            var report = new HealthReport { GeneratedAt=DateTimeOffset.Parse("2026-09-03T15:30:00Z"), ApplicationVersion=ApplicationInfo.ApplicationVersion, Server=new SqlServerInfo { ServerName="<script>alert('x')</script>", ProductVersion="15.0", Edition="Express & Test" }, Database=new DatabaseInfo { Name="Robert'); DROP TABLE Test;--", CompatibilityLevel=150 }, SqlServerVersion="15.0", AssessmentStatus=AssessmentStatus.PartiallyComplete, AssessmentMessage="Assessment <incomplete>" };
            report.Coverage = new HealthCoverage { EligibleDiagnostics=2, ExecutedDiagnostics=2, SuccessfulDiagnostics=1, FailedDiagnostics=1, CoveragePercentage=50 };
            report.HealthScore = new HealthScore { Score=1, MaxScore=1, Percentage=100, Grade="Excellent", Confidence=InterpretationConfidence.Low, LogicalGroupsEvaluated=1, DiagnosticsEvaluated=1 };
            report.HealthScore.Breakdown.Add(new HealthScoreBreakdown { DiagnosticId="safe", DiagnosticName="Safe", Included=true, Weight=1, Explanation="No penalty" });
            var finding = new DiagnosticFinding { Id="f1", DiagnosticId="safe", Severity=DiagnosticSeverity.Warning, Impact=FindingImpact.High, Confidence=InterpretationConfidence.High, Title="Title, with \"quotes\"", Description="line one\r\nline two; password=\"DO_NOT_EXPORT\"", Recommendation="Review & validate", SuggestedSql="ALTER TABLE dbo.Safe ADD C int" };
            report.Findings.Add(finding); report.LogicalFindings.Add(new LogicalHealthFinding { Group="safe", PrimaryFinding=finding, Severity=finding.Severity, Impact=finding.Impact });
            var result = new DiagnosticResult { DiagnosticId="safe", DiagnosticName="Safe", Category=DiagnosticCategory.Schema, Status=DiagnosticExecutionStatus.Succeeded, UserMessage="Done" }; var set = new DiagnosticResultSet { Index=0, Name="Result" }; set.Columns.Add(new DiagnosticColumn { Name="Value", Key="Value", Ordinal=0 }); set.Rows.Add(new System.Collections.Generic.Dictionary<string, object>{{"Value", "RAW_RESULT_ONLY_MARKER"}}); result.ResultSets.Add(set); report.Results.Add(result);
            report.Results.Add(new DiagnosticResult { DiagnosticId="failed", DiagnosticName="Failed", Category=DiagnosticCategory.Schema, Status=DiagnosticExecutionStatus.Failed, FailureKind=DiagnosticFailureKind.SqlError, UserMessage="Controlled error" }); return report;
        }

        private sealed class TemporaryDirectory : IDisposable { public TemporaryDirectory(){Path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"SqlServerDiagnostics-P6-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path);} public string Path{get;private set;} public void Dispose(){if(Directory.Exists(Path))Directory.Delete(Path,true);} }
    }
}
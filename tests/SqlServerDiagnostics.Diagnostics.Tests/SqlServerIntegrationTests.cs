using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;
using TheMaxLab.SqlServerDiagnostics.Core.Interfaces;
using TheMaxLab.SqlServerDiagnostics.Core.Models;
using TheMaxLab.SqlServerDiagnostics.Diagnostics.Execution;
using TheMaxLab.SqlServerDiagnostics.Diagnostics.Interpretation;
using TheMaxLab.SqlServerDiagnostics.Diagnostics.Repositories;
using TheMaxLab.SqlServerDiagnostics.Infrastructure.Connections;
using TheMaxLab.SqlServerDiagnostics.Infrastructure.Security;

namespace TheMaxLab.SqlServerDiagnostics.Diagnostics.Tests
{
    [TestClass]
    public sealed class SqlServerIntegrationTests
    {
        [TestMethod]
        [TestCategory("Integration")]
        public void ConfiguredSqlServer_ReturnsMetadata()
        {
            string server = Environment.GetEnvironmentVariable("SQLSERVER_DIAGNOSTICS_TEST_SERVER");
            if (string.IsNullOrWhiteSpace(server)) Assert.Inconclusive("Set SQLSERVER_DIAGNOSTICS_TEST_SERVER to run integration tests.");
            string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SqlServerDiagnostics-Integration");
            var credentials = new DpapiCredentialService(directory);
            var service = new SqlServerService(new SecureConnectionStringFactory(credentials));
            var connection = new DatabaseConnection { ServerName = server, DatabaseName = "master", AuthenticationType = AuthenticationType.Windows, Encrypt = false, TrustServerCertificate = true };
            SqlServerInfo info = service.GetServerInfoAsync(connection, CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsTrue(info.MajorVersion > 0);
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void ConfiguredSqlServer_ExecutesOneDiagnosticPerCategory()
        {
            string server = Environment.GetEnvironmentVariable("SQLSERVER_DIAGNOSTICS_TEST_SERVER");
            if (string.IsNullOrWhiteSpace(server)) Assert.Inconclusive("Set SQLSERVER_DIAGNOSTICS_TEST_SERVER to run integration tests.");
            string root = FindRepositoryRoot();
            string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SqlServerDiagnostics-Integration");
            var credentials = new DpapiCredentialService(directory);
            var service = new SqlServerService(new SecureConnectionStringFactory(credentials));
            var connection = new DatabaseConnection { ServerName = server, DatabaseName = "master", AuthenticationType = AuthenticationType.Windows, Encrypt = false, TrustServerCertificate = true };
            var context = new DiagnosticExecutionContext { Connection = connection, Server = service.GetServerInfoAsync(connection, CancellationToken.None).Result, Database = service.GetDatabaseInfoAsync(connection, CancellationToken.None).Result };
            Assert.IsTrue(System.Linq.Enumerable.Any(service.GetDatabasesAsync(connection, CancellationToken.None).Result, item => item.Name == "master" && item.IsAccessible));
            var repository = new ManifestDiagnosticRepository(System.IO.Path.Combine(root, "diagnostics"));
            var engine = new DiagnosticEngine(repository, service, new DiagnosticResultNormalizer(), new DiagnosticErrorClassifier(), new NullLogger());
            var definitions = repository.GetAllAsync(CancellationToken.None).Result;
            foreach (string id in new[] { "blocking-sessions", "missing-indexes", "untrusted-constraints", "database-configuration" })
            {
                DiagnosticDefinition definition = System.Linq.Enumerable.Single(definitions, item => item.Id == id);
                DiagnosticResult result = engine.ExecuteAsync(definition, context, CancellationToken.None).Result;
                Assert.AreEqual(DiagnosticExecutionStatus.Succeeded, result.Status, id + ": " + result.UserMessage);
                if (id == "database-configuration") Assert.IsTrue(System.Linq.Enumerable.Any(result.Findings, finding => finding.SuggestedSql.Contains("ALTER DATABASE")));
            }
            DiagnosticResult multiple = engine.ExecuteAsync(System.Linq.Enumerable.Single(definitions, item => item.Id == "memory-pressure"), context, CancellationToken.None).Result;
            Assert.AreEqual(DiagnosticExecutionStatus.Succeeded, multiple.Status, multiple.UserMessage);
            Assert.IsTrue(multiple.ResultSets.Count >= 2);
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void ConfiguredSqlServer_ExecutesCorrectedDiagnostics()
        {
            string server = Environment.GetEnvironmentVariable("SQLSERVER_DIAGNOSTICS_TEST_SERVER");
            if (string.IsNullOrWhiteSpace(server)) Assert.Inconclusive("Set SQLSERVER_DIAGNOSTICS_TEST_SERVER to run integration tests.");
            string root = FindRepositoryRoot();
            var credentials = new DpapiCredentialService(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SqlServerDiagnostics-Integration"));
            var service = new SqlServerService(new SecureConnectionStringFactory(credentials));
            var connection = new DatabaseConnection { ServerName = server, DatabaseName = "master", AuthenticationType = AuthenticationType.Windows, Encrypt = false, TrustServerCertificate = true };
            var context = new DiagnosticExecutionContext { Connection = connection, Server = service.GetServerInfoAsync(connection, CancellationToken.None).Result, Database = service.GetDatabaseInfoAsync(connection, CancellationToken.None).Result };
            var repository = new ManifestDiagnosticRepository(System.IO.Path.Combine(root, "diagnostics"));
            var engine = new DiagnosticEngine(repository, service, new DiagnosticResultNormalizer(), new DiagnosticErrorClassifier(), new NullLogger());
            var definitions = repository.GetAllAsync(CancellationToken.None).Result;

            foreach (string id in new[] { "database-sizes", "missing-primary-keys" })
            {
                DiagnosticResult result = engine.ExecuteAsync(System.Linq.Enumerable.Single(definitions, item => item.Id == id), context, CancellationToken.None).Result;
                Assert.AreEqual(DiagnosticExecutionStatus.Succeeded, result.Status, id + ": " + result.UserMessage);
                if (id == "database-sizes")
                {
                    Assert.AreNotEqual(451, result.SqlErrorNumber.GetValueOrDefault(), "database-sizes must not reproduce the collation conflict.");
                    Assert.AreEqual(1, result.ResultSets.Count, "database-sizes must return one result set.");
                    Assert.AreEqual(8, result.ResultSets[0].Columns.Count, "database-sizes must preserve its result contract.");
                }
            }
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void ConfiguredSqlServer_RunsEligibleHealthCheck()
        {
            string server = Environment.GetEnvironmentVariable("SQLSERVER_DIAGNOSTICS_TEST_SERVER");
            if (string.IsNullOrWhiteSpace(server)) Assert.Inconclusive("Set SQLSERVER_DIAGNOSTICS_TEST_SERVER to run integration tests.");
            string root = FindRepositoryRoot();
            var credentials = new DpapiCredentialService(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SqlServerDiagnostics-Integration"));
            var service = new SqlServerService(new SecureConnectionStringFactory(credentials));
            var connection = new DatabaseConnection { ServerName = server, DatabaseName = "master", AuthenticationType = AuthenticationType.Windows, Encrypt = false, TrustServerCertificate = true };
            var context = new DiagnosticExecutionContext { Connection = connection, Server = service.GetServerInfoAsync(connection, CancellationToken.None).Result, Database = service.GetDatabaseInfoAsync(connection, CancellationToken.None).Result };
            var repository = new ManifestDiagnosticRepository(System.IO.Path.Combine(root, "diagnostics"));
            var engine = new DiagnosticEngine(repository, service, new DiagnosticResultNormalizer(), new DiagnosticErrorClassifier(), new NullLogger());
            int selected = engine.GetHealthCheckCandidatesAsync(context, CancellationToken.None).Result.Count;
            HealthCheckReport report = engine.RunHealthCheckAsync(context, null, CancellationToken.None).Result;
            Console.WriteLine("HealthCheck selected={0}; completed={1}; succeeded={2}; skipped={3}; failed={4}; cancelled={5}", selected, report.Results.Count, report.ExecutedCount, report.SkippedCount, report.FailedCount, report.CancelledCount);
            Assert.AreEqual(selected, report.Results.Count);
            Assert.AreEqual(0, report.CancelledCount);
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void ConfiguredSqlServer_BuildsSemanticHealthReport()
        {
            string server = Environment.GetEnvironmentVariable("SQLSERVER_DIAGNOSTICS_TEST_SERVER");
            if (string.IsNullOrWhiteSpace(server)) Assert.Inconclusive("Set SQLSERVER_DIAGNOSTICS_TEST_SERVER to run integration tests.");
            string root = FindRepositoryRoot();
            var credentials = new DpapiCredentialService(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SqlServerDiagnostics-Integration"));
            var service = new SqlServerService(new SecureConnectionStringFactory(credentials));
            var connection = new DatabaseConnection { ServerName = server, DatabaseName = "master", AuthenticationType = AuthenticationType.Windows, Encrypt = false, TrustServerCertificate = true };
            var context = new DiagnosticExecutionContext { Connection = connection, Server = service.GetServerInfoAsync(connection, CancellationToken.None).Result, Database = service.GetDatabaseInfoAsync(connection, CancellationToken.None).Result };
            var repository = new ManifestDiagnosticRepository(System.IO.Path.Combine(root, "diagnostics"));
            var engine = new DiagnosticEngine(repository, service, new DiagnosticResultNormalizer(), new DiagnosticErrorClassifier(), new NullLogger());
            var definitions = repository.GetAllAsync(CancellationToken.None).Result;
            HealthCheckReport execution = engine.RunHealthCheckAsync(context, null, CancellationToken.None).Result;
            var map = System.Linq.Enumerable.ToDictionary(definitions, item => item.Id, StringComparer.OrdinalIgnoreCase);
            var interpreter = new DiagnosticInterpreter();
            var interpretations = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(execution.Results, item => interpreter.Interpret(map[item.DiagnosticId], item)));
            HealthReport report = new HealthScoreService().BuildReport(execution, definitions, interpretations);
            Console.WriteLine("SemanticHealth eligible={0}; completed={1}; succeeded={2}; skipped={3}; failed={4}; score={5:0.##}; grade={6}; coverage={7:0.##}; confidence={8}; assessment={9}; findings={10}; critical={11}; warnings={12}; information={13}", report.Coverage.EligibleDiagnostics, report.Coverage.ExecutedDiagnostics, report.Coverage.SuccessfulDiagnostics, report.Coverage.SkippedDiagnostics, report.Coverage.FailedDiagnostics, report.HealthScore.Percentage, report.HealthScore.Grade, report.Coverage.CoveragePercentage, report.HealthScore.Confidence, report.AssessmentStatus, report.Findings.Count, report.HealthScore.CriticalFindings, report.HealthScore.WarningFindings, report.HealthScore.InformationFindings);
            Assert.IsTrue(report.HealthScore.Percentage >= 0 && report.HealthScore.Percentage <= 100);
            Assert.AreEqual(execution.ExecutedCount, report.Coverage.SuccessfulDiagnostics);
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void ConfiguredSqlServer_CancelsRunningCommand()
        {
            string server = Environment.GetEnvironmentVariable("SQLSERVER_DIAGNOSTICS_TEST_SERVER");
            if (string.IsNullOrWhiteSpace(server)) Assert.Inconclusive("Set SQLSERVER_DIAGNOSTICS_TEST_SERVER to run integration tests.");
            var credentials = new DpapiCredentialService(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SqlServerDiagnostics-Integration"));
            var service = new SqlServerService(new SecureConnectionStringFactory(credentials));
            var connection = new DatabaseConnection { ServerName = server, DatabaseName = "master", AuthenticationType = AuthenticationType.Windows, Encrypt = false, TrustServerCertificate = true };
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200)))
            {
                try
                {
                    service.ExecuteQueryWithMultipleResultsAsync(connection, "WAITFOR DELAY '00:00:05'; SELECT 1;", new List<SqlQueryParameter>(), 30, 100, cancellation.Token).GetAwaiter().GetResult();
                    Assert.Fail("Expected command cancellation.");
                }
                catch (OperationCanceledException) { }
            }
        }

        private static string FindRepositoryRoot()
        {
            var directory = new System.IO.DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null && !System.IO.File.Exists(System.IO.Path.Combine(directory.FullName, "SqlServerDiagnostics.sln"))) directory = directory.Parent;
            if (directory == null) Assert.Inconclusive("Repository root was not found.");
            return directory.FullName;
        }

        private sealed class NullLogger : IApplicationLogger
        {
            public void Log(LogEvent logEvent, Exception exception = null) { }
        }
    }
}